using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class AddOptionViewModel : UiStackLayoutViewModel
{
    public AddOptionViewModel()
    {
        PageTitle = "Add Option to Question";
    }

    public int QuestionId { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Option Content")]
    [MaxLength(500, ErrorMessage = "The {0} must be at max {1} characters long.")]
    public string? Content { get; set; }
}
