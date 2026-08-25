using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Submission
{
    [Key]
    public int Id { get; set; }

    public Guid PollId { get; set; }

    [ForeignKey(nameof(PollId))]
    public Poll? Poll { get; set; }

    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(200)]
    public string? BrowserFingerprint { get; set; }

    public int AttemptNumber { get; set; } = 1;

    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;

    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Score { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MaxScore { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal FullScore { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal PartialScore { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal OverSelectionScore { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal PassingScore { get; set; }

    public bool Passed { get; set; }

    public DateTime SubmitTime { get; set; } = DateTime.UtcNow;

    public ICollection<Answer>? Answers { get; set; }
    public ICollection<AttemptQuestion>? AttemptQuestions { get; set; }
    public ICollection<AttemptSelection>? AttemptSelections { get; set; }
}
