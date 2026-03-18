using System.Net;
using Aiursoft.Polls.Entities;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class ReminderTests : TestBase
{
    [TestMethod]
    public async Task TestPublicPollReminderButtonHidden()
    {
        await LoginAsAdmin();

        // 1. Create a public poll
        var pollTitle = "Public Poll " + Guid.NewGuid();
        var createResponse = await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", pollTitle },
            { "Description", "A public test poll" },
            { "AccessType", ((int)AccessType.Public).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "Deadline", DateTime.UtcNow.AddDays(7).ToString("O") }
        });
        
        // Should redirect to Details
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        var location = createResponse.Headers.Location?.ToString() ?? string.Empty;
        var pollIdString = location.Split('/').LastOrDefault();
        if (pollIdString?.Contains("?id=") == true)
        {
            pollIdString = pollIdString.Split("id=")[1].Split("&")[0];
        }
        var pollId = Guid.Parse(pollIdString!);

        // 2. Publish it (reminders only work on published polls)
        var publishResponse = await PostForm($"/Polls/Publish/{pollId}", new Dictionary<string, string>
        {
            { "id", pollId.ToString() }
        }, tokenUrl: $"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, publishResponse.StatusCode);

        // 3. Check Details view
        var detailsResponse = await Http.GetAsync($"/Polls/Details/{pollId}");
        detailsResponse.EnsureSuccessStatusCode();
        var html = await detailsResponse.Content.ReadAsStringAsync();

        // Verify "Remind" button is NOT present
        Assert.IsFalse(html.Contains("name=\"id\" value=\"" + pollId + "\"") && html.Contains("SendReminder"), "Reminder button should be hidden for public polls.");

        // 4. Try calling SendReminder directly and it should fail
        var reminderResponse = await PostForm($"/Polls/SendReminder/{pollId}", new Dictionary<string, string>
        {
            { "id", pollId.ToString() }
        }, tokenUrl: $"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.BadRequest, reminderResponse.StatusCode);
        var errorContent = await reminderResponse.Content.ReadAsStringAsync();
        Assert.IsTrue(errorContent.Contains("Reminders are only supported for RoleBased polls."));
    }

    [TestMethod]
    public async Task TestRegisteredOnlyPollReminderButtonHidden()
    {
        await LoginAsAdmin();

        // 1. Create a registered-only poll
        var pollTitle = "RegisteredOnly Poll " + Guid.NewGuid();
        var createResponse = await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", pollTitle },
            { "Description", "A registered-only test poll" },
            { "AccessType", ((int)AccessType.RegisteredOnly).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "Deadline", DateTime.UtcNow.AddDays(7).ToString("O") }
        });
        
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        var location = createResponse.Headers.Location?.ToString() ?? string.Empty;
        var pollIdString = location.Split('/').LastOrDefault();
        if (pollIdString?.Contains("?id=") == true)
        {
            pollIdString = pollIdString.Split("id=")[1].Split("&")[0];
        }
        var pollId = Guid.Parse(pollIdString!);

        // 2. Publish it
        var publishResponse = await PostForm($"/Polls/Publish/{pollId}", new Dictionary<string, string>
        {
            { "id", pollId.ToString() }
        }, tokenUrl: $"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, publishResponse.StatusCode);

        // 3. Check Details view
        var detailsResponse = await Http.GetAsync($"/Polls/Details/{pollId}");
        detailsResponse.EnsureSuccessStatusCode();
        var html = await detailsResponse.Content.ReadAsStringAsync();

        // Verify "Remind" button is NOT present
        Assert.IsFalse(html.Contains("name=\"id\" value=\"" + pollId + "\"") && html.Contains("SendReminder"), "Reminder button should be hidden for registered-only polls.");

        // 4. Try calling SendReminder directly and it should fail
        var reminderResponse = await PostForm($"/Polls/SendReminder/{pollId}", new Dictionary<string, string>
        {
            { "id", pollId.ToString() }
        }, tokenUrl: $"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.BadRequest, reminderResponse.StatusCode);
    }

    [TestMethod]
    public async Task TestRoleBasedPollReminderButtonVisible()
    {
        await LoginAsAdmin();

        // Need to get a valid role ID for RoleBased poll
        var dashboardResponse = await Http.GetAsync("/Polls/Create");
        var dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
        // Just extract the first role ID from the create page
        var roleIdMatch = System.Text.RegularExpressions.Regex.Match(dashboardHtml, @"name=""SelectedRoles"" value=""([^""]+)""");
        if (!roleIdMatch.Success)
        {
            // Fallback if no roles found, maybe try to create one or use a known default?
            // Usually SeedAsync() should have created some.
            // Let's assume there's at least one role.
            Assert.Fail("No roles found to create a RoleBased poll.");
        }
        var roleId = roleIdMatch.Groups[1].Value;

        // 1. Create a role-based poll
        var pollTitle = "RoleBased Poll " + Guid.NewGuid();
        var createResponse = await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", pollTitle },
            { "Description", "A role-based test poll" },
            { "AccessType", ((int)AccessType.RoleBased).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "Deadline", DateTime.UtcNow.AddDays(7).ToString("O") },
            { "SelectedRoles", roleId }
        });
        
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        var location = createResponse.Headers.Location?.ToString() ?? string.Empty;
        var pollIdString = location.Split('/').LastOrDefault();
        if (pollIdString?.Contains("?id=") == true)
        {
            pollIdString = pollIdString.Split("id=")[1].Split("&")[0];
        }
        var pollId = Guid.Parse(pollIdString!);

        // 2. Publish it
        var publishResponse = await PostForm($"/Polls/Publish/{pollId}", new Dictionary<string, string>
        {
            { "id", pollId.ToString() }
        }, tokenUrl: $"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, publishResponse.StatusCode);

        // 3. Check Details view
        var detailsResponse = await Http.GetAsync($"/Polls/Details/{pollId}");
        detailsResponse.EnsureSuccessStatusCode();
        var html = await detailsResponse.Content.ReadAsStringAsync();

        // Verify "Remind" button IS present
        Assert.IsTrue(html.Contains("action=\"/Polls/SendReminder"), "Reminder form action should be present.");
        Assert.IsTrue(html.Contains("Remind"), "Reminder button text should be present.");

        // 4. Try calling SendReminder and it should succeed (redirect)
        var reminderResponse = await PostForm($"/Polls/SendReminder/{pollId}", new Dictionary<string, string>
        {
            { "id", pollId.ToString() }
        }, tokenUrl: $"/Polls/Details/{pollId}");
        Assert.AreEqual(HttpStatusCode.Found, reminderResponse.StatusCode);
        AssertRedirect(reminderResponse, $"/Polls/Details/{pollId}");
    }
}
