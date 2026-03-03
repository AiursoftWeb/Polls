using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Vote
{
    [Key]
    public int Id { get; set; }

    public int OptionId { get; set; }

    [ForeignKey(nameof(OptionId))]
    public Option? Option { get; set; }

    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [MaxLength(50)]
    public string? IPAddress { get; set; }

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
