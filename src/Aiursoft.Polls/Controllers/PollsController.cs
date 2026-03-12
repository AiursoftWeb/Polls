using System.Text;
using Aiursoft.Polls.Authorization;
using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Models.PollsViewModels;
using Aiursoft.Polls.Services;
using Aiursoft.Polls.Services.BackgroundJobs;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Controllers;

[Authorize]
[LimitPerMin]
public class PollsController(
    TemplateDbContext context,
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    BackgroundJobQueue backgroundJobQueue) : Controller
{
    // ==================== Helper Methods ====================

    private async Task<User?> GetCurrentUserAsync() => await userManager.GetUserAsync(User);

    private bool HasManagePermission() =>
        User.HasClaim(AppPermissions.Type, AppPermissionNames.CanManagePolls);

    private bool IsCreatorOrAdmin(Poll poll, User user) =>
        poll.CreatedById == user.Id || User.HasClaim(AppPermissions.Type, AppPermissionNames.CanViewSystemContext);

    private async Task<bool> CanUserAccessPoll(Poll poll, User? user)
    {
        switch (poll.AccessType)
        {
            case AccessType.Public:
                return true;
            case AccessType.RegisteredOnly:
                return user != null;
            case AccessType.RoleBased:
                if (user == null) return false;
                var userRoles = await userManager.GetRolesAsync(user);
                // Get role IDs for the user's roles
                var userRoleIds = new List<string>();
                foreach (var roleName in userRoles)
                {
                    var role = await roleManager.FindByNameAsync(roleName);
                    if (role != null) userRoleIds.Add(role.Id);
                }
                return poll.RoleRestrictions?.Any(r => userRoleIds.Contains(r.RoleId)) == true;
            default:
                return false;
        }
    }

    private async Task LogOperationAsync(int pollId, string userId, string action, string? details = null)
    {
        context.PollOperationLogs.Add(new PollOperationLog
        {
            PollId = pollId,
            OperatorId = userId,
            Action = action,
            Details = details
        });
        await context.SaveChangesAsync();
    }

    // ==================== Index (Dashboard) ====================

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
        var user = await GetCurrentUserAsync();
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
            p.AccessType == AccessType.Public ||
            p.AccessType == AccessType.RegisteredOnly ||
            (p.AccessType == AccessType.RoleBased &&
             (p.RoleRestrictions?.Any(r => userRoleIds.Contains(r.RoleId)) ?? false))
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
            .ToListAsync();

        // Managed Polls (Created by user)
        var managedPolls = await context.Polls
            .Where(p => p.CreatedById == user.Id && !p.IsDeleted)
            .OrderByDescending(p => p.CreationTime)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            ToDoPolls = todoPolls,
            HistoryPolls = historyPolls,
            ManagedPolls = managedPolls
        });
    }

    // ==================== Create ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel
        {
            AllRoles = roleManager.Roles.ToList()
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await GetCurrentUserAsync();
            var poll = new Poll
            {
                Title = model.Title!,
                Description = model.Description,
                AccessType = model.AccessType,
                Visibility = model.Visibility,
                Deadline = model.Deadline,
                CreatedById = user!.Id,
                State = PollState.Draft
            };
            context.Polls.Add(poll);
            await context.SaveChangesAsync();

            // Add role restrictions if RoleBased
            if (model.AccessType == AccessType.RoleBased && model.SelectedRoles.Count != 0)
            {
                foreach (var roleId in model.SelectedRoles)
                {
                    context.PollRoleRestrictions.Add(new PollRoleRestriction
                    {
                        PollId = poll.Id,
                        RoleId = roleId
                    });
                }
                await context.SaveChangesAsync();
            }

            await LogOperationAsync(poll.Id, user.Id, "Created", "Poll created as draft.");

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        model.AllRoles = roleManager.Roles.ToList();
        return this.StackView(model);
    }

    // ==================== Details ====================

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .Include(p => p.CreatedBy)
            .Include(p => p.RoleRestrictions)
            .Include(p => p.OperationLogs)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        bool hasSubmitted = false;
        Submission? userSubmission = null;

        if (user != null)
        {
            userSubmission = await context.Submissions
                .Include(s => s.Answers!)
                .ThenInclude(a => a.Option)
                .Include(s => s.Answers!)
                .ThenInclude(a => a.Question)
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.PollId == poll.Id);
            hasSubmitted = userSubmission != null;
        }

        bool isCreator = user != null && poll.CreatedById == user.Id;
        bool canManage = isCreator || HasManagePermission();

        return this.StackView(new DetailsViewModel
        {
            Poll = poll,
            HasSubmitted = hasSubmitted,
            UserSubmission = userSubmission,
            IsCreator = isCreator,
            CanManage = canManage
        });
    }

    // ==================== Edit ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

        return this.StackView(new EditViewModel
        {
            Id = poll.Id,
            Title = poll.Title,
            Description = poll.Description,
            AccessType = poll.AccessType,
            Visibility = poll.Visibility,
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
                .FirstOrDefaultAsync(p => p.Id == model.Id && !p.IsDeleted);
            if (poll == null) return NotFound();

            var user = await GetCurrentUserAsync();
            if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

            poll.Title = model.Title!;
            poll.Description = model.Description;
            poll.AccessType = model.AccessType;
            poll.Visibility = model.Visibility;
            poll.Deadline = model.Deadline;
            poll.UpdatedTime = DateTime.UtcNow;

            // Update role restrictions
            var existingRestrictions = poll.RoleRestrictions?.ToList() ?? [];
            context.PollRoleRestrictions.RemoveRange(existingRestrictions);

            if (model.AccessType == AccessType.RoleBased && model.SelectedRoles.Count != 0)
            {
                foreach (var roleId in model.SelectedRoles)
                {
                    context.PollRoleRestrictions.Add(new PollRoleRestriction
                    {
                        PollId = poll.Id,
                        RoleId = roleId
                    });
                }
            }

            await context.SaveChangesAsync();
            await LogOperationAsync(poll.Id, user!.Id, "Edited", "Poll settings updated.");

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        model.AllRoles = roleManager.Roles.ToList();
        return this.StackView(model);
    }

    // ==================== Lifecycle Operations ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

        if (poll.State != PollState.Draft)
            return BadRequest("Only draft polls can be published.");

        poll.State = PollState.Published;
        poll.UpdatedTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await LogOperationAsync(poll.Id, user!.Id, "Published", "Poll published and now accepting submissions.");

        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate(int id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

        if (poll.State != PollState.Published)
            return BadRequest("Only published polls can be terminated.");

        poll.State = PollState.Terminated;
        poll.UpdatedTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await LogOperationAsync(poll.Id, user!.Id, "Terminated", "Poll terminated early.");

        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> Extend(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

        return this.StackView(new ExtendViewModel
        {
            PollId = poll.Id,
            PollTitle = poll.Title,
            CurrentDeadline = poll.Deadline,
            NewDeadline = poll.Deadline.AddDays(7)
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Extend(ExtendViewModel model)
    {
        if (ModelState.IsValid)
        {
            var poll = await context.Polls.FindAsync(model.PollId);
            if (poll == null || poll.IsDeleted) return NotFound();

            var user = await GetCurrentUserAsync();
            if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

            var oldDeadline = poll.Deadline;
            poll.Deadline = model.NewDeadline;
            poll.UpdatedTime = DateTime.UtcNow;

            // If poll was completed/terminated, reactivate it
            if (poll.State is PollState.Completed or PollState.Terminated)
            {
                poll.State = PollState.Published;
            }

            await context.SaveChangesAsync();
            await LogOperationAsync(poll.Id, user!.Id, "Extended",
                $"Deadline extended from {oldDeadline:u} to {model.NewDeadline:u}.");

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invalidate(int id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

        poll.State = PollState.Void;
        poll.IsDeleted = true;
        poll.UpdatedTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await LogOperationAsync(poll.Id, user!.Id, "Invalidated", "Poll has been invalidated (soft deleted).");

        return RedirectToAction(nameof(Index));
    }

    // ==================== Delete ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

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
            var user = await GetCurrentUserAsync();
            if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

            // Soft delete
            poll.State = PollState.Void;
            poll.IsDeleted = true;
            poll.UpdatedTime = DateTime.UtcNow;
            await context.SaveChangesAsync();
            await LogOperationAsync(poll.Id, user!.Id, "Deleted", "Poll soft deleted.");
        }
        return RedirectToAction(nameof(Index));
    }

    // ==================== Question Management ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> AddQuestion(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

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
            if (poll == null || poll.IsDeleted) return NotFound();

            var user = await GetCurrentUserAsync();
            if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

            var order = await context.Questions.Where(q => q.PollId == poll.Id).CountAsync();
            var question = new Question
            {
                PollId = poll.Id,
                Title = model.Title!,
                Type = model.Type,
                IsRequired = model.IsRequired,
                Order = order
            };
            context.Questions.Add(question);
            await context.SaveChangesAsync();

            // Add options (for choice-type questions)
            if (model.Type != QuestionType.TextResponse && model.Options.Count != 0)
            {
                var displayOrder = 0;
                foreach (var opt in model.Options.Where(o => !string.IsNullOrWhiteSpace(o.Content)))
                {
                    context.Options.Add(new Option
                    {
                        QuestionId = question.Id,
                        Content = opt.Content.Trim(),
                        AllowCustomText = opt.AllowCustomText,
                        DisplayOrder = displayOrder++
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
        var question = await context.Questions
            .Include(q => q.Poll)
            .Include(q => q.Options)
            .SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(question.Poll!, user!)) return Unauthorized();

        return this.StackView(new EditQuestionViewModel
        {
            Id = question.Id,
            PollId = question.PollId,
            Title = question.Title,
            Type = question.Type,
            IsRequired = question.IsRequired,
            Options = question.Options?
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new QuestionOptionViewModel
                {
                    Id = o.Id,
                    Content = o.Content,
                    AllowCustomText = o.AllowCustomText
                }).ToList() ?? []
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditQuestion(EditQuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var question = await context.Questions
                .Include(q => q.Poll)
                .Include(q => q.Options)
                .SingleOrDefaultAsync(q => q.Id == model.Id);
            if (question == null) return NotFound();

            var user = await GetCurrentUserAsync();
            if (!IsCreatorOrAdmin(question.Poll!, user!)) return Unauthorized();

            question.Title = model.Title!;
            question.Type = model.Type;
            question.IsRequired = model.IsRequired;

            var existingOptions = question.Options?.ToList() ?? [];
            var modelOptions = model.Options ?? [];

            // Delete removed options
            var modelOptionIds = modelOptions.Select(o => o.Id).ToList();
            var removedOptions = existingOptions.Where(o => !modelOptionIds.Contains(o.Id)).ToList();
            context.Options.RemoveRange(removedOptions);

            // Add or update options
            var displayOrder = 0;
            foreach (var optModel in modelOptions.Where(o => !string.IsNullOrWhiteSpace(o.Content)))
            {
                if (optModel.Id == 0) // New option
                {
                    context.Options.Add(new Option
                    {
                        QuestionId = question.Id,
                        Content = optModel.Content.Trim(),
                        AllowCustomText = optModel.AllowCustomText,
                        DisplayOrder = displayOrder++
                    });
                }
                else // Existing option
                {
                    var existingOpt = existingOptions.FirstOrDefault(o => o.Id == optModel.Id);
                    if (existingOpt != null)
                    {
                        existingOpt.Content = optModel.Content.Trim();
                        existingOpt.AllowCustomText = optModel.AllowCustomText;
                        existingOpt.DisplayOrder = displayOrder++;
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

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(question.Poll!, user!)) return Unauthorized();

        context.Questions.Remove(question);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = question.PollId });
    }

    // ==================== Vote ====================

    [AllowAnonymous]
    public async Task<IActionResult> Vote(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!.OrderBy(q => q.Order))
            .ThenInclude(q => q.Options!.OrderBy(o => o.DisplayOrder))
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
            return BadRequest("This poll is not active.");

        var user = await GetCurrentUserAsync();

        if (!await CanUserAccessPoll(poll, user))
        {
            if (user == null) return Challenge();
            return Forbid();
        }

        // Check if already submitted
        if (user != null)
        {
            var hasSubmitted = await context.Submissions.AnyAsync(s => s.UserId == user.Id && s.PollId == poll.Id);
            if (hasSubmitted) return BadRequest("You have already submitted your response.");
        }

        return this.SimpleView(new VoteViewModel { PollId = poll.Id, Poll = poll });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(VoteViewModel model)
    {
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == model.PollId && !p.IsDeleted);

        if (poll == null) return NotFound();

        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
            return BadRequest("This poll is not active.");

        var user = await GetCurrentUserAsync();
        if (!await CanUserAccessPoll(poll, user))
        {
            if (user == null) return Challenge();
            return Forbid();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // Check duplicate submission
        if (user != null)
        {
            var hasSubmitted = await context.Submissions.AnyAsync(s => s.UserId == user.Id && s.PollId == poll.Id);
            if (hasSubmitted) return BadRequest("You have already submitted your response.");
        }
        else
        {
            // Anonymous IP limit: 5 per day per poll
            var recentAnonymous = await context.Submissions
                .Where(s => s.IpAddress == ip && s.PollId == poll.Id && s.SubmitTime > DateTime.UtcNow.AddDays(-1) && s.UserId == null)
                .CountAsync();
            if (recentAnonymous >= 5)
                return BadRequest("You have reached the maximum number of anonymous submissions for today.");
        }

        // Create submission
        var submission = new Submission
        {
            PollId = poll.Id,
            UserId = user?.Id,
            IpAddress = ip,
            BrowserFingerprint = model.BrowserFingerprint
        };
        context.Submissions.Add(submission);
        await context.SaveChangesAsync();

        // Create answers
        foreach (var question in poll.Questions ?? [])
        {
            switch (question.Type)
            {
                case QuestionType.TextResponse:
                    if (model.CustomTexts.TryGetValue(question.Id, out var textResponse) &&
                        !string.IsNullOrWhiteSpace(textResponse))
                    {
                        context.Answers.Add(new Answer
                        {
                            SubmissionId = submission.Id,
                            QuestionId = question.Id,
                            CustomText = textResponse
                        });
                    }
                    break;

                case QuestionType.SingleChoice:
                    if (model.SelectedOptions.TryGetValue(question.Id, out var singleVal) &&
                        int.TryParse(singleVal, out var singleOptId))
                    {
                        var option = question.Options?.FirstOrDefault(o => o.Id == singleOptId);
                        if (option != null)
                        {
                            var answer = new Answer
                            {
                                SubmissionId = submission.Id,
                                QuestionId = question.Id,
                                OptionId = singleOptId
                            };
                            if (option.AllowCustomText &&
                                model.CustomTexts.TryGetValue(question.Id, out var customText))
                            {
                                answer.CustomText = customText;
                            }
                            context.Answers.Add(answer);
                        }
                    }
                    break;

                case QuestionType.MultipleChoice:
                    if (model.SelectedOptions.TryGetValue(question.Id, out var multiVal))
                    {
                        var optionIds = multiVal.Split(',')
                            .Where(s => int.TryParse(s, out _))
                            .Select(int.Parse)
                            .ToList();

                        foreach (var optId in optionIds)
                        {
                            var option = question.Options?.FirstOrDefault(o => o.Id == optId);
                            if (option != null)
                            {
                                var answer = new Answer
                                {
                                    SubmissionId = submission.Id,
                                    QuestionId = question.Id,
                                    OptionId = optId
                                };
                                if (option.AllowCustomText &&
                                    model.CustomTexts.TryGetValue(question.Id, out var customText))
                                {
                                    answer.CustomText = customText;
                                }
                                context.Answers.Add(answer);
                            }
                        }
                    }
                    break;
            }
        }

        await context.SaveChangesAsync();

        if (user != null)
            return RedirectToAction(nameof(Details), new { id = poll.Id });

        return RedirectToAction(nameof(Results), new { id = poll.Id });
    }

    // ==================== Results ====================

    [AllowAnonymous]
    public async Task<IActionResult> Results(int? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!.OrderBy(q => q.Order))
            .ThenInclude(q => q.Options!.OrderBy(o => o.DisplayOrder))
            .Include(p => p.CreatedBy)
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        var isCreator = user != null && poll.CreatedById == user.Id;
        var isManager = isCreator || (user != null && HasManagePermission());

        // Check visibility
        switch (poll.Visibility)
        {
            case ResultVisibility.CreatorOnly when !isManager:
                return Forbid();
            case ResultVisibility.Participants when !isManager:
                if (user == null) return Challenge();
                var hasSubmitted = await context.Submissions.AnyAsync(s => s.UserId == user.Id && s.PollId == poll.Id);
                if (!hasSubmitted) return Forbid();
                break;
            case ResultVisibility.Public:
                break;
        }

        // Build result data
        var totalSubmissions = await context.Submissions.CountAsync(s => s.PollId == poll.Id);
        var allAnswers = await context.Answers
            .Where(a => a.Submission!.PollId == poll.Id)
            .ToListAsync();

        var questionResults = new List<QuestionResultViewModel>();
        foreach (var question in poll.Questions ?? [])
        {
            var qAnswers = allAnswers.Where(a => a.QuestionId == question.Id).ToList();
            var qResult = new QuestionResultViewModel
            {
                Question = question,
                TotalAnswers = question.Type == QuestionType.TextResponse
                    ? qAnswers.Count
                    : qAnswers.Select(a => a.SubmissionId).Distinct().Count()
            };

            if (question.Type == QuestionType.TextResponse)
            {
                qResult.TextResponses = qAnswers
                    .Where(a => !string.IsNullOrWhiteSpace(a.CustomText))
                    .Select(a => a.CustomText!)
                    .ToList();
            }
            else
            {
                foreach (var option in question.Options ?? [])
                {
                    var optAnswers = qAnswers.Where(a => a.OptionId == option.Id).ToList();
                    qResult.OptionResults.Add(new OptionResultViewModel
                    {
                        Option = option,
                        Count = optAnswers.Count,
                        Percentage = qResult.TotalAnswers > 0
                            ? Math.Round(optAnswers.Count * 100.0 / qResult.TotalAnswers, 1)
                            : 0,
                        CustomTexts = optAnswers
                            .Where(a => !string.IsNullOrWhiteSpace(a.CustomText))
                            .Select(a => a.CustomText!)
                            .ToList()
                    });
                }
            }

            questionResults.Add(qResult);
        }

        return this.StackView(new ResultsViewModel
        {
            Poll = poll,
            TotalSubmissions = totalSubmissions,
            QuestionResults = questionResults,
            CanExport = isManager
        });
    }

    // ==================== Export CSV ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public async Task<IActionResult> ExportCsv(int id)
    {
        var poll = await context.Polls
            .Include(p => p.Questions!.OrderBy(q => q.Order))
            .ThenInclude(q => q.Options)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

        var submissions = await context.Submissions
            .Include(s => s.Answers!)
            .ThenInclude(a => a.Option)
            .Include(s => s.User)
            .Where(s => s.PollId == id)
            .OrderBy(s => s.SubmitTime)
            .ToListAsync();

        var sb = new StringBuilder();
        var questions = poll.Questions?.OrderBy(q => q.Order).ToList() ?? [];

        // Header
        var headers = new List<string> { "Submission ID", "User", "IP Address", "Submit Time" };
        foreach (var q in questions)
        {
            headers.Add($"\"{q.Title.Replace("\"", "\"\"")}\"");
        }
        sb.AppendLine(string.Join(",", headers));

        // Rows
        foreach (var submission in submissions)
        {
            var row = new List<string>
            {
                submission.Id.ToString(),
                $"\"{(submission.User?.DisplayName ?? "Anonymous").Replace("\"", "\"\"")}\"",
                $"\"{submission.IpAddress}\"",
                submission.SubmitTime.ToString("u")
            };

            foreach (var q in questions)
            {
                var answers = submission.Answers?.Where(a => a.QuestionId == q.Id).ToList() ?? [];
                if (q.Type == QuestionType.TextResponse)
                {
                    var text = answers.FirstOrDefault()?.CustomText ?? "";
                    row.Add($"\"{text.Replace("\"", "\"\"")}\"");
                }
                else
                {
                    var parts = answers.Select(a =>
                    {
                        var content = a.Option?.Content ?? "";
                        if (!string.IsNullOrWhiteSpace(a.CustomText))
                            content += $" ({a.CustomText})";
                        return content;
                    });
                    row.Add($"\"{string.Join("; ", parts).Replace("\"", "\"\"")}\"");
                }
            }

            sb.AppendLine(string.Join(",", row));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"poll-{poll.Id}-results.csv");
    }

    // ==================== Send Reminder ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendReminder(int id)
    {
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Unauthorized();

        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
            return BadRequest("Can only send reminders for active polls.");

        // Queue background job for sending reminders
        backgroundJobQueue.QueueWithDependency<TemplateDbContext>(
            $"reminder-poll-{poll.Id}",
            $"Send reminders for poll: {poll.Title}",
            async dbContext =>
            {
                // Get users who should participate but haven't
                var targetUserIds = new HashSet<string>();

                if (poll.AccessType == AccessType.RoleBased && poll.RoleRestrictions != null)
                {
                    foreach (var restriction in poll.RoleRestrictions)
                    {
                        var role = await roleManager.FindByIdAsync(restriction.RoleId);
                        if (role?.Name == null) continue;
                        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name);
                        foreach (var u in usersInRole)
                        {
                            targetUserIds.Add(u.Id);
                        }
                    }
                }
                else if (poll.AccessType == AccessType.RegisteredOnly)
                {
                    var allUsers = await dbContext.Users.Select(u => u.Id).ToListAsync();
                    foreach (var uid in allUsers) targetUserIds.Add(uid);
                }

                // Remove those who already submitted
                var submittedUserIds = await dbContext.Submissions
                    .Where(s => s.PollId == poll.Id && s.UserId != null)
                    .Select(s => s.UserId!)
                    .Distinct()
                    .ToListAsync();

                targetUserIds.ExceptWith(submittedUserIds);

                // Log the reminder operation
                var log = new PollOperationLog
                {
                    PollId = poll.Id,
                    OperatorId = user!.Id,
                    Action = "ReminderSent",
                    Details = $"Reminder sent to {targetUserIds.Count} user(s) who haven't submitted."
                };
                dbContext.PollOperationLogs.Add(log);
                await dbContext.SaveChangesAsync();

                // Note: Actual email sending would be implemented via SMTP service
                // For now, we log the action. Email integration can be added when SMTP settings are configured.
            });

        await LogOperationAsync(poll.Id, user!.Id, "ReminderQueued",
            "Reminder job queued for users who haven't submitted.");

        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }
}
