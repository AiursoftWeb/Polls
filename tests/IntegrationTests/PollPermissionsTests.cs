using System.Net;
using Aiursoft.Polls.Authorization;
using Aiursoft.Polls.Entities;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class PollPermissionsTests : TestBase
{
    private async Task<string> CreatePollAsUser(string email, string password)
    {
        // Login as the user
        var loginResponse = await PostForm("/Account/Login", new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password }
        });
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);

        // Create a Poll
        var pollTitle = $"Poll by {email}";
        var createResponse = await PostForm("/Polls/Create", new Dictionary<string, string>
        {
            { "Title", pollTitle },
            { "Description", "Testing permissions" },
            { "AccessType", ((int)AccessType.Public).ToString() },
            { "Visibility", ((int)ResultVisibility.Public).ToString() },
            { "Deadline", DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss") }
        });
        
        Assert.AreEqual(HttpStatusCode.Found, createResponse.StatusCode);
        var detailsUrl = createResponse.Headers.Location?.OriginalString;
        Assert.IsNotNull(detailsUrl);
        return detailsUrl.Split('/').Last();
    }

    [TestMethod]
    public async Task TestPermissionEnforcement()
    {
        // 1. Setup: Create two users. 
        // User A: Has CanManagePolls (regular manager)
        // User B: Has CanManagePolls (regular manager)
        // Admin: Has CanManageAllPolls (super admin)

        var (emailA, passwordA) = await RegisterAndLoginAsync();
        var (emailB, passwordB) = await RegisterAndLoginAsync();

        // Grant CanManagePolls to both (via a new role or directly)
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            
            var managerRole = new IdentityRole("Managers");
            await roleManager.CreateAsync(managerRole);
            await roleManager.AddClaimAsync(managerRole, new Claim(AppPermissions.Type, AppPermissionNames.CanManagePolls));

            var userA = await userManager.FindByEmailAsync(emailA);
            var userB = await userManager.FindByEmailAsync(emailB);
            await userManager.AddToRoleAsync(userA!, "Managers");
            await userManager.AddToRoleAsync(userB!, "Managers");
        }

        // 2. User A creates a poll
        var pollAId = await CreatePollAsUser(emailA, passwordA);

        // 3. User B tries to access User A's poll details - SHOULD FAIL (Forbid or Unauthorized)
        // Re-login as User B
        await PostForm("/Account/Login", new Dictionary<string, string>
        {
            { "EmailOrUserName", emailB },
            { "Password", passwordB }
        });
        
        var detailsResponseB = await Http.GetAsync($"/Polls/Details/{pollAId}");
        Assert.AreEqual(HttpStatusCode.Found, detailsResponseB.StatusCode);
        Assert.IsTrue(detailsResponseB.Headers.Location?.OriginalString.Contains("/Error/Code403"));

        // 4. User B tries to edit User A's poll - SHOULD FAIL (Redirect to Access Denied)
        var editResponseB = await Http.GetAsync($"/Polls/Edit/{pollAId}");
        Assert.AreEqual(HttpStatusCode.Found, editResponseB.StatusCode);
        Assert.IsTrue(editResponseB.Headers.Location?.OriginalString.Contains("/Error/Code403"));

        // 5. Admin tries to access User A's poll details - SHOULD SUCCEED
        await LoginAsAdmin();
        var detailsResponseAdmin = await Http.GetAsync($"/Polls/Details/{pollAId}");
        Assert.AreEqual(HttpStatusCode.OK, detailsResponseAdmin.StatusCode);

        // 6. Admin tries to edit User A's poll - SHOULD SUCCEED
        var editResponseAdmin = await Http.GetAsync($"/Polls/Edit/{pollAId}");
        Assert.AreEqual(HttpStatusCode.OK, editResponseAdmin.StatusCode);
        
        // 7. User A can still access their own poll
        await PostForm("/Account/Login", new Dictionary<string, string>
        {
            { "EmailOrUserName", emailA },
            { "Password", passwordA }
        });
        var detailsResponseA = await Http.GetAsync($"/Polls/Details/{pollAId}");
        Assert.AreEqual(HttpStatusCode.OK, detailsResponseA.StatusCode);

        // 8. Test access to /Polls/All
        // User A (regular manager) should be forbidden
        var allResponseA = await Http.GetAsync("/Polls/All");
        Assert.AreEqual(HttpStatusCode.Found, allResponseA.StatusCode); // Redirect to 403
        Assert.IsTrue(allResponseA.Headers.Location?.OriginalString.Contains("/Error/Code403"));

        // Admin (super admin) should succeed
        await LoginAsAdmin();
        var allResponseAdmin = await Http.GetAsync("/Polls/All");
        Assert.AreEqual(HttpStatusCode.OK, allResponseAdmin.StatusCode);
    }
}
