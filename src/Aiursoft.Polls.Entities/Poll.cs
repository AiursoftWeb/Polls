using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Poll
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsTemplate { get; set; }

    public PollState State { get; set; } = PollState.Draft;

    public AccessType AccessType { get; set; } = AccessType.RegisteredOnly;

    public ResultVisibility Visibility { get; set; } = ResultVisibility.CreatorOnly;

    public DateTime Deadline { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    public DateTime? UpdatedTime { get; set; }

    public bool IsDeleted { get; set; }

    public string? CreatedById { get; set; }

    [ForeignKey(nameof(CreatedById))]
    public User? CreatedBy { get; set; }

    public ICollection<Question>? Questions { get; set; }
    public ICollection<PollRoleRestriction>? RoleRestrictions { get; set; }
    public ICollection<Submission>? Submissions { get; set; }
    public ICollection<PollOperationLog>? OperationLogs { get; set; }
}
