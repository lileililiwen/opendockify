using Microsoft.EntityFrameworkCore;

namespace OpenDockify.Data;

/// <summary>
/// Central EF Core DbContext for the OpenDockify modular monolith.
/// Each domain module contributes entities through its own
/// <c>IEntityTypeConfiguration&lt;T&gt;</c>; when a module lands, its assembly
/// gets ONE new <c>ApplyConfigurationsFromAssembly</c> line below (plus its
/// DbSet), never any other edit to this file.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OpenDockify.Auth.Models.User> Users => Set<OpenDockify.Auth.Models.User>();

    public DbSet<OpenDockify.SystemConfig.Models.Setting> Settings => Set<OpenDockify.SystemConfig.Models.Setting>();

    public DbSet<OpenDockify.Templates.Models.Template> Templates => Set<OpenDockify.Templates.Models.Template>();

    public DbSet<OpenDockify.Generation.Models.Document> Documents => Set<OpenDockify.Generation.Models.Document>();

    public DbSet<OpenDockify.Interviews.Models.InterviewSession> InterviewSessions => Set<OpenDockify.Interviews.Models.InterviewSession>();

    public DbSet<OpenDockify.AiAssist.Models.AiUsageLog> AiUsageLogs => Set<OpenDockify.AiAssist.Models.AiUsageLog>();

    public DbSet<OpenDockify.Esign.Models.Signer> Signers => Set<OpenDockify.Esign.Models.Signer>();

    public DbSet<OpenDockify.Esign.Models.SigningAuditLog> SigningAuditLogs => Set<OpenDockify.Esign.Models.SigningAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // One line per module assembly (see class doc). Added by user-auth.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenDockify.Auth.Models.User).Assembly);
        // Added by system-config.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenDockify.SystemConfig.Models.Setting).Assembly);
        // Added by template-engine.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenDockify.Templates.Models.Template).Assembly);
        // Added by document-generation.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenDockify.Generation.Models.Document).Assembly);
        // Added by guided-interview-workflows.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenDockify.Interviews.Models.InterviewSession).Assembly);
        // Added by ai-assist.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenDockify.AiAssist.Models.AiUsageLog).Assembly);
        // Added by esign-extensions.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenDockify.Esign.Models.Signer).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
