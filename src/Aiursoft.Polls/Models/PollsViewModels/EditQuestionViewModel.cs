using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class EditQuestionViewModel : UiStackLayoutViewModel
{
    public EditQuestionViewModel()
    {
        PageTitle = "Edit Question";
    }

    public int Id { get; set; }
    public int PollId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Question Title")]
    [MaxLength(500, ErrorMessage = "The {0} must be at max {1} characters long.")]
    public string? Title { get; set; }

    [Display(Name = "Question Type")]
    public QuestionType Type { get; set; }

    public List<QuestionOptionViewModel> Options { get; set; } = [];
}
