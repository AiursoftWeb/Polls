using System.Net;
using Aiursoft.Polls.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class SubmissionAccessTests : TestBase
{
    [TestMethod]
    public async Task TestSubmissionAccess()
    {
        // 1. Setup - Create data directly in DB for reliability
        Guid pollId = Guid.NewGuid();
        int questionId;
        int optionId;
        string voterEmail = $"voter-{Guid.NewGuid()}@aiursoft.com";

        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            
            // Create Poll
            var poll = new Poll
            {
                Id = pollId,
                Title = "Access Test Poll",
                Description = "Description",
                AccessType = AccessType.Public,
                Visibility = ResultVisibility.Public,
                IsAnonymous = false,
                State = PollState.Published,
                Deadline = DateTime.UtcNow.AddDays(7),
                CreatedById = "admin-id" // Admin ID from seeding usually
            };
            dbContext.Polls.Add(poll);

            // Create Question
            var question = new Question
            {
                PollId = pollId,
                Title = "Test Question",
                Type = QuestionType.SingleChoice,
                IsRequired = true,
                Order = 1
            };
            dbContext.Questions.Add(question);
            await dbContext.SaveChangesAsync();
            questionId = question.Id;

            // Create Option
            var option = new Option
            {
                QuestionId = questionId,
                Content = "Test Option",
                DisplayOrder = 1
            };
            dbContext.Options.Add(option);
            await dbContext.SaveChangesAsync();
            optionId = option.Id;

            // Create User & Submission
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = voterEmail,
                UserName = voterEmail,
                DisplayName = "Test Voter"
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var submission = new Submission
            {
                PollId = pollId,
                UserId = user.Id,
                SubmitTime = DateTime.UtcNow,
                Answers = new List<Answer>
                {
                    new Answer
                    {
                        QuestionId = questionId,
                        OptionId = optionId
                    }
                }
            };
            dbContext.Submissions.Add(submission);
            await dbContext.SaveChangesAsync();
        }

        // 2. Normal user attempts to access submissions list - Should FAIL
        // Register and login a NEW user who is NOT the admin
        await RegisterAndLoginAsync();
        var userSubmissionsResponse = await Http.GetAsync($"/Polls/Submissions/{pollId}");
        Assert.IsTrue(userSubmissionsResponse.StatusCode == HttpStatusCode.Forbidden || userSubmissionsResponse.StatusCode == HttpStatusCode.Redirect);

        // 3. Login back as Admin
        await LoginAsAdmin();

        // 4. Admin accesses submissions list - Should SUCCESS
        var adminSubmissionsResponse = await Http.GetAsync($"/Polls/Submissions/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, adminSubmissionsResponse.StatusCode);
        var submissionsContent = await adminSubmissionsResponse.Content.ReadAsStringAsync();
        Assert.IsTrue(submissionsContent.Contains("Test Voter") || submissionsContent.Contains("Submissions"));

        // 5. Admin accesses individual submission detail - Should SUCCESS
        int submissionId;
        using (var scope = Server!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            submissionId = (await dbContext.Submissions.FirstAsync(s => s.PollId == pollId)).Id;
        }
        var adminDetailResponse = await Http.GetAsync($"/Polls/SubmissionDetail/{submissionId}");
        Assert.AreEqual(HttpStatusCode.OK, adminDetailResponse.StatusCode);
        var detailContent = await adminDetailResponse.Content.ReadAsStringAsync();
        Assert.IsTrue(detailContent.Contains("Test Question"));
        Assert.IsTrue(detailContent.Contains("Test Option"));
    }
}
