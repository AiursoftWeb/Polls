using System.ComponentModel.DataAnnotations;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class QuestionOptionViewModel
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;
}
