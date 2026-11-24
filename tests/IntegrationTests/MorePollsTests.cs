using System.Globalization;
using System.Net;
using Aiursoft.Polls.Configuration;
using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Services;
using Aiursoft.Polls.Services.FileStorage;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class MorePollsTests : TestBase
{
    [TestMethod]
    public async Task TestPollsExtendedFlow()
    {
        await LoginAsAdmin();

        // 1. Index & All
        var indexResp = await Http.GetAsync("/Polls");
        Assert.AreEqual(HttpStatusCode.OK, indexResp.StatusCode);

        var allResp = await Http.GetAsync("/Polls/All");
        Assert.AreEqual(HttpStatusCode.OK, allResp.StatusCode);

        // 2. Create Poll
        var createResponse = await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", "More Polls Test" },
            { "Description", "Testing more endpoints" },
            { "AccessType", ((int)AccessType.Public).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "Deadline", DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) }
        });
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        var detailsUrl = createResponse.Headers.Location?.OriginalString;
        Assert.IsNotNull(detailsUrl);
        var pollId = Guid.Parse(detailsUrl.Split('/').Last());

        // 3. Edit Poll GET & POST
        var editGetResp = await Http.GetAsync($"/Polls/Edit/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, editGetResp.StatusCode);

        var editPostResp = await PostForm($"/Polls/Edit/{pollId}", new Dictionary<string, string>
        {
            { "Id", pollId.ToString() },
            { "Title", "Updated Poll Title" },
            { "Description", "Updated Description" },
            { "AccessType", ((int)AccessType.Public).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "Deadline", DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) }
        });
        Assert.IsTrue(editPostResp.IsSuccessStatusCode || editPostResp.StatusCode == HttpStatusCode.Found);

        // 4. Add Question
        var addQResp = await PostForm("/Polls/AddQuestion", new Dictionary<string, string>
        {
            { "PollId", pollId.ToString() },
            { "Title", "Test Question" },
            { "Type", ((int)QuestionType.SingleChoice).ToString() },
            { "IsRequired", "true" }
        });
        Assert.AreEqual(HttpStatusCode.Found, addQResp.StatusCode);

        int questionId;
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var q = await db.Questions.FirstAsync(x => x.PollId == pollId);
            questionId = q.Id;
        }

        // 5. Edit Question GET & POST
        var editQGet = await Http.GetAsync($"/Polls/EditQuestion/{questionId}");
        Assert.AreEqual(HttpStatusCode.OK, editQGet.StatusCode);

        var editQPost = await PostForm($"/Polls/EditQuestion/{questionId}", new Dictionary<string, string>
        {
            { "Id", questionId.ToString() },
            { "PollId", pollId.ToString() },
            { "Title", "Updated Question Title" },
            { "Type", ((int)QuestionType.SingleChoice).ToString() },
            { "IsRequired", "false" }
        });
        Assert.AreEqual(HttpStatusCode.Found, editQPost.StatusCode);

        // 6. ManageShares GET
        var manageSharesResp = await Http.GetAsync($"/Polls/ManageShares/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, manageSharesResp.StatusCode);

        // 7. Results GET
        var resultsResp = await Http.GetAsync($"/Polls/Results/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, resultsResp.StatusCode);

        // 8. VoterStatus GET
        var voterStatusResp = await Http.GetAsync($"/Polls/VoterStatus/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, voterStatusResp.StatusCode);

        // 9. ExportCsv GET
        var exportCsvResp = await Http.GetAsync($"/Polls/ExportCsv/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, exportCsvResp.StatusCode);

        // 10. ExportPendingUsers GET
        var exportPendingResp = await Http.GetAsync($"/Polls/ExportPendingUsers/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, exportPendingResp.StatusCode);

        // 11. Submissions GET
        var subsResp = await Http.GetAsync($"/Polls/Submissions/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, subsResp.StatusCode);

        // 12. Publish & Vote GET
        var publishResp = await PostForm($"/Polls/Publish/{pollId}", new Dictionary<string, string>());
        Assert.AreEqual(HttpStatusCode.Found, publishResp.StatusCode);

        var voteResp = await Http.GetAsync($"/Polls/Vote/{pollId}");
        Assert.AreEqual(HttpStatusCode.OK, voteResp.StatusCode);
    }

    [TestMethod]
    public async Task TestViewComponents()
    {
        using var scope = Server!.Services.CreateScope();
        var globalSettings = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
        var storage = scope.ServiceProvider.GetRequiredService<StorageService>();

        var navbar = new Views.Shared.Components.MarketingNavbar.MarketingNavbar(globalSettings, storage)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext { HttpContext = new DefaultHttpContext() }
            }
        };
        var navRes = await navbar.InvokeAsync();
        Assert.IsNotNull(navRes);

        var footer = new Views.Shared.Components.MarketingFooter.MarketingFooter(globalSettings, storage)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext { HttpContext = new DefaultHttpContext() }
            }
        };
        var footRes = await footer.InvokeAsync();
        Assert.IsNotNull(footRes);
    }

    [TestMethod]
    public async Task TestGlobalSettingsService()
    {
        var service = GetService<GlobalSettingsService>();
        await service.UpdateSettingAsync(SettingsMap.AllowUserAdjustNickname, "False");
        var val = await service.GetBoolSettingAsync(SettingsMap.AllowUserAdjustNickname);
        Assert.IsFalse(val);

        await service.UpdateSettingAsync(SettingsMap.AllowUserAdjustNickname, "True");
        var val2 = await service.GetBoolSettingAsync(SettingsMap.AllowUserAdjustNickname);
        Assert.IsTrue(val2);
    }
}
