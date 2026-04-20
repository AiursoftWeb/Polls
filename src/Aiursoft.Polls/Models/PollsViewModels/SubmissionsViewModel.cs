using Aiursoft.Polls.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class SubmissionsViewModel : UiStackLayoutViewModel
{
    public Poll? Poll { get; set; }
    public List<Submission> Submissions { get; set; } = [];
}
