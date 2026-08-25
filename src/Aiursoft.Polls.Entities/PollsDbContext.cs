using System.Diagnostics.CodeAnalysis;
using Aiursoft.DbTools;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]

public abstract class TemplateDbContext(DbContextOptions options) : IdentityDbContext<User>(options), ICanMigrate
{
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Option> Options => Set<Option>();
    public DbSet<PollRoleRestriction> PollRoleRestrictions => Set<PollRoleRestriction>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<PollOperationLog> PollOperationLogs => Set<PollOperationLog>();
    public DbSet<PollAssignment> PollAssignments => Set<PollAssignment>();
    public DbSet<PollShare> PollShares => Set<PollShare>();
    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();
    public DbSet<AttemptOption> AttemptOptions => Set<AttemptOption>();
    public DbSet<AttemptSelection> AttemptSelections => Set<AttemptSelection>();

    public virtual  Task MigrateAsync(CancellationToken cancellationToken) =>
        Database.MigrateAsync(cancellationToken);

    public virtual  Task<bool> CanConnectAsync() =>
        Database.CanConnectAsync();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Poll>()
            .HasOne(p => p.CreatedBy)
            .WithMany()
            .HasForeignKey(p => p.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Submission>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<PollAssignment>(entity =>
        {
            entity.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssignedRole).WithMany().HasForeignKey(x => x.AssignedRoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.PollId, x.AssignedUserId }).IsUnique();
            entity.HasIndex(x => new { x.PollId, x.AssignedRoleId }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_PollAssignments_ExactlyOneRecipient",
                "(AssignedUserId IS NOT NULL AND AssignedRoleId IS NULL) OR (AssignedUserId IS NULL AND AssignedRoleId IS NOT NULL)"));
        });

        builder.Entity<PollShare>(entity =>
        {
            entity.HasOne(x => x.SharedWithUser).WithMany().HasForeignKey(x => x.SharedWithUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SharedWithRole).WithMany().HasForeignKey(x => x.SharedWithRoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.PollId, x.SharedWithUserId }).IsUnique();
            entity.HasIndex(x => new { x.PollId, x.SharedWithRoleId }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_PollShares_ExactlyOneRecipient",
                "(SharedWithUserId IS NOT NULL AND SharedWithRoleId IS NULL) OR (SharedWithUserId IS NULL AND SharedWithRoleId IS NOT NULL)"));
        });

        builder.Entity<AttemptSelection>()
            .HasIndex(x => new { x.SubmissionId, x.AttemptQuestionId, x.AttemptOptionId })
            .IsUnique();
    }
}
