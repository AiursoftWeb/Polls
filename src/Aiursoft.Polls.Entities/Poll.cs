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

    public bool IsAnonymous { get; set; }

    /// <summary>Number of questions sampled for every attempt. Zero means all questions.</summary>
    public int QuestionsPerAttempt { get; set; }

    public bool ShuffleQuestions { get; set; } = true;

    public bool ShuffleOptions { get; set; } = true;

    public bool AllowRepeatedSubmissions { get; set; } = true;

    /// <summary>Time available for one attempt.</summary>
    public int DurationMinutes { get; set; } = 60;

    [Column(TypeName = "decimal(10,2)")]
    public decimal FullScore { get; set; } = 4;

    [Column(TypeName = "decimal(10,2)")]
    public decimal PartialScore { get; set; } = 2;

    [Column(TypeName = "decimal(10,2)")]
    public decimal OverSelectionScore { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal PassingScore { get; set; } = 90;

    [MaxLength(2000)]
    public string PassMessage { get; set; } = "You passed the exam.";

    [MaxLength(2000)]
    public string FailMessage { get; set; } = "Unfortunately, you did not pass the exam.";

    public DateTime Deadline { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    public DateTime? UpdatedTime { get; set; }

    public bool IsDeleted { get; set; }

    public string? CreatedById { get; set; }

    [ForeignKey(nameof(CreatedById))]
    public User? CreatedBy { get; set; }

    public ICollection<Question>? Questions { get; set; }
    public ICollection<PollRoleRestriction>? RoleRestrictions { get; set; }
    public ICollection<PollAssignment>? Assignments { get; set; }
    public ICollection<PollShare>? Shares { get; set; }
    public ICollection<Submission>? Submissions { get; set; }
    public ICollection<PollOperationLog>? OperationLogs { get; set; }
}
