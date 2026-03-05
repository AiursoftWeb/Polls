using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class AddQuestionViewModel : UiStackLayoutViewModel
{
    public AddQuestionViewModel()
    {
        PageTitle = "Add Question to Poll";
    }

    public int PollId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Question Title")]
    [MaxLength(500, ErrorMessage = "The {0} must be at max {1} characters long.")]
    public string? Title { get; set; }

    [Display(Name = "Question Type")]
    public QuestionType Type { get; set; }

    public List<string> Options { get; set; } = [];
}
