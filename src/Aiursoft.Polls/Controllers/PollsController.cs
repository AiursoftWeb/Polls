using Aiursoft.Polls.Authorization;
using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Models.PollsViewModels;
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
public class PollsController(TemplateDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Features",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Polls",
        CascadedLinksIcon = "bar-chart",
        CascadedLinksOrder = 2,
        LinkText = "Dashboard",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Include Role restrictions to evaluate RBAC
        var userRoles = await userManager.GetRolesAsync(user);

        // Active Polls that the user can vote in (Todo)
        // A poll is To-Do for the user if:
        // 1. It is Published
        // 2. Deadline is in the future
        // 3. User hasn't voted
        // 4. Matches Public rules, OR matches RBAC restrictions (Dept, Role, User)
        // Note: For simplicity, computing locally after fetching relevant or doing complex query.
        
        var allActivePolls = await context.Polls
            .Include(p => p.RoleRestrictions)
            .Include(p => p.UserRestrictions)
            .Where(p => p.State == PollState.Published && p.Deadline > DateTime.UtcNow)
            .ToListAsync();

        var todoPolls = allActivePolls.Where(p =>
            p.IsPublic ||
            (p.RoleRestrictions?.Any(r => userRoles.Contains(r.RoleId)) ?? false) ||
            (p.UserRestrictions?.Any(u => u.UserId == user.Id) ?? false) ||
            // If no restrictions, maybe we consider it open to all internal?
            ((p.RoleRestrictions == null || !p.RoleRestrictions.Any()) &&
             (p.UserRestrictions == null || !p.UserRestrictions.Any()))
        ).ToList();

        // Remove those already voted by user
        var votedPollIds = await context.Votes
            .Where(v => v.UserId == user.Id)
            .Include(v => v.Option!.Question!)
            .Select(v => v.Option!.Question!.PollId)
            .Distinct()
            .ToListAsync();

        todoPolls = todoPolls.Where(p => !votedPollIds.Contains(p.Id)).ToList();

        // History
        var historyPolls = await context.Polls
            .Where(p => votedPollIds.Contains(p.Id))
            .ToListAsync();

        // Managed Polls (Created by user)
        var managedPolls = await context.Polls
            .Where(p => p.CreatedById == user.Id)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            ToDoPolls = todoPolls,
            HistoryPolls = historyPolls,
            ManagedPolls = managedPolls
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public IActionResult Create()
    {
        var model = new CreateViewModel
        {
            AllRoles = roleManager.Roles.ToList()
        };
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await userManager.GetUserAsync(User);
            var poll = new Poll
            {
                Title = model.Title!,
                Content = model.Content,
                IsAnonymous = model.IsAnonymous,
                IsPublic = model.IsPublic,
                Deadline = model.Deadline,
                CreatedById = user!.Id,
                State = PollState.Draft
            };
            context.Polls.Add(poll);
            await context.SaveChangesAsync();

            // Add role restrictions
            if (model.SelectedRoles != null && model.SelectedRoles.Any())
            {
                foreach (var roleId in model.SelectedRoles)
                {
                    var restriction = new PollRoleRestriction
                    {
                        PollId = poll.Id,
                        RoleId = roleId
                    };
                    context.PollRoleRestrictions.Add(restriction);
                }
                await context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        model.AllRoles = roleManager.Roles.ToList();
        return this.StackView(model);
    }
    
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .Include(p => p.CreatedBy)
            .SingleOrDefaultAsync(p => p.Id == id);
            
        if (poll == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        bool hasVoted = false;
        List<Vote> userVotes = [];

        if (user != null)
        {
            userVotes = await context.Votes
                .Include(v => v.Option)
                .Where(v => v.UserId == user.Id && v.Option!.Question!.PollId == poll.Id)
                .ToListAsync();
            hasVoted = userVotes.Any();
        }

        return this.StackView(new DetailsViewModel
        {
            Poll = poll,
            HasVoted = hasVoted,
            UserVotes = userVotes
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (poll == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
        {
            return Unauthorized();
        }

        return this.StackView(new EditViewModel
        {
            Id = poll.Id,
            Title = poll.Title,
            Content = poll.Content,
            IsAnonymous = poll.IsAnonymous,
            IsPublic = poll.IsPublic,
            Deadline = poll.Deadline,
            SelectedRoles = poll.RoleRestrictions?.Select(r => r.RoleId).ToList() ?? [],
            AllRoles = roleManager.Roles.ToList()
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var poll = await context.Polls
                .Include(p => p.RoleRestrictions)
                .FirstOrDefaultAsync(p => p.Id == model.Id);
            if (poll == null) return NotFound();

            var user = await userManager.GetUserAsync(User);
            if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            {
                return Unauthorized();
            }

            poll.Title = model.Title!;
            poll.Content = model.Content;
            poll.IsAnonymous = model.IsAnonymous;
            poll.IsPublic = model.IsPublic;
            poll.Deadline = model.Deadline;

            // Update role restrictions
            var existingRestrictions = poll.RoleRestrictions?.ToList() ?? [];
            foreach (var restriction in existingRestrictions)
            {
                context.PollRoleRestrictions.Remove(restriction);
            }

            if (model.SelectedRoles != null && model.SelectedRoles.Any())
            {
                foreach (var roleId in model.SelectedRoles)
                {
                    var restriction = new PollRoleRestriction
                    {
                        PollId = poll.Id,
                        RoleId = roleId
                    };
                    context.PollRoleRestrictions.Add(restriction);
                }
            }

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        model.AllRoles = roleManager.Roles.ToList();
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            return Unauthorized();

        poll.State = PollState.Published;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            return Unauthorized();

        return this.StackView(new DeleteViewModel { Poll = poll });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll != null)
        {
            var user = await userManager.GetUserAsync(User);
            if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
                return Unauthorized();

            context.Polls.Remove(poll);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> AddQuestion(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            return Unauthorized();

        return this.StackView(new AddQuestionViewModel { PollId = poll.Id });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(AddQuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var poll = await context.Polls.FindAsync(model.PollId);
            if (poll == null) return NotFound();

            var user = await userManager.GetUserAsync(User);
            if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
                return Unauthorized();

            var order = await context.Questions.Where(q => q.PollId == poll.Id).CountAsync();
            var req = new Question
            {
                PollId = poll.Id,
                Title = model.Title!,
                Type = model.Type,
                Order = order
            };
            context.Questions.Add(req);
            await context.SaveChangesAsync();

            if (model.Options != null && model.Options.Any())
            {
                foreach (var optContent in model.Options.Where(o => !string.IsNullOrWhiteSpace(o)))
                {
                    context.Options.Add(new Option
                    {
                        QuestionId = req.Id,
                        Content = optContent.Trim()
                    });
                }
                await context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> EditQuestion(int? id)
    {
        if (id == null) return NotFound();
        var question = await context.Questions.Include(q => q.Poll).Include(q => q.Options).SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (question.Poll!.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            return Unauthorized();

        return this.StackView(new EditQuestionViewModel
        {
            Id = question.Id,
            PollId = question.PollId,
            Title = question.Title,
            Type = question.Type,
            Options = question.Options?.Select(o => new QuestionOptionViewModel { Id = o.Id, Content = o.Content }).ToList() ?? []
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditQuestion(EditQuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var question = await context.Questions.Include(q => q.Poll).Include(q => q.Options).SingleOrDefaultAsync(q => q.Id == model.Id);
            if (question == null) return NotFound();

            var user = await userManager.GetUserAsync(User);
            if (question.Poll!.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
                return Unauthorized();

            question.Title = model.Title!;
            question.Type = model.Type;

            var existingOptions = question.Options?.ToList() ?? [];
            var modelOptions = model.Options ?? [];

            // Delete removed options
            var modelOptionIds = modelOptions.Select(o => o.Id).ToList();
            var removedOptions = existingOptions.Where(o => !modelOptionIds.Contains(o.Id)).ToList();
            context.Options.RemoveRange(removedOptions);

            // Add or update options
            foreach (var optModel in modelOptions.Where(o => !string.IsNullOrWhiteSpace(o.Content)))
            {
                if (optModel.Id == 0) // New option
                {
                    context.Options.Add(new Option
                    {
                        QuestionId = question.Id,
                        Content = optModel.Content.Trim()
                    });
                }
                else // Existing option
                {
                    var existingOpt = existingOptions.FirstOrDefault(o => o.Id == optModel.Id);
                    if (existingOpt != null)
                    {
                        existingOpt.Content = optModel.Content.Trim();
                    }
                }
            }

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = question.PollId });
        }
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (question.Poll!.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            return Unauthorized();

        context.Questions.Remove(question);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = question.PollId });
    }
}
