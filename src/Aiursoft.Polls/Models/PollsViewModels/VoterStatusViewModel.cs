using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class VoterStatusViewModel : UiStackLayoutViewModel
{
    public VoterStatusViewModel()
    {
        PageTitle = "Voter Status";
    }

    public Poll? Poll { get; set; }
    public List<UserStatus> Users { get; set; } = [];
}

public class UserStatus
{
    public User? User { get; set; }
    public bool HasVoted { get; set; }
    public DateTime? VoteTime { get; set; }
}
