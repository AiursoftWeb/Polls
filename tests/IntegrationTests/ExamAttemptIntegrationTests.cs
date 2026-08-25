using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Services;
using Aiursoft.Polls.Services.BackgroundJobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class ExamAttemptIntegrationTests : TestBase
{
    [TestMethod]
    public async Task AttemptSnapshotsScoresAndKeepsHistory()
    {
        await LoginAsAdmin();
        using var scope = Server!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var service = scope.ServiceProvider.GetRequiredService<ExamAttemptService>();
        var admin = await userManager.FindByEmailAsync("admin@default.com");
        Assert.IsNotNull(admin);

        var poll = new Poll
        {
            Title = "On-call license",
            CreatedById = admin.Id,
            State = PollState.Published,
            AccessType = AccessType.RegisteredOnly,
            Deadline = DateTime.UtcNow.AddDays(1),
            QuestionsPerAttempt = 2,
            ShuffleQuestions = true,
            ShuffleOptions = true,
            DurationMinutes = 30,
            FullScore = 4,
            PartialScore = 2,
            OverSelectionScore = 0,
            PassingScore = 6,
            PassMessage = "Qualified for on-call duty.",
            FailMessage = "Training is required before retrying."
        };
        context.Polls.Add(poll);
        for (var i = 0; i < 3; i++)
        {
            context.Questions.Add(new Question
            {
                Poll = poll,
                PollId = poll.Id,
                Title = $"Question {i}",
                Explanation = $"Explanation {i}",
                Type = QuestionType.MultipleChoice,
                Order = i,
                Options =
                [
                    new Option { Content = "A", IsCorrect = true },
                    new Option { Content = "B", IsCorrect = true },
                    new Option { Content = "C", IsCorrect = false }
                ]
            });
        }
        await context.SaveChangesAsync();

        var first = await service.StartAttemptAsync(poll, admin, "127.0.0.1", null);
        Assert.AreEqual(2, first.AttemptQuestions?.Count);
        Assert.AreEqual(8m, first.MaxScore);
        Assert.AreEqual(1, first.AttemptNumber);
        Assert.IsTrue(first.ExpiresAt > first.StartedAt);

        var snapshots = first.AttemptQuestions!.OrderBy(q => q.DisplayOrder).ToList();
        var q1Correct = snapshots[0].Options!.Where(o => o.IsCorrect).ToList();
        var q2Correct = snapshots[1].Options!.Where(o => o.IsCorrect).ToList();
        await service.SaveSelectionsAsync(first, snapshots[0].Id, [q1Correct[0].Id]);
        await service.SaveSelectionsAsync(first, snapshots[1].Id, q2Correct.Select(o => o.Id));
        await service.FinalizeAsync(first);

        var completed = await service.GetAttemptAsync(first.Id);
        Assert.IsNotNull(completed);
        Assert.AreEqual(6m, completed.Score);
        Assert.IsTrue(completed.Passed);
        Assert.AreEqual(AttemptStatus.Submitted, completed.Status);

        var sourceQuestion = await context.Questions.FindAsync(snapshots[0].SourceQuestionId);
        sourceQuestion!.Title = "Changed after submission";
        await context.SaveChangesAsync();
        Assert.AreNotEqual(sourceQuestion.Title, snapshots[0].Title, "Attempt history must use its immutable snapshot.");

        var second = await service.StartAttemptAsync(poll, admin, "127.0.0.1", null);
        Assert.AreEqual(2, second.AttemptNumber);
        Assert.AreNotEqual(first.Id, second.Id);
        foreach (var question in second.AttemptQuestions ?? [])
            await service.SaveSelectionsAsync(second, question.Id, question.Options!.Where(o => o.IsCorrect).Select(o => o.Id));
        await service.FinalizeAsync(second);
        Assert.AreEqual(8m, (await service.GetAttemptAsync(second.Id))!.Score);
        Assert.AreEqual(2, await context.Submissions.CountAsync(s => s.PollId == poll.Id && s.UserId == admin.Id));

        var resultResponse = await Http.GetAsync($"/Polls/AttemptResult/{first.Id}");
        var resultHtml = await resultResponse.Content.ReadAsStringAsync();
        Assert.Contains("exam-score-orb", resultHtml);
        Assert.Contains("alert-outline-coloured", resultHtml);
        Assert.Contains("Qualified for on-call duty.", resultHtml);
        Assert.Contains(snapshots[0].Explanation!, resultHtml, "Partial answers must show their explanation.");
        Assert.DoesNotContain(snapshots[1].Explanation!, resultHtml, "Fully correct answers must not reveal their explanation.");

        var historyResponse = await Http.GetAsync($"/Polls/Submissions/{poll.Id}");
        var historyHtml = await historyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Employee performance", historyHtml);
        Assert.Contains("Detailed attempt log", historyHtml);
        Assert.Contains("6 &#x2192; 8", historyHtml);

        var expired = await service.StartAttemptAsync(poll, admin, "127.0.0.1", null);
        expired.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();
        var finalizer = scope.ServiceProvider.GetRequiredService<ExpiredExamAttemptJob>();
        await finalizer.ExecuteAsync();
        Assert.AreEqual(AttemptStatus.Expired, (await service.GetAttemptAsync(expired.Id))!.Status);
    }
}
