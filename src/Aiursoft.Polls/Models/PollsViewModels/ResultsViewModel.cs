using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class ResultsViewModel : UiStackLayoutViewModel
{
    public ResultsViewModel()
    {
        PageTitle = "Poll Results";
    }

    public required Poll Poll { get; set; }
    public int TotalSubmissions { get; set; }
    public List<QuestionResultViewModel> QuestionResults { get; set; } = [];
    public bool CanExport { get; set; }
}

public class QuestionResultViewModel
{
    public required Question Question { get; set; }
    public List<OptionResultViewModel> OptionResults { get; set; } = [];
    public List<string> TextResponses { get; set; } = [];
    public int TotalAnswers { get; set; }
}

public class OptionResultViewModel
{
    public required Option Option { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
    public List<string> CustomTexts { get; set; } = [];
}
