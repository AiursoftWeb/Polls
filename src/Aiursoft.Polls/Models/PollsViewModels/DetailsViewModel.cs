using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class DetailsViewModel : UiStackLayoutViewModel
{
    public DetailsViewModel()
    {
        PageTitle = "Poll Details";
    }

    public required Poll Poll { get; set; }
    
    // Will be true if the current user has already voted on this poll.
    public bool HasVoted { get; set; }
    // If the user has voted, this holds their vote details.
    public List<Vote>? UserVotes { get; set; }
}
