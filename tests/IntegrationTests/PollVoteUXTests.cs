using System.Net;
using Aiursoft.Polls.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class PollVoteUXTests : TestBase
{
    [TestMethod]
    public async Task AuthoringPagesUseTheOrganizedExamInterface()
    {
        await LoginAsAdmin();

        var createResponse = await Http.GetAsync("/Polls/Create");
        Assert.AreEqual(HttpStatusCode.OK, createResponse.StatusCode);
        var createHtml = await createResponse.Content.ReadAsStringAsync();
        Assert.Contains("exam-ui", createHtml);
        Assert.Contains("Advanced options", createHtml);
        Assert.Contains("alert-outline-coloured", createHtml);

        var pollId = await CreatePollWithQuestion("Authoring experience", "A clear operational question");
        var detailsResponse = await Http.GetAsync($"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, detailsResponse.StatusCode);
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Advanced scoring and randomization", detailsHtml);
        Assert.Contains("dropdown-menu", detailsHtml);
        Assert.Contains("Edit question", detailsHtml);
        Assert.DoesNotContain("badge rounded-pill", detailsHtml);

        await PublishPoll(pollId);
        var publishedDetailsResponse = await Http.GetAsync($"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, publishedDetailsResponse.StatusCode);
        var publishedDetailsHtml = await publishedDetailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Edit question", publishedDetailsHtml,
            "Editors should be able to correct a question without taking the exam back to draft.");
        Assert.Contains("Add question", publishedDetailsHtml,
            "Editors should be able to expand the question set for future attempts after publishing.");
        Assert.Contains("exam-admin-action", publishedDetailsHtml);
        Assert.Contains($"/Polls/Extend/{pollId}", publishedDetailsHtml);
        Assert.Contains("exam-danger-panel", publishedDetailsHtml);
        Assert.Contains("typeof window.Swal", publishedDetailsHtml,
            "Destructive actions need a native confirmation fallback when SweetAlert cannot load.");

        var addQuestionResponse = await Http.GetAsync($"/Polls/AddQuestion/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, addQuestionResponse.StatusCode);
        var addQuestionHtml = await addQuestionResponse.Content.ReadAsStringAsync();
        Assert.Contains("Question builder", addQuestionHtml);
        Assert.AreEqual(5, System.Text.RegularExpressions.Regex.Matches(addQuestionHtml, "class=\"exam-option-editor option-row\"").Count,
            "The authoring page should render four initial options plus one client-side template row.");
    }

    [TestMethod]
    public async Task InvalidPublishReturnsToDetailsWithAnActionableAlert()
    {
        await LoginAsAdmin();
        var pollId = await CreatePollWithQuestion("Incomplete exam", "Only question");

        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var poll = await dbContext.Polls.FindAsync(pollId);
            Assert.IsNotNull(poll);
            poll.QuestionsPerAttempt = 5;
            await dbContext.SaveChangesAsync();
        }

        var publishResponse = await PostForm($"/Polls/Publish/{pollId}", new Dictionary<string, string>());
        AssertRedirect(publishResponse, $"/Polls/Details/{pollId}");

        var detailsResponse = await Http.GetAsync($"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, detailsResponse.StatusCode);
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Unable to publish exam", detailsHtml);
        Assert.Contains("currently contains only 1", detailsHtml);
        Assert.Contains("Review settings", detailsHtml);
    }

    [TestMethod]
    public async Task CompletedExamCanBeTakenAgainAsANewAttempt()
    {
        // 1. Login as Admin
        await LoginAsAdmin();

        // 2. Create a Poll with a question
        var pollId = await CreatePollWithQuestion("Test Poll", "Question 1");

        // 3. Publish the Poll
        await PublishPoll(pollId);

        // Start the first randomized attempt and read its snapshot IDs.
        var startResponse = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, startResponse.StatusCode);
        var startHtml = await startResponse.Content.ReadAsStringAsync();
        Assert.Contains("/UIStack/dist/css/app.css", startHtml);
        Assert.Contains("exam-number-grid", startHtml);
        Assert.Contains("answeredCount", startHtml);
        Assert.Contains("timeProgress", startHtml);
        Assert.Contains("SaveAttemptAnswer", startHtml);
        int attemptId;
        int attemptQuestionId;
        int attemptOptionId;
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var attempt = await dbContext.Submissions
                .Include(s => s.AttemptQuestions!).ThenInclude(q => q.Options)
                .SingleAsync(s => s.PollId == pollId && s.Status == AttemptStatus.InProgress);
            attemptId = attempt.Id;
            var question = attempt.AttemptQuestions!.Single();
            attemptQuestionId = question.Id;
            attemptOptionId = question.Options!.First(o => o.IsCorrect).Id;
        }

        // 4. Submit the exam.
        var submitResponse = await PostForm($"/Polls/Vote", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "AttemptId", attemptId.ToString() },
            { $"SelectedOptions[{attemptQuestionId}]", attemptOptionId.ToString() }
        }, tokenUrl: $"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, submitResponse.StatusCode);

        // 5. Re-entering starts a second attempt instead of overwriting the first.
        var voteResponse = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, voteResponse.StatusCode);
        var voteHtml = await voteResponse.Content.ReadAsStringAsync();
        Assert.Contains("Attempt #2", voteHtml);
        Assert.Contains("Answers are saved automatically.", voteHtml);
    }

    [TestMethod]
    public async Task ExamCanBeSubmittedWithAnUnansweredQuestion()
    {
        await LoginAsAdmin();
        var pollId = await CreatePollWithQuestion("Partially answered exam", "Answered question");
        await PostForm("/Polls/AddQuestion", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "Title", "Unanswered question" },
            { "Type", ((int)QuestionType.SingleChoice).ToString() },
            { "IsRequired", "false" },
            { "Options[0].Content", "Correct option" },
            { "Options[0].IsCorrect", "true" },
            { "Options[1].Content", "Incorrect option" }
        });
        await PublishPoll(pollId);

        var startResponse = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, startResponse.StatusCode);

        int attemptId;
        int answeredQuestionId;
        int answeredOptionId;
        int unansweredQuestionId;
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var attempt = await dbContext.Submissions
                .Include(s => s.AttemptQuestions!).ThenInclude(q => q.Options)
                .SingleAsync(s => s.PollId == pollId && s.Status == AttemptStatus.InProgress);
            attemptId = attempt.Id;
            var attemptQuestions = attempt.AttemptQuestions!;
            var answeredQuestion = attemptQuestions.Single(q => q.Title == "Answered question");
            answeredQuestionId = answeredQuestion.Id;
            answeredOptionId = answeredQuestion.Options!.Single(o => o.IsCorrect).Id;
            unansweredQuestionId = attemptQuestions.Single(q => q.Title == "Unanswered question").Id;
        }

        var submitResponse = await PostForm("/Polls/Vote", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "AttemptId", attemptId.ToString() },
            { $"SelectedOptions[{answeredQuestionId}]", answeredOptionId.ToString() },
            // Browsers submit the hidden field for an unanswered question as an empty value.
            { $"SelectedOptions[{unansweredQuestionId}]", string.Empty }
        }, tokenUrl: $"/Polls/Vote/{pollId}");

        Assert.AreEqual(HttpStatusCode.Found, submitResponse.StatusCode);
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var attempt = await dbContext.Submissions.SingleAsync(s => s.Id == attemptId);
            Assert.AreEqual(AttemptStatus.Submitted, attempt.Status);
        }
    }

    [TestMethod]
    public async Task TerminatedExamCanBeReopenedWithANewDeadline()
    {
        await LoginAsAdmin();
        var pollId = await CreatePollWithQuestion("Reopenable exam", "Operational question");
        await PublishPoll(pollId);
        var terminateResponse = await PostForm("/Polls/Terminate", new Dictionary<string, string>
        {
            { "id", pollId.ToString() }
        }, tokenUrl: $"/Polls/Details/{pollId}");
        AssertRedirect(terminateResponse, $"/Polls/Details/{pollId}");

        var terminatedDetails = await Http.GetAsync($"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, terminatedDetails.StatusCode);
        Assert.Contains("Reopen exam", await terminatedDetails.Content.ReadAsStringAsync());

        var reopenPage = await Http.GetAsync($"/Polls/Extend/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, reopenPage.StatusCode);
        Assert.Contains("Reopen exam", await reopenPage.Content.ReadAsStringAsync());

        var reopenResponse = await PostForm("/Polls/Extend", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "NewDeadline", DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-ddTHH:mm:ss") }
        }, tokenUrl: $"/Polls/Extend/{pollId}");
        AssertRedirect(reopenResponse, $"/Polls/Details/{pollId}");

        using var scope = Server!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        var poll = await dbContext.Polls.SingleAsync(p => p.Id == pollId);
        Assert.AreEqual(PollState.Published, poll.State);
        Assert.IsGreaterThan(DateTime.UtcNow, poll.Deadline);
    }

    [TestMethod]
    public async Task TestInactivePollMessage()
    {
        // 1. Create a Poll (Draft by default)
        await LoginAsAdmin();
        var pollId = await CreatePollWithQuestion("Draft Poll", "Question 1");

        // 2. Try to vote - SHOULD SHOW MESSAGE (OK status code but content should have the message)
        var voteResponse = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, voteResponse.StatusCode);
        var content = await voteResponse.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("This poll is not yet open."), "Should show 'not yet open' message for draft polls");
    }

    private async Task<Guid> CreatePollWithQuestion(string title, string qTitle)
    {
        await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", title },
            { "Description", "Testing UX" },
            { "AccessType", ((int)AccessType.Public).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "PassingScore", "4" },
            { "Deadline", DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss") }
        });
        
        Guid pollId;
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            pollId = (await dbContext.Polls.OrderByDescending(p => p.CreationTime).FirstAsync()).Id;
        }

        await PostForm("/Polls/AddQuestion", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "Title", qTitle },
            { "Type", ((int)QuestionType.SingleChoice).ToString() },
            { "IsRequired", "true" },
            { "Options[0].Content", "Option 1" },
            { "Options[0].IsCorrect", "true" },
            { "Options[1].Content", "Option 2" }
        });

        return pollId;
    }

    private async Task PublishPoll(Guid pollId)
    {
        await PostForm($"/Polls/Publish/{pollId}", new Dictionary<string, string>());
    }
}
