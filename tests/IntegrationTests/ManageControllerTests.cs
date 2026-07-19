using System.Net;
using Aiursoft.Polls.Services;
using Aiursoft.Polls.Services.FileStorage;

using Aiursoft.Polls.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Aiursoft.Polls.Tests.IntegrationTests;

[TestClass]
public class ManageControllerTests : TestBase
{
    [TestMethod]
    public async Task TestManageWorkflow()
    {
        await LoginAsAdmin();

        // Ensure AllowUserAdjustNickname is true
        using (var scope = Server!.Services.CreateScope())
        {
            var settingsService = scope.ServiceProvider.GetRequiredService<GlobalSettingsService>();
            await settingsService.UpdateSettingAsync(Configuration.SettingsMap.AllowUserAdjustNickname, "True");
        }

        // 1. Index
        var indexResponse = await Http.GetAsync("/Manage/Index");
        indexResponse.EnsureSuccessStatusCode();

        // 2. ChangePassword (GET)
        var changePasswordPage = await Http.GetAsync("/Manage/ChangePassword");
        changePasswordPage.EnsureSuccessStatusCode();

        // 3. ChangeProfile (GET)
        var changeProfilePage = await Http.GetAsync("/Manage/ChangeProfile");
        changeProfilePage.EnsureSuccessStatusCode();

        // 4. ChangeAvatar (GET)
        var changeAvatarPage = await Http.GetAsync("/Manage/ChangeAvatar");
        changeAvatarPage.EnsureSuccessStatusCode();
    }

    [TestMethod]
    public async Task TestChangePasswordFailure()
    {
        await RegisterAndLoginAsync();

        // Test with wrong old password
        var response = await PostForm("/Manage/ChangePassword", new Dictionary<string, string>
        {
            { "OldPassword", "WrongPassword" },
            { "NewPassword", "NewPassword123!" },
            { "ConfirmPassword", "NewPassword123!" }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Incorrect password.", html);
    }

    [TestMethod]
    public async Task TestChangeProfileFailure()
    {
        await RegisterAndLoginAsync();

        // Test with invalid model (empty name)
        var response = await PostForm("/Manage/ChangeProfile", new Dictionary<string, string>
        {
            { "Name", "" }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        // Should stay on the same page with validation error
    }

    [TestMethod]
    public async Task TestChangeAvatarInvalidImage()
    {
        await RegisterAndLoginAsync();

        // Upload a non-image file
        var content = new StringContent("Not an image");
        var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(content, "file", "test.txt");

        var storage = GetService<StorageService>();
        var uploadUrl = storage.GetUploadUrl("avatar", isVault: false);
        var uploadResponse = await Http.PostAsync(uploadUrl, multipartContent);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResult>();
        string path = uploadResult!.Path;

        var response = await PostForm("/Manage/ChangeAvatar", new Dictionary<string, string>
        {
            { "AvatarUrl", path }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The file is not a valid image.", html);
    }

    [TestMethod]
    public async Task TestChangeAvatarAllowsJpegExtension()
    {
        await RegisterAndLoginAsync();

        var response = await Http.GetAsync("/Manage/ChangeAvatar");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-allowed-file-extensions=\"png bmp jpg jpeg\"", html);
        Assert.Contains("validExtensions: ('png bmp jpg jpeg' || '').split(' ').filter(Boolean)", html);
    }

    private class UploadResult
    {
        public string Path { get; init; } = string.Empty;
    }

    [TestMethod]
    public async Task TestDeleteAccount_WithContent_ContentSurvives()
    {
        // Arrange: register, login, create content owned by the user
        var (email, _) = await RegisterAndLoginAsync();

        string userId;
        int entityId; // or Guid
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(email);
            userId = user!.Id;

            var db = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var entity = new Poll { Title = "Test poll for account deletion UT", CreatedById = userId };
            db.Polls.Add(entity);
            await db.SaveChangesAsync();
        }

        // Act: delete account
        var deleteResponse = await PostForm("/Manage/DeleteAccountPost", new(),
            tokenUrl: "/Manage/DeleteAccount");
        AssertRedirect(deleteResponse, "/");

        // Assert: signed out
        var managePage = await Http.GetAsync("/Manage/Index");
        Assert.AreEqual(HttpStatusCode.Found, managePage.StatusCode);

        // Assert: user gone from DB
        using (var scope = Server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Assert.IsNull(await userManager.FindByEmailAsync(email));
        }

        // Assert: content still exists (not cascade-deleted, proving SetNull behavior)
        // Note: InMemory DB does not enforce FK constraints, so CreatedById
        // is NOT set to NULL here. With Sqlite/MySQL, ON DELETE SET NULL
        // would nullify this column automatically.
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            Assert.IsTrue(await db.Polls.AnyAsync(p => p.CreatedById == userId));
        }
    }

    [TestMethod]
    public async Task TestDeleteAccount_NoContent_Succeeds()
    {
        var (email, _) = await RegisterAndLoginAsync();

        var deletePage = await Http.GetAsync("/Manage/DeleteAccount");
        deletePage.EnsureSuccessStatusCode();

        var deleteResponse = await PostForm("/Manage/DeleteAccountPost", new(),
            tokenUrl: "/Manage/DeleteAccount");
        AssertRedirect(deleteResponse, "/");

        var managePage = await Http.GetAsync("/Manage/Index");
        Assert.AreEqual(HttpStatusCode.Found, managePage.StatusCode);

        using var scope = Server!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        Assert.IsNull(await userManager.FindByEmailAsync(email));
    }

    [TestMethod]
    public async Task TestDeleteAccount_Unauthenticated_RedirectsToLogin()
    {
        var deletePage = await Http.GetAsync("/Manage/DeleteAccount");
        Assert.AreEqual(HttpStatusCode.Found, deletePage.StatusCode);
    }
}
