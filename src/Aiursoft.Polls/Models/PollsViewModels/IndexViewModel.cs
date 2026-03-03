using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Polls Dashboard";
    }

    public required List<Poll> ToDoPolls { get; set; }
    public required List<Poll> HistoryPolls { get; set; }
    public required List<Poll> ManagedPolls { get; set; }
}
