using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class DeleteViewModel : UiStackLayoutViewModel
{
    public DeleteViewModel()
    {
        PageTitle = "Delete Poll";
    }

    public required Poll Poll { get; set; }
}
