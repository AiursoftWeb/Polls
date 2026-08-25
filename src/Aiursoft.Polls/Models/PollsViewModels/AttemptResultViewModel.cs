using Aiursoft.Polls.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class AttemptResultViewModel : UiStackLayoutViewModel
{
    public AttemptResultViewModel() => PageTitle = "Exam Result";
    public required Poll Poll { get; set; }
    public required Submission Attempt { get; set; }
    public List<AttemptQuestionResult> Questions { get; set; } = [];
}

public class AttemptQuestionResult
{
    public required AttemptQuestion Question { get; set; }
    public List<int> SelectedOptionIds { get; set; } = [];
    public decimal Score { get; set; }
    public bool IsFullyCorrect { get; set; }
}

public class SaveAttemptAnswerRequest
{
    public int AttemptId { get; set; }
    public int AttemptQuestionId { get; set; }
    public IReadOnlyList<int> OptionIds { get; set; } = [];
}
