using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Models.DashboardViewModels;
using Aiursoft.Polls.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Controllers;

[Authorize]
[LimitPerMin]
public class DashboardController(
    TemplateDbContext context,
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Features",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Dashboard",
        CascadedLinksIcon = "layout",
        CascadedLinksOrder = 1,
        LinkText = "Overview",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var userRoles = await userManager.GetRolesAsync(user);
        var userRoleIds = new List<string>();
        foreach (var roleName in userRoles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role != null) userRoleIds.Add(role.Id);
        }

        var allActivePolls = await context.Polls
            .Include(p => p.RoleRestrictions)
            .Where(p => p.State == PollState.Published && p.Deadline > DateTime.UtcNow && !p.IsDeleted)
            .ToListAsync();

        var todoPolls = allActivePolls.Where(p =>
            !p.IsAnonymous && p.AccessType == AccessType.RoleBased && (p.RoleRestrictions?.Any(r => userRoleIds.Contains(r.RoleId)) ?? false)
        ).ToList();

        var activeAnonymousPolls = allActivePolls.Where(p =>
            p.IsAnonymous && (p.AccessType == AccessType.Public ||
                             (p.AccessType == AccessType.RoleBased && (p.RoleRestrictions?.Any(r => userRoleIds.Contains(r.RoleId)) ?? false)) ||
                             p.AccessType == AccessType.RegisteredOnly)
        ).ToList();

        // Remove those already submitted by user
        var submittedPollIds = await context.Submissions
            .Where(s => s.UserId == user.Id)
            .Select(s => s.PollId)
            .Distinct()
            .ToListAsync();

        todoPolls = todoPolls.Where(p => !submittedPollIds.Contains(p.Id)).ToList();

        // History
        var historyPolls = await context.Polls
            .Where(p => submittedPollIds.Contains(p.Id) && !p.IsDeleted)
            .OrderByDescending(p => p.Deadline)
            .Take(10)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            ToDoPolls = todoPolls,
            ActiveAnonymousPolls = activeAnonymousPolls,
            HistoryPolls = historyPolls
        });
    }
}
