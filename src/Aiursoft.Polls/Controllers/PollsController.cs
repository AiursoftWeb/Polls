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
public class PollsController(TemplateDbContext context, UserManager<User> userManager) : Controller
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
        return this.StackView(new CreateViewModel());
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
            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
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
        var poll = await context.Polls.FindAsync(id);
        if (poll == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (poll.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext) /* System Admin fallback */)
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
            Deadline = poll.Deadline
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var poll = await context.Polls.FindAsync(model.Id);
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
            
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
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
            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> EditQuestion(int? id)
    {
        if (id == null) return NotFound();
        var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (question.Poll!.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            return Unauthorized();

        return this.StackView(new EditQuestionViewModel
        {
            Id = question.Id,
            PollId = question.PollId,
            Title = question.Title,
            Type = question.Type
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditQuestion(EditQuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == model.Id);
            if (question == null) return NotFound();

            var user = await userManager.GetUserAsync(User);
            if (question.Poll!.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
                return Unauthorized();

            question.Title = model.Title!;
            question.Type = model.Type;

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

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> AddOption(int? id)
    {
        if (id == null) return NotFound();
        var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (question.Poll!.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
            return Unauthorized();

        return this.StackView(new AddOptionViewModel { QuestionId = question.Id });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOption(AddOptionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == model.QuestionId);
            if (question == null) return NotFound();

            var user = await userManager.GetUserAsync(User);
            if (question.Poll!.CreatedById != user!.Id && !User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext))
                return Unauthorized();

            var option = new Option
            {
                QuestionId = question.Id,
                Content = model.Content!
            };
            context.Options.Add(option);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = question.PollId });
        }
        return this.StackView(model);
    }

    public async Task<IActionResult> Vote(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .Include(p => p.RoleRestrictions)
            .Include(p => p.UserRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id);

        if (poll == null) return NotFound();

        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
        {
            return BadRequest("This poll is not active.");
        }

        var user = await userManager.GetUserAsync(User);
        if (!poll.IsPublic)
        {
            if (user == null) return Unauthorized();

            var userRoles = await userManager.GetRolesAsync(user);
            bool allowed = false;

            if ((poll.RoleRestrictions == null || !poll.RoleRestrictions.Any()) &&
                (poll.UserRestrictions == null || !poll.UserRestrictions.Any()))
            {
                allowed = true;
            }
            else if (poll.RoleRestrictions?.Any(r => userRoles.Contains(r.RoleId)) == true ||
                     poll.UserRestrictions?.Any(u => u.UserId == user.Id) == true)
            {
                allowed = true;
            }

            if (!allowed) return Forbid();
        }

        if (user != null)
        {
            bool hasVoted = await context.Votes.AnyAsync(v => v.UserId == user.Id && v.Option!.Question!.PollId == poll.Id);
            if (hasVoted) return BadRequest("You have already voted.");
        }

        return this.StackView(new VoteViewModel { PollId = poll.Id, Poll = poll });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(VoteViewModel model)
    {
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .Include(p => p.RoleRestrictions)
            .Include(p => p.UserRestrictions)
            .SingleOrDefaultAsync(p => p.Id == model.PollId);

        if (poll == null) return NotFound();

        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
        {
            return BadRequest("This poll is not active.");
        }

        var user = await userManager.GetUserAsync(User);
        if (!poll.IsPublic)
        {
            if (user == null) return Unauthorized();

            var userRoles = await userManager.GetRolesAsync(user);
            bool allowed = false;

            if ((poll.RoleRestrictions == null || !poll.RoleRestrictions.Any()) &&
                (poll.UserRestrictions == null || !poll.UserRestrictions.Any()))
            {
                allowed = true;
            }
            else if (poll.RoleRestrictions?.Any(r => userRoles.Contains(r.RoleId)) == true ||
                     poll.UserRestrictions?.Any(u => u.UserId == user.Id) == true)
            {
                allowed = true;
            }

            if (!allowed) return Forbid();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        if (user != null)
        {
            bool hasVoted = await context.Votes.AnyAsync(v => v.UserId == user.Id && v.Option!.Question!.PollId == poll.Id);
            if (hasVoted) return BadRequest("You have already voted.");
        }
        else
        {
            // Anonymous public vote IP limit check. Limit to 5 per IP per day for this poll.
            var recentVotes = await context.Votes
                .Where(v => v.IPAddress == ip && v.Option!.Question!.PollId == poll.Id && v.CreationTime > DateTime.UtcNow.AddDays(-1))
                .GroupBy(v => v.CreationTime)
                .CountAsync();

            if (recentVotes >= 5)
            {
                return BadRequest("You have reached the maximum number of anonymous votes for this poll today.");
            }
        }

        // Validate and insert votes
        var votes = new List<Vote>();
        foreach (var answer in model.Answers)
        {
            var questionId = answer.Key;
            var optionIdsStr = answer.Value; // could be comma separated for MultipleChoice

            var question = poll.Questions?.SingleOrDefault(q => q.Id == questionId);
            if (question == null) continue;

            var selectedOptionIds = optionIdsStr.Split(',').Select(int.Parse).ToList();

            if (question.Type == QuestionType.SingleChoice && selectedOptionIds.Count > 1)
            {
                return BadRequest($"Question '{question.Title}' allows only one choice.");
            }

            foreach (var optId in selectedOptionIds)
            {
                if (question.Options?.Any(o => o.Id == optId) == true)
                {
                    votes.Add(new Vote
                    {
                        OptionId = optId,
                        UserId = poll.IsAnonymous ? null : user?.Id, // Anonymize if needed
                        IPAddress = ip
                    });
                }
            }
        }

        context.Votes.AddRange(votes);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }

    public async Task<IActionResult> Results(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options!)
            .ThenInclude(o => o.Votes!)
            .ThenInclude(v => v.User)
            .Include(p => p.CreatedBy)
            .Include(p => p.RoleRestrictions)
            .Include(p => p.UserRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id);

        if (poll == null) return NotFound();

        var user = await userManager.GetUserAsync(User);

        // Visibility rules for results:
        // 1. Manager/Admin can always see
        // 2. If it's Public, everyone can see
        // 3. Otherwise, check RBAC as with voting
        bool isManager = user != null && (poll.CreatedById == user.Id || User.HasClaim(AppPermissions.Type, AppPermissionNames.CanManagePolls));

        if (!poll.IsPublic && !isManager)
        {
            if (user == null) return Unauthorized();

            var userRoles = await userManager.GetRolesAsync(user);
            bool allowed = false;

            if ((poll.RoleRestrictions == null || !poll.RoleRestrictions.Any()) &&
                (poll.UserRestrictions == null || !poll.UserRestrictions.Any()))
            {
                allowed = true;
            }
            else if (poll.RoleRestrictions?.Any(r => userRoles.Contains(r.RoleId)) == true ||
                     poll.UserRestrictions?.Any(u => u.UserId == user.Id) == true)
            {
                allowed = true;
            }

            if (!allowed) return Forbid();
        }

        return this.StackView(new DetailsViewModel { Poll = poll, HasVoted = true, UserVotes = [] }, "Results");
    }
}
