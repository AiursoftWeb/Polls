using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class PollMessageViewModel : UiStackLayoutViewModel
{
    public required string Message { get; set; }
    public required string SubMessage { get; set; }
    public string Icon { get; set; } = "info";
    public string IconColor { get; set; } = "text-info";
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }
}
