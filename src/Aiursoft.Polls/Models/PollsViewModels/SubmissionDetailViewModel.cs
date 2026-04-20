using Aiursoft.Polls.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class SubmissionDetailViewModel : UiStackLayoutViewModel
{
    public Poll? Poll { get; set; }
    public Submission? Submission { get; set; }
    public List<Question> Questions { get; set; } = [];
}
