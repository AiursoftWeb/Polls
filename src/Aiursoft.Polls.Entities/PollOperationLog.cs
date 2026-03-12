using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class PollOperationLog
{
    [Key]
    public int Id { get; set; }

    public int PollId { get; set; }

    [ForeignKey(nameof(PollId))]
    public Poll? Poll { get; set; }

    [Required]
    public required string OperatorId { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Action { get; set; }

    [MaxLength(1000)]
    public string? Details { get; set; }

    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
