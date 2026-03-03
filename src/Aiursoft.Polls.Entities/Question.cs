using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Question
{
    [Key]
    public int Id { get; set; }

    public int PollId { get; set; }

    [ForeignKey(nameof(PollId))]
    public Poll? Poll { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    public QuestionType Type { get; set; }

    public int Order { get; set; }

    public ICollection<Option>? Options { get; set; }
}
