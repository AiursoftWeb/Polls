using System.Net;
using Aiursoft.Polls.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class PollVoteUXTests : TestBase
{
    [TestMethod]
    public async Task TestAlreadySubmittedRedirect()
    {
        // 1. Login as Admin
        await LoginAsAdmin();

        // 2. Create a Poll with a question
        var pollId = await CreatePollWithQuestion("Test Poll", "Question 1");

        // 3. Publish the Poll
        await PublishPoll(pollId);

        // Get Question and Option IDs
        int questionId;
        int optionId;
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var question = await dbContext.Questions.Include(q => q.Options).FirstAsync(q => q.PollId == pollId);
            questionId = question.Id;
            optionId = question.Options!.First().Id;
        }

        // 4. Submit a vote
        var submitResponse = await PostForm($"/Polls/Vote", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { $"SelectedOptions[{questionId}]", optionId.ToString() }
        });
        Assert.AreEqual(HttpStatusCode.Found, submitResponse.StatusCode);

        // 5. Try to vote again - SHOULD REDIRECT TO RESULTS
        var voteResponse = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, voteResponse.StatusCode);
        Assert.IsTrue(voteResponse.Headers.Location?.OriginalString.Contains($"/Polls/Results/{pollId}"), "Should redirect to results if already submitted");
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
            { "Options[0].Content", "Option 1" }
        });

        return pollId;
    }

    private Guid ExtractGuid(string? url)
    {
        if (string.IsNullOrEmpty(url)) return Guid.Empty;
        
        // Try to find a GUID in the URL using Regex, but be more flexible
        var match = System.Text.RegularExpressions.Regex.Match(url, @"[a-fA-F0-9]{8}-?([a-fA-F0-9]{4}-?){3}[a-fA-F0-9]{12}");
        if (match.Success)
        {
            return Guid.Parse(match.Value);
        }

        // Fallback to splitting
        var parts = url.Split('/', '?', '&', '=');
        foreach (var part in parts)
        {
            if (Guid.TryParse(part, out var result))
            {
                return result;
            }
        }
        return Guid.Empty;
    }

    private async Task PublishPoll(Guid pollId)
    {
        await PostForm($"/Polls/Publish/{pollId}", new Dictionary<string, string>());
    }
}
