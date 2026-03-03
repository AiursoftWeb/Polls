using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Option
{
    [Key]
    public int Id { get; set; }

    public int QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public Question? Question { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Content { get; set; }

    public ICollection<Vote>? Votes { get; set; }
}
