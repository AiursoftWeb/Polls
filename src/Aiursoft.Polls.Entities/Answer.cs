using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class Answer
{
    [Key]
    public int Id { get; set; }

    public int SubmissionId { get; set; }

    [ForeignKey(nameof(SubmissionId))]
    public Submission? Submission { get; set; }

    public int QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public Question? Question { get; set; }

    public int? OptionId { get; set; }

    [ForeignKey(nameof(OptionId))]
    public Option? Option { get; set; }

    [MaxLength(2000)]
    public string? CustomText { get; set; }
}
