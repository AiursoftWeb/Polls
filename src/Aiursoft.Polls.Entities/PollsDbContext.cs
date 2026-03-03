using System.Diagnostics.CodeAnalysis;
using Aiursoft.DbTools;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]

public abstract class TemplateDbContext(DbContextOptions options) : IdentityDbContext<User>(options), ICanMigrate
{
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Option> Options => Set<Option>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<PollRoleRestriction> PollRoleRestrictions => Set<PollRoleRestriction>();
    public DbSet<PollDepartmentRestriction> PollDepartmentRestrictions => Set<PollDepartmentRestriction>();
    public DbSet<PollUserRestriction> PollUserRestrictions => Set<PollUserRestriction>();

    public virtual  Task MigrateAsync(CancellationToken cancellationToken) =>
        Database.MigrateAsync(cancellationToken);

    public virtual  Task<bool> CanConnectAsync() =>
        Database.CanConnectAsync();
}
