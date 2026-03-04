using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Poll
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    public string? Content { get; set; }

    public bool IsAnonymous { get; set; }
    public bool IsPublic { get; set; }
    public bool IsTemplate { get; set; }
    
    public PollState State { get; set; } = PollState.Draft;

    public DateTime Deadline { get; set; }
    
    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    public string? CreatedById { get; set; }

    [ForeignKey(nameof(CreatedById))]
    public User? CreatedBy { get; set; }

    public ICollection<Question>? Questions { get; set; }
    public ICollection<PollRoleRestriction>? RoleRestrictions { get; set; }
    public ICollection<PollUserRestriction>? UserRestrictions { get; set; }
}
