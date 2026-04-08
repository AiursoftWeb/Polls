using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.DashboardViewModels;

public class IndexViewModel : UiStackLayoutViewModel
{
    public IndexViewModel()
    {
        PageTitle = "Dashboard";
    }

    public List<Poll>? ToDoPolls { get; set; }
    public List<Poll>? ActiveAnonymousPolls { get; set; }
    public List<Poll>? HistoryPolls { get; set; }
}
