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
    public bool HasSubmitted { get; set; }
    public Submission? UserSubmission { get; set; }
    public bool IsCreator { get; set; }
    public bool CanManage { get; set; }
}
