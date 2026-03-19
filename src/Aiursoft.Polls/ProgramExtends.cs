using Aiursoft.Polls.Authorization;
using Aiursoft.Polls.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Aiursoft.Polls.Services;
using Aiursoft.Polls.Services.FileStorage;
using System.Diagnostics.CodeAnalysis;

namespace Aiursoft.Polls;

[ExcludeFromCodeCoverage]
public static class ProgramExtends
{
    [ExcludeFromCodeCoverage]
    private static async Task<bool> ShouldSeedAsync(TemplateDbContext dbContext)
    {
        return !await dbContext.Set<Poll>().AnyAsync();
    }

    [ExcludeFromCodeCoverage]
    public static Task<IHost> CopyAvatarFileAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var storageService = services.GetRequiredService<StorageService>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        var avatarFilePath = Path.Combine(host.Services.GetRequiredService<IHostEnvironment>().ContentRootPath,
            "wwwroot", "images", "default-avatar.jpg");
        var physicalPath = storageService.GetFilePhysicalPath(User.DefaultAvatarPath);
        if (!File.Exists(avatarFilePath))
        {
            logger.LogWarning("Avatar file does not exist. Skip copying.");
            return Task.FromResult(host);
        }

        if (File.Exists(physicalPath))
        {
            logger.LogInformation("Avatar file already exists. Skip copying.");
            return Task.FromResult(host);
        }

        if (!Directory.Exists(Path.GetDirectoryName(physicalPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        }

        File.Copy(avatarFilePath, physicalPath);
        logger.LogInformation("Avatar file copied to {Path}", physicalPath);
        return Task.FromResult(host);
    }

    [ExcludeFromCodeCoverage]
    public static async Task<IHost> SeedAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<TemplateDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        var settingsService = services.GetRequiredService<GlobalSettingsService>();
        await settingsService.SeedSettingsAsync();

        // Essential infrastructure seeding should always run
        logger.LogInformation("Ensuring essential roles and permissions exist...");
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        var role = await roleManager.FindByNameAsync("Administrators");
        if (role == null)
        {
            role = new IdentityRole("Administrators");
            await roleManager.CreateAsync(role);
        }

        var existingClaims = await roleManager.GetClaimsAsync(role);
        var existingClaimValues = existingClaims
            .Where(c => c.Type == AppPermissions.Type)
            .Select(c => c.Value)
            .ToHashSet();

        foreach (var permission in AppPermissions.GetAllPermissions())
        {
            if (!existingClaimValues.Contains(permission.Key))
            {
                var claim = new Claim(AppPermissions.Type, permission.Key);
                await roleManager.AddClaimAsync(role, claim);
            }
        }

        if (!await db.Users.AnyAsync(u => u.UserName == "admin"))
        {
            logger.LogInformation("Creating default admin user...");
            var user = new User
            {
                UserName = "admin",
                DisplayName = "Super Administrator",
                Email = "admin@default.com",
            };
            var result = await userManager.CreateAsync(user, "admin123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Administrators");
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to create admin user: {Errors}", errors);
            }
        }
        else
        {
             var admin = await userManager.FindByNameAsync("admin");
             if (admin != null && !await userManager.IsInRoleAsync(admin, "Administrators"))
             {
                 await userManager.AddToRoleAsync(admin, "Administrators");
             }
        }

        var shouldSeed = await ShouldSeedAsync(db);
        if (!shouldSeed)
        {
            return host;
        }

        logger.LogInformation("Seeding the database with demo data...");
        // Add business demo data seeding logic here if needed.
        return host;
    }
}
