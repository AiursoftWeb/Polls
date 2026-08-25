using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aiursoft.Polls.Entities;

public class AttemptOption
{
    [Key] public int Id { get; set; }
    public int AttemptQuestionId { get; set; }
    [ForeignKey(nameof(AttemptQuestionId))] public AttemptQuestion? AttemptQuestion { get; set; }
    public int SourceOptionId { get; set; }
    [MaxLength(500)] public required string Content { get; set; }
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}
