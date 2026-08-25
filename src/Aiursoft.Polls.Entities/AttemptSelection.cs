using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.Polls.Entities;

public class AttemptSelection
{
    [Key] public int Id { get; set; }
    public int SubmissionId { get; set; }
    [ForeignKey(nameof(SubmissionId))] public Submission? Submission { get; set; }
    public int AttemptQuestionId { get; set; }
    [ForeignKey(nameof(AttemptQuestionId))] public AttemptQuestion? AttemptQuestion { get; set; }
    public int AttemptOptionId { get; set; }
    [ForeignKey(nameof(AttemptOptionId))] public AttemptOption? AttemptOption { get; set; }
}
