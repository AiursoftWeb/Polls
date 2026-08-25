using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Services;

namespace Aiursoft.Polls.Tests;

[TestClass]
public class ExamScoringTests
{
    private static (Submission attempt, AttemptQuestion question, AttemptOption a, AttemptOption b, AttemptOption c, AttemptOption d) BuildQuestion()
    {
        var attempt = new Submission
        {
            FullScore = 4,
            PartialScore = 2,
            OverSelectionScore = 0
        };
        var question = new AttemptQuestion
        {
            Title = "Select the correct options",
            Options =
            [
                new AttemptOption { Id = 1, Content = "A", IsCorrect = true },
                new AttemptOption { Id = 2, Content = "B", IsCorrect = true },
                new AttemptOption { Id = 3, Content = "C", IsCorrect = true },
                new AttemptOption { Id = 4, Content = "D", IsCorrect = false }
            ]
        };
        var options = question.Options.ToArray();
        return (attempt, question, options[0], options[1], options[2], options[3]);
    }

    [TestMethod]
    public void AnyNonEmptyCorrectSubsetGetsTheSamePartialScore()
    {
        var (attempt, question, a, b, _, _) = BuildQuestion();
        Assert.AreEqual(2m, ExamAttemptService.ScoreQuestion(attempt, question, [a.Id]));
        Assert.AreEqual(2m, ExamAttemptService.ScoreQuestion(attempt, question, [a.Id, b.Id]));
    }

    [TestMethod]
    public void ExactCorrectSetGetsFullScore()
    {
        var (attempt, question, a, b, c, _) = BuildQuestion();
        Assert.AreEqual(4m, ExamAttemptService.ScoreQuestion(attempt, question, [a.Id, b.Id, c.Id]));
    }

    [TestMethod]
    public void AnyIncorrectSelectionUsesOverSelectionScore()
    {
        var (attempt, question, a, b, c, d) = BuildQuestion();
        Assert.AreEqual(0m, ExamAttemptService.ScoreQuestion(attempt, question, [a.Id, b.Id, c.Id, d.Id]));
        Assert.AreEqual(0m, ExamAttemptService.ScoreQuestion(attempt, question, [d.Id]));
    }

    [TestMethod]
    public void EmptySelectionGetsZero()
    {
        var (attempt, question, _, _, _, _) = BuildQuestion();
        Assert.AreEqual(0m, ExamAttemptService.ScoreQuestion(attempt, question, []));
    }
}
