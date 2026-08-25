using OpenDockify.Interviews.Models;
using OpenDockify.Interviews.Services;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class InterviewDefinitionCompilerTests
{
    [Fact]
    public void Validate_accepts_deterministic_conditional_flow()
    {
        var definition = ValidDefinition();

        var error = InterviewDefinitionCompiler.Validate(definition);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_rejects_cycle()
    {
        var definition = ValidDefinition();
        definition.Interview!.Steps[1].NextStepId = "identity";

        var error = InterviewDefinitionCompiler.Validate(definition);

        Assert.Contains("cycle", error);
    }

    [Fact]
    public void Validate_rejects_unknown_condition_field()
    {
        var definition = ValidDefinition();
        definition.Interview!.Steps[1].Condition!.Field = "script";

        var error = InterviewDefinitionCompiler.Validate(definition);

        Assert.Contains("unknown field 'script'", error);
    }

    [Fact]
    public void Validate_rejects_unreachable_required_field()
    {
        var definition = ValidDefinition();
        definition.Interview!.Steps[0].NextStepId = null;

        var error = InterviewDefinitionCompiler.Validate(definition);

        Assert.Contains("unreachable", error);
    }

    [Fact]
    public void Validate_rejects_unsupported_operator()
    {
        var definition = ValidDefinition();
        definition.Interview!.Steps[1].Condition!.Operator = "execute";

        var error = InterviewDefinitionCompiler.Validate(definition);

        Assert.Contains("unsupported operator", error);
    }

    internal static TemplateDefinition ValidDefinition()
    {
        return new()
        {
            Fields =
        {
            new FieldDefinition { Name = "kind", Label = "Kind", Type = FieldType.Text, Required = true },
            new FieldDefinition { Name = "company", Label = "Company", Type = FieldType.Text, Required = true },
        },
            Interview = new InterviewDefinition
            {
                StartStepId = "identity",
                Steps =
            {
                new InterviewStepDefinition { Id = "identity", Title = "Identity", Fields = ["kind"], NextStepId = "company" },
                new InterviewStepDefinition
                {
                    Id = "company",
                    Title = "Company",
                    Fields = ["company"],
                    Condition = new InterviewCondition { Operator = "equals", Field = "kind", Value = "business" },
                },
            },
            },
        };
    }
}

public sealed class InterviewFlowTests
{
    [Fact]
    public void Branch_change_removes_hidden_answers()
    {
        var interview = InterviewDefinitionCompilerTests.ValidDefinition().Interview!;
        var answers = new Dictionary<string, string>
        {
            ["kind"] = "person",
            ["company"] = "Must be removed",
        };

        var effective = InterviewFlow.RemoveHiddenAnswers(interview, answers);

        Assert.Equal("person", effective["kind"]);
        Assert.DoesNotContain("company", effective);
    }

    [Theory]
    [InlineData("", "Field 'amount' is required.")]
    [InlineData("abc", "Field 'amount' must be a number.")]
    [InlineData("-1", "Field 'amount' must not be negative.")]
    [InlineData("12.50", null)]
    public void ValidateValue_applies_field_rules(string value, string? expected)
    {
        var field = new FieldDefinition
        {
            Name = "amount",
            Label = "Amount",
            Type = FieldType.Currency,
            Required = true,
            Validation = new ValidationRule { NonNegative = true },
        };

        Assert.Equal(expected, InterviewFlow.ValidateValue(field, value));
    }
}

public sealed class InterviewSessionPolicyTests
{
    private static readonly Guid _ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Session_is_visible_only_to_owner_before_expiry()
    {
        var now = new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
        var session = Session(now.AddHours(1));

        Assert.True(InterviewSessionPolicy.CanAccess(session, _ownerId, now));
        Assert.False(InterviewSessionPolicy.CanAccess(session, Guid.NewGuid(), now));
        Assert.False(InterviewSessionPolicy.CanAccess(session, _ownerId, now.AddHours(1)));
    }

    [Fact]
    public void Template_edit_conflicts_with_pinned_session_revision()
    {
        var revision = new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
        var session = Session(DateTimeOffset.MaxValue);
        session.TemplateRevisionStamp = revision;

        Assert.True(InterviewSessionPolicy.MatchesTemplateRevision(session, revision));
        Assert.False(InterviewSessionPolicy.MatchesTemplateRevision(session, revision.AddTicks(1)));
    }

    private static InterviewSession Session(DateTimeOffset expiresAt)
    {
        return new()
        {
            OwnerId = _ownerId,
            ExpiresAt = expiresAt,
        };
    }
}
