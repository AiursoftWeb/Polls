using System.Net;
using Aiursoft.Polls.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class ExamAccessTests : TestBase
{
    [TestMethod]
    public async Task AssignmentAllowsOnlyTheAssignedEmployeeToStartExam()
    {
        var (assignedEmail, assignedPassword) = await RegisterAndLoginAsync();
        var (otherEmail, otherPassword) = await RegisterAndLoginAsync();
        Guid pollId;
        using (var scope = Server!.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var assigned = await users.FindByEmailAsync(assignedEmail);
            var admin = await users.FindByEmailAsync("admin@default.com");
            var poll = BuildPublishedExam(admin!.Id);
            poll.Assignments = [new PollAssignment { AssignedUserId = assigned!.Id }];
            context.Polls.Add(poll);
            await context.SaveChangesAsync();
            pollId = poll.Id;
        }

        await Login(assignedEmail, assignedPassword);
        var allowed = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("id=\"countdown\"", await allowed.Content.ReadAsStringAsync());

        await Login(otherEmail, otherPassword);
        var denied = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, denied.StatusCode);
        Assert.Contains("/Error/Code403", denied.Headers.Location?.OriginalString ?? string.Empty);
    }

    [TestMethod]
    public async Task EditableAndReadOnlySharesHaveDifferentManagementRights()
    {
        var (editorEmail, editorPassword) = await RegisterAndLoginAsync();
        var (readerEmail, readerPassword) = await RegisterAndLoginAsync();
        Guid pollId;
        using (var scope = Server!.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var editor = await users.FindByEmailAsync(editorEmail);
            var reader = await users.FindByEmailAsync(readerEmail);
            var admin = await users.FindByEmailAsync("admin@default.com");
            var poll = BuildPublishedExam(admin!.Id);
            poll.Shares =
            [
                new PollShare { SharedWithUserId = editor!.Id, Permission = SharePermission.Editable },
                new PollShare { SharedWithUserId = reader!.Id, Permission = SharePermission.ReadOnly }
            ];
            context.Polls.Add(poll);
            await context.SaveChangesAsync();
            pollId = poll.Id;
        }

        await Login(editorEmail, editorPassword);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync($"/Polls/Details/{pollId}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync($"/Polls/Edit/{pollId}")).StatusCode);

        await Login(readerEmail, readerPassword);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync($"/Polls/Details/{pollId}")).StatusCode);
        var editDenied = await Http.GetAsync($"/Polls/Edit/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, editDenied.StatusCode);
        Assert.Contains("/Error/Code403", editDenied.Headers.Location?.OriginalString ?? string.Empty);
    }

    private async Task Login(string email, string password)
    {
        var response = await PostForm("/Account/Login", new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password }
        });
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
    }

    private static Poll BuildPublishedExam(string creatorId) => new()
    {
        Title = $"On-call exam {Guid.NewGuid():N}",
        CreatedById = creatorId,
        State = PollState.Published,
        AccessType = AccessType.Assigned,
        Deadline = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 30,
        FullScore = 4,
        PartialScore = 2,
        PassingScore = 4,
        Questions =
        [
            new Question
            {
                Title = "Choose A",
                Type = QuestionType.SingleChoice,
                Options =
                [
                    new Option { Content = "A", IsCorrect = true },
                    new Option { Content = "B", IsCorrect = false }
                ]
            }
        ]
    };
}
