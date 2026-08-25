using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class VoteViewModel : UiStackLayoutViewModel
{
    public VoteViewModel()
    {
        PageTitle = "Take Exam";
    }

    public Guid PollId { get; set; }
    public Poll? Poll { get; set; }
    public int AttemptId { get; set; }
    public Submission? Attempt { get; set; }

    /// <summary>
    /// Key: QuestionId, Value: list of selected option IDs (comma separated for multi-choice)
    /// </summary>
    public Dictionary<int, string> SelectedOptions { get; set; } = [];

    /// <summary>
    /// Key: QuestionId, Value: custom text (for TextResponse or AllowCustomText options)
    /// </summary>
    public Dictionary<int, string> CustomTexts { get; set; } = [];

    /// <summary>
    /// Browser fingerprint for anonymous submissions
    /// </summary>
    public string? BrowserFingerprint { get; set; }
}
