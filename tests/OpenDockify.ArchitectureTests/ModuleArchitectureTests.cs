using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Extensions;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace OpenDockify.ArchitectureTests;

/// <summary>
/// Machine-checked modular-monolith rules from Agents.md §2.
///
/// FIXTURE MAINTENANCE: when a new module is added, add its assembly to
/// <see cref="_moduleAssemblyNames"/>, its allowed OpenDockify dependencies to
/// <see cref="_allowedModuleDependencies"/>, and its name to the Api
/// composition-root expectation. A failing architecture test is the signal
/// that the fixture (or the module graph) is out of date.
/// </summary>
public sealed class ModuleArchitectureTests
{
    private static readonly string[] _moduleAssemblyNames =
    {
        "OpenDockify.Auth",
        "OpenDockify.Templates",
        "OpenDockify.Generation",
        "OpenDockify.Interviews",
        "OpenDockify.Rendering",
        "OpenDockify.Finance",
        "OpenDockify.AiAssist",
        "OpenDockify.Esign",
        "OpenDockify.SystemConfig",
        "OpenDockify.Operations",
        "OpenDockify.Integrations",
        "OpenDockify.Data",
        "OpenDockify.Api",
    };

    /// <summary>Declared OpenDockify module dependencies (Agents.md §2, csproj graph).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> _allowedModuleDependencies =
        new Dictionary<string, string[]>
        {
            ["OpenDockify.Auth"] = Array.Empty<string>(),
            ["OpenDockify.Templates"] = new[] { "OpenDockify.Auth", "OpenDockify.Finance", "OpenDockify.SystemConfig" },
            ["OpenDockify.Generation"] = new[]
            {
                "OpenDockify.Templates",
                "OpenDockify.Finance",
                "OpenDockify.Rendering",
                "OpenDockify.SystemConfig",
            },
            ["OpenDockify.Interviews"] = new[] { "OpenDockify.Generation", "OpenDockify.Templates" },
            ["OpenDockify.Rendering"] = new[] { "OpenDockify.SystemConfig", "OpenDockify.Finance" },
            ["OpenDockify.Finance"] = new[] { "OpenDockify.SystemConfig" },
            ["OpenDockify.AiAssist"] = new[] { "OpenDockify.SystemConfig", "OpenDockify.Templates" },
            ["OpenDockify.Esign"] = Array.Empty<string>(),
            ["OpenDockify.SystemConfig"] = Array.Empty<string>(),
            ["OpenDockify.Operations"] = new[] { "OpenDockify.Generation" },
            ["OpenDockify.Integrations"] = new[] { "OpenDockify.Generation", "OpenDockify.Templates" },
        };

    private static readonly Architecture _architecture = new ArchLoader()
        .LoadAssemblies(_moduleAssemblyNames.Select(Assembly.Load).ToArray())
        .Build();

    private static readonly IObjectProvider<IType> _dataTypes = Types()
        .That().ResideInNamespaceMatching(@"^OpenDockify\.Data($|\.)");

    private static readonly IObjectProvider<IType> _applicationDbContextType = Types()
        .That().HaveFullName("OpenDockify.Data.AppDbContext");

    private static GivenTypesConjunction TypesInNamespaces(params string[] namespaces)
    {
        // Partial-match: each alternative covers the namespace itself (via $)
        // or any sub-namespace (via the dot), without anchoring the end.
        var patterns = namespaces.Select(ns => $"{Regex.Escape(ns)}(\\.|$)");
        return Types().That().ResideInNamespaceMatching($"^({string.Join("|", patterns)})");
    }

    [Fact]
    public void Modules_do_not_depend_on_OpenDockify_Data()
    {
        Types().That().ResideInNamespaceMatching(@"^OpenDockify($|\.)")
            .And().DoNotResideInNamespaceMatching(@"^OpenDockify\.Data($|\.)")
            .And().DoNotResideInNamespaceMatching(@"^OpenDockify\.Api($|\.)")
            .Should().NotDependOnAny(_dataTypes)
            .Because("modules must never reference OpenDockify.Data; services depend on the base DbContext")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);
    }

    [Fact]
    public void Modules_do_not_depend_on_the_concrete_ApplicationDbContext()
    {
        Types().That().ResideInNamespaceMatching(@"^OpenDockify($|\.)")
            .And().DoNotResideInNamespaceMatching(@"^OpenDockify\.Data($|\.)")
            .And().DoNotResideInNamespaceMatching(@"^OpenDockify\.Api($|\.)")
            .Should().NotDependOnAny(_applicationDbContextType)
            .Because("services inject the base Microsoft.EntityFrameworkCore.DbContext to avoid circular references")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);
    }

    [Fact]
    public void Module_dependency_graph_matches_Agents_md()
    {
        foreach (var (module, allowedDependencies) in _allowedModuleDependencies)
        {
            var forbidden = _moduleAssemblyNames
                .Where(ns => ns != module && !allowedDependencies.Contains(ns))
                .ToArray();

            Types().That().ResideInNamespace(module)
                .Should().NotDependOnAny(TypesInNamespaces(forbidden))
                .Because($"'{module}' may only depend on: {(allowedDependencies.Length == 0 ? "no other OpenDockify module" : string.Join(", ", allowedDependencies))}")
                .WithoutRequiringPositiveResults()
                .Check(_architecture);
        }
    }

    [Fact]
    public void Modules_do_not_reference_Api()
    {
        Types().That().ResideInNamespaceMatching(@"^OpenDockify($|\.)")
            .And().DoNotResideInNamespaceMatching(@"^OpenDockify\.Api($|\.)")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^OpenDockify\.Api($|\.)"))
            .Because("the Api project is the composition root; no module may depend on it")
            .WithoutRequiringPositiveResults()
            .Check(_architecture);
    }

    [Fact]
    public void Api_is_the_composition_root_and_references_every_module()
    {
        // The C# compiler drops assembly references to modules whose types are
        // not used, so an empty (stub) module would not show up in
        // GetReferencedAssemblies(). The normative check is the csproj
        // ProjectReference graph, which is stable regardless of stub state.
        var apiCsproj = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "OpenDockify.Api", "OpenDockify.Api.csproj");
        var projectText = File.ReadAllText(Path.GetFullPath(apiCsproj));

        foreach (var module in _moduleAssemblyNames.Where(ns => ns != "OpenDockify.Api"))
        {
            Assert.True(
                projectText.Contains($"{module}.csproj", StringComparison.OrdinalIgnoreCase),
                $"OpenDockify.Api (composition root) must reference '{module}'.");
        }
    }
}
