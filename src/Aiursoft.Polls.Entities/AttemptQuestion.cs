using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.Polls.Entities;

/// <summary>A stable snapshot of a question used in one exam attempt.</summary>
public class AttemptQuestion
{
    [Key] public int Id { get; set; }
    public int SubmissionId { get; set; }
    [ForeignKey(nameof(SubmissionId))] public Submission? Submission { get; set; }
    public int SourceQuestionId { get; set; }
    [MaxLength(500)] public required string Title { get; set; }
    [MaxLength(4000)] public string? Explanation { get; set; }
    public QuestionType Type { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public ICollection<AttemptOption>? Options { get; set; }
}
