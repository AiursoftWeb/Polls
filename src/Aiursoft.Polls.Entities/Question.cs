using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Question
{
    [Key]
    public int Id { get; set; }

    public Guid PollId { get; set; }

    [ForeignKey(nameof(PollId))]
    public Poll? Poll { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    [MaxLength(4000)]
    public string? Explanation { get; set; }

    public QuestionType Type { get; set; }

    public bool IsRequired { get; set; } = true;

    public int Order { get; set; }

    public ICollection<Option>? Options { get; set; }
}
