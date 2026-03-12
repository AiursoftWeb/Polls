using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class ExtendViewModel : UiStackLayoutViewModel
{
    public ExtendViewModel()
    {
        PageTitle = "Extend Poll Deadline";
    }

    public int PollId { get; set; }
    public string? PollTitle { get; set; }
    public DateTime CurrentDeadline { get; set; }

    [Required]
    [Display(Name = "New Deadline")]
    public DateTime NewDeadline { get; set; }
}
