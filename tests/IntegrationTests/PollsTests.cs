using System.Net;
using Aiursoft.Polls.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class PollsTests : TestBase
{
    [TestMethod]
    public async Task DeadlineAndExtensionStoreUtcInstants()
    {
        await LoginAsAdmin();
        var response = await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", "UTC deadline" }, { "Deadline", "2030-07-01T09:30:00+08:00" }
        });
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        var id = Guid.Parse(response.Headers.Location!.OriginalString.Split('/').Last());
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        var poll = await db.Polls.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.AreEqual(new DateTime(2030, 7, 1, 1, 30, 0, DateTimeKind.Utc), poll.Deadline);
        var editHtml = await Http.GetStringAsync($"/Polls/Edit/{id}");
        Assert.Contains("data-utc-input", editHtml);
        Assert.Contains("utc-input.js", editHtml);
        response = await PostForm($"/Polls/Extend/{id}", new Dictionary<string, string>
        {
            { "PollId", id.ToString() }, { "NewDeadline", "2030-07-02T09:30:00-04:00" }
        });
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        poll = await db.Polls.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.AreEqual(new DateTime(2030, 7, 2, 13, 30, 0, DateTimeKind.Utc), poll.Deadline);
    }


    [TestMethod]
    public async Task TestReorderQuestions()
    {
        // 1. Login as Admin
        await LoginAsAdmin();

        // 2. Create a Poll
        var pollTitle = "Reorder Test Poll";
        var createResponse = await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", pollTitle },
            { "Description", "Testing question reordering" },
            { "AccessType", ((int)AccessType.Public).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "Deadline", DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss") }
        });
        
        // Assert redirect to Details
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        var detailsUrl = createResponse.Headers.Location?.OriginalString;
        Assert.IsNotNull(detailsUrl);
        var pollId = Guid.Parse(detailsUrl.Split('/').Last());

        // 3. Add Question 1
        await PostForm("/Polls/AddQuestion", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "Title", "Question 1" },
            { "Type", ((int)QuestionType.SingleChoice).ToString() },
            { "IsRequired", "true" }
        });

        // 4. Add Question 2
        await PostForm("/Polls/AddQuestion", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "Title", "Question 2" },
            { "Type", ((int)QuestionType.SingleChoice).ToString() },
            { "IsRequired", "true" }
        });

        // Verify initial order
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var questions = await dbContext.Questions
                .Where(q => q.PollId == pollId)
                .OrderBy(q => q.Order)
                .ToListAsync();
            
            Assert.AreEqual(2, questions.Count);
            Assert.AreEqual("Question 1", questions[0].Title);
            Assert.AreEqual("Question 2", questions[1].Title);
            Assert.IsTrue(questions[0].Order < questions[1].Order);
            
            var q2Id = questions[1].Id;

            // 5. Move Question 2 Up
            var moveUpResponse = await PostForm($"/Polls/MoveQuestionUp/{q2Id}", new Dictionary<string, string>());
            Assert.AreEqual(HttpStatusCode.Found, moveUpResponse.StatusCode);

            // Verify new order
            var questionsAfterMove = await dbContext.Questions
                .Where(q => q.PollId == pollId)
                .OrderBy(q => q.Order)
                .ToListAsync();
            
            Assert.AreEqual("Question 2", questionsAfterMove[0].Title);
            Assert.AreEqual("Question 1", questionsAfterMove[1].Title);

            // 6. Move Question 2 Down
            var moveDownResponse = await PostForm($"/Polls/MoveQuestionDown/{q2Id}", new Dictionary<string, string>());
            Assert.AreEqual(HttpStatusCode.Found, moveDownResponse.StatusCode);

            // Verify original order restored
            var questionsAfterMoveBack = await dbContext.Questions
                .Where(q => q.PollId == pollId)
                .OrderBy(q => q.Order)
                .ToListAsync();
            
            Assert.AreEqual("Question 1", questionsAfterMoveBack[0].Title);
            Assert.AreEqual("Question 2", questionsAfterMoveBack[1].Title);
        }
    }
}
