using Aiursoft.Polls.Entities;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class SubmissionsViewModel : UiStackLayoutViewModel
{
    public SubmissionsViewModel()
    {
        PageTitle = "Submissions";
    }

    public Poll? Poll { get; set; }
    public List<Submission> Submissions { get; set; } = [];
    public List<EmployeeAttemptSummary> EmployeeSummaries { get; set; } = [];
}

public class EmployeeAttemptSummary
{
    public User? User { get; set; }
    public int AttemptCount { get; set; }
    public decimal HighestScore { get; set; }
    public List<Submission> Attempts { get; set; } = [];
}
