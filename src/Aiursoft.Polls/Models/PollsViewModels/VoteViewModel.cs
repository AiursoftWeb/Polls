using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class VoteViewModel : UiStackLayoutViewModel
{
    public VoteViewModel()
    {
        PageTitle = "Submit Vote";
    }

    public int PollId { get; set; }
    public required Poll Poll { get; set; }

    [Required]
    public Dictionary<int, string> Answers { get; set; } = [];
}
