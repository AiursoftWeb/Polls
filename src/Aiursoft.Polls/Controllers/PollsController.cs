using System.Text;
using Aiursoft.Canon.TaskQueue;
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
public class PollsController(
    TemplateDbContext context,
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    ServiceTaskQueue taskQueue,
    ExamAttemptService attemptService) : Controller
{
    // ==================== Helper Methods ====================

    private async Task<User?> GetCurrentUserAsync() => await userManager.GetUserAsync(User);

    private bool HasManagePermission() =>
        User.HasClaim(AppPermissions.Type, AppPermissionNames.CanManageAllPolls);

    private bool IsCreatorOrAdmin(Poll poll, User user)
    {
        if (poll.CreatedById == user.Id || HasManagePermission()) return true;
        var roleIds = context.UserRoles.Where(x => x.UserId == user.Id).Select(x => x.RoleId);
        return context.PollShares.Any(s => s.PollId == poll.Id && s.Permission == SharePermission.Editable &&
            (s.SharedWithUserId == user.Id || (s.SharedWithRoleId != null && roleIds.Contains(s.SharedWithRoleId))));
    }

    private bool IsOwnerOrAdmin(Poll poll, User? user) =>
        user != null && (poll.CreatedById == user.Id || HasManagePermission());

    private async Task<List<string>> GetUserRoleIdsAsync(User user)
    {
        var names = await userManager.GetRolesAsync(user);
        return await roleManager.Roles.Where(r => names.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
    }

    private async Task<SharePermission?> GetPollPermissionAsync(Poll poll, User? user)
    {
        if (user == null) return null;
        if (poll.CreatedById == user.Id || HasManagePermission()) return SharePermission.Editable;
        var roleIds = await GetUserRoleIdsAsync(user);
        return await context.PollShares
            .Where(s => s.PollId == poll.Id &&
                        (s.SharedWithUserId == user.Id ||
                         (s.SharedWithRoleId != null && roleIds.Contains(s.SharedWithRoleId))))
            .OrderByDescending(s => s.Permission)
            .Select(s => (SharePermission?)s.Permission)
            .FirstOrDefaultAsync();
    }

    private async Task<bool> CanEditPollAsync(Poll poll, User? user) =>
        await GetPollPermissionAsync(poll, user) == SharePermission.Editable;

    private async Task<bool> CanViewPollManagementAsync(Poll poll, User? user) =>
        await GetPollPermissionAsync(poll, user) != null;

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
            case AccessType.Assigned:
                if (user == null) return false;
                var assignedRoleIds = await GetUserRoleIdsAsync(user);
                return await context.PollAssignments.AnyAsync(a => a.PollId == poll.Id &&
                    (a.AssignedUserId == user.Id ||
                     (a.AssignedRoleId != null && assignedRoleIds.Contains(a.AssignedRoleId))));
            default:
                return false;
        }
    }

    private async Task<(List<User> eligibleUsers, List<User> pendingUsers)> GetEligibleAndPendingUsers(Poll poll)
    {
        var eligibleUsers = new List<User>();
        // Only RoleBased polls have a defined list of eligible voters
        // RegisteredOnly polls are open to all registered users, so we can't track pending voters
        if (poll.AccessType == AccessType.RoleBased && poll.RoleRestrictions != null)
        {
            var userIds = new HashSet<string>();
            foreach (var restriction in poll.RoleRestrictions)
            {
                var role = await roleManager.FindByIdAsync(restriction.RoleId);
                if (role?.Name == null) continue;
                var usersInRole = await userManager.GetUsersInRoleAsync(role.Name);
                foreach (var u in usersInRole)
                {
                    if (userIds.Add(u.Id))
                    {
                        eligibleUsers.Add(u);
                    }
                }
            }
        }
        else if (poll.AccessType == AccessType.Assigned)
        {
            var assignments = await context.PollAssignments.Where(a => a.PollId == poll.Id).ToListAsync();
            var userIds = new HashSet<string>();
            foreach (var directUserId in assignments.Where(a => a.AssignedUserId != null).Select(a => a.AssignedUserId!))
            {
                var directUser = await userManager.FindByIdAsync(directUserId);
                if (directUser != null && userIds.Add(directUser.Id)) eligibleUsers.Add(directUser);
            }
            foreach (var roleId in assignments.Where(a => a.AssignedRoleId != null).Select(a => a.AssignedRoleId!))
            {
                var role = await roleManager.FindByIdAsync(roleId);
                if (role?.Name == null) continue;
                foreach (var assignedUser in await userManager.GetUsersInRoleAsync(role.Name))
                    if (userIds.Add(assignedUser.Id)) eligibleUsers.Add(assignedUser);
            }
        }

        var submittedUserIds = await context.Submissions
            .Where(s => s.PollId == poll.Id && s.UserId != null && s.Status != AttemptStatus.InProgress)
            .Select(s => s.UserId!)
            .Distinct()
            .ToListAsync();

        var pendingUsers = eligibleUsers.Where(u => !submittedUserIds.Contains(u.Id)).ToList();
        return (eligibleUsers, pendingUsers);
    }

    private async Task LogOperationAsync(Guid pollId, string userId, string action, string? details = null)
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
        CascadedLinksGroupName = "Polls Management",
        CascadedLinksIcon = "settings",
        CascadedLinksOrder = 1,
        LinkText = "My Polls",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        // Managed Polls (Created by user)
        var roleIds = await GetUserRoleIdsAsync(user);
        var sharedIds = context.PollShares
            .Where(s => s.SharedWithUserId == user.Id ||
                        (s.SharedWithRoleId != null && roleIds.Contains(s.SharedWithRoleId)))
            .Select(s => s.PollId);
        var managedPolls = await context.Polls
            .Where(p => (p.CreatedById == user.Id || sharedIds.Contains(p.Id)) && !p.IsDeleted)
            .OrderByDescending(p => p.CreationTime)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            ManagedPolls = managedPolls
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageAllPolls)]
    [RenderInNavBar(
        NavGroupName = "Features",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Polls Management",
        CascadedLinksIcon = "settings",
        CascadedLinksOrder = 1,
        LinkText = "All Polls",
        LinkOrder = 2)]
    public async Task<IActionResult> All()
    {
        var allPolls = await context.Polls
            .Include(p => p.CreatedBy)
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.CreationTime)
            .ToListAsync();

        return this.StackView(new IndexViewModel
        {
            ManagedPolls = allPolls,
            PageTitle = "All Polls"
        });
    }

    // ==================== Create ====================

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel
        {
            AllRoles = roleManager.Roles.ToList(),
            AllUsers = userManager.Users.OrderBy(u => u.DisplayName).ToList()
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManagePolls)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        ValidateExamSettings(model.AccessType, model.IsAnonymous, model.FullScore, model.PartialScore, model.OverSelectionScore,
            model.SelectedUserIds.Count + model.SelectedAssignmentRoleIds.Count);
        if (ModelState.IsValid)
        {
            var user = await GetCurrentUserAsync();
            var poll = new Poll
            {
                Title = model.Title!,
                Description = model.Description,
                AccessType = model.AccessType,
                Visibility = model.Visibility,
                IsAnonymous = model.IsAnonymous,
                QuestionsPerAttempt = model.QuestionsPerAttempt,
                ShuffleQuestions = model.ShuffleQuestions,
                ShuffleOptions = model.ShuffleOptions,
                AllowRepeatedSubmissions = model.AllowRepeatedSubmissions,
                DurationMinutes = model.DurationMinutes,
                FullScore = model.FullScore,
                PartialScore = model.PartialScore,
                OverSelectionScore = model.OverSelectionScore,
                PassingScore = model.PassingScore,
                PassMessage = model.PassMessage,
                FailMessage = model.FailMessage,
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

            if (model.AccessType == AccessType.Assigned)
            {
                context.PollAssignments.AddRange(model.SelectedUserIds.Distinct().Select(userId => new PollAssignment
                    { PollId = poll.Id, AssignedUserId = userId }));
                context.PollAssignments.AddRange(model.SelectedAssignmentRoleIds.Distinct().Select(roleId => new PollAssignment
                    { PollId = poll.Id, AssignedRoleId = roleId }));
                await context.SaveChangesAsync();
            }

            await LogOperationAsync(poll.Id, user.Id, "Created", "Poll created as draft.");

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        model.AllRoles = roleManager.Roles.ToList();
        model.AllUsers = userManager.Users.OrderBy(u => u.DisplayName).ToList();
        return this.StackView(model);
    }

    // ==================== Details ====================

    public async Task<IActionResult> Details(Guid? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .Include(p => p.CreatedBy)
            .Include(p => p.RoleRestrictions)
            .Include(p => p.Assignments)
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
                .Where(s => s.UserId == user.Id && s.PollId == poll.Id)
                .OrderByDescending(s => s.AttemptNumber)
                .FirstOrDefaultAsync();
            hasSubmitted = userSubmission != null;
        }

        bool isCreator = user != null && poll.CreatedById == user.Id;
        bool canManage = await CanViewPollManagementAsync(poll, user);

        if (!canManage)
        {
            return Forbid();
        }

        int pendingCount = 0;
        int eligibleCount = 0;
        if (canManage && poll.AccessType is AccessType.RoleBased or AccessType.Assigned)
        {
            var (eligible, pending) = await GetEligibleAndPendingUsers(poll);
            eligibleCount = eligible.Count;
            pendingCount = pending.Count;
        }

        return this.StackView(new DetailsViewModel
        {
            Poll = poll,
            HasSubmitted = hasSubmitted,
            UserSubmission = userSubmission,
            IsCreator = isCreator,
            CanManage = canManage,
            CanEdit = await CanEditPollAsync(poll, user),
            CanManageShares = isCreator || HasManagePermission(),
            PendingVotersCount = pendingCount,
            EligibleVotersCount = eligibleCount
        });
    }

    // ==================== Edit ====================

    [Authorize]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .Include(p => p.Assignments)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!await CanEditPollAsync(poll, user)) return Forbid();

        return this.StackView(new EditViewModel
        {
            Id = poll.Id,
            Title = poll.Title,
            Description = poll.Description,
            AccessType = poll.AccessType,
            Visibility = poll.Visibility,
            IsAnonymous = poll.IsAnonymous,
            QuestionsPerAttempt = poll.QuestionsPerAttempt,
            ShuffleQuestions = poll.ShuffleQuestions,
            ShuffleOptions = poll.ShuffleOptions,
            AllowRepeatedSubmissions = poll.AllowRepeatedSubmissions,
            DurationMinutes = poll.DurationMinutes,
            FullScore = poll.FullScore,
            PartialScore = poll.PartialScore,
            OverSelectionScore = poll.OverSelectionScore,
            PassingScore = poll.PassingScore,
            PassMessage = poll.PassMessage,
            FailMessage = poll.FailMessage,
            Deadline = poll.Deadline.ToSecondPrecision(),
            SelectedRoles = poll.RoleRestrictions?.Select(r => r.RoleId).ToList() ?? [],
            AllRoles = roleManager.Roles.ToList(),
            AllUsers = userManager.Users.OrderBy(u => u.DisplayName).ToList(),
            SelectedUserIds = poll.Assignments?.Where(a => a.AssignedUserId != null).Select(a => a.AssignedUserId!).ToList() ?? [],
            SelectedAssignmentRoleIds = poll.Assignments?.Where(a => a.AssignedRoleId != null).Select(a => a.AssignedRoleId!).ToList() ?? []
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        ValidateExamSettings(model.AccessType, model.IsAnonymous, model.FullScore, model.PartialScore, model.OverSelectionScore,
            model.SelectedUserIds.Count + model.SelectedAssignmentRoleIds.Count);
        if (ModelState.IsValid)
        {
            var poll = await context.Polls
                .Include(p => p.RoleRestrictions)
                .Include(p => p.Assignments)
                .FirstOrDefaultAsync(p => p.Id == model.Id && !p.IsDeleted);
            if (poll == null) return NotFound();

            var user = await GetCurrentUserAsync();
            if (!await CanEditPollAsync(poll, user)) return Forbid();

            poll.Title = model.Title!;
            poll.Description = model.Description;
            poll.AccessType = model.AccessType;
            poll.Visibility = model.Visibility;
            poll.IsAnonymous = model.IsAnonymous;
            poll.QuestionsPerAttempt = model.QuestionsPerAttempt;
            poll.ShuffleQuestions = model.ShuffleQuestions;
            poll.ShuffleOptions = model.ShuffleOptions;
            poll.AllowRepeatedSubmissions = model.AllowRepeatedSubmissions;
            poll.DurationMinutes = model.DurationMinutes;
            poll.FullScore = model.FullScore;
            poll.PartialScore = model.PartialScore;
            poll.OverSelectionScore = model.OverSelectionScore;
            poll.PassingScore = model.PassingScore;
            poll.PassMessage = model.PassMessage;
            poll.FailMessage = model.FailMessage;
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

            context.PollAssignments.RemoveRange(poll.Assignments ?? []);
            if (model.AccessType == AccessType.Assigned)
            {
                context.PollAssignments.AddRange(model.SelectedUserIds.Distinct().Select(userId => new PollAssignment
                    { PollId = poll.Id, AssignedUserId = userId }));
                context.PollAssignments.AddRange(model.SelectedAssignmentRoleIds.Distinct().Select(roleId => new PollAssignment
                    { PollId = poll.Id, AssignedRoleId = roleId }));
            }

            await context.SaveChangesAsync();
            await LogOperationAsync(poll.Id, user!.Id, "Edited", "Poll settings updated.");

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        model.AllRoles = roleManager.Roles.ToList();
        model.AllUsers = userManager.Users.OrderBy(u => u.DisplayName).ToList();
        return this.StackView(model);
    }

    private void ValidateExamSettings(AccessType accessType, bool isAnonymous, decimal fullScore,
        decimal partialScore, decimal overSelectionScore, int assignmentCount)
    {
        if (overSelectionScore > partialScore || partialScore > fullScore)
            ModelState.AddModelError(string.Empty, "Scores must satisfy: over-selection score ≤ partial score ≤ full score.");
        if (isAnonymous)
            ModelState.AddModelError(string.Empty, "License exams cannot be anonymous because employee attempt history is required.");
        if (accessType == AccessType.Assigned && assignmentCount == 0)
            ModelState.AddModelError(string.Empty, "Assign the exam to at least one employee or role.");
    }

    private IActionResult RedirectToDetailsWithError(Guid pollId, string message)
    {
        TempData["ErrorMessage"] = message;
        return RedirectToAction(nameof(Details), new { id = pollId });
    }

    // ==================== Lifecycle Operations ====================

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id)
    {
        var poll = await context.Polls.Include(p => p.Questions!).ThenInclude(q => q.Options)
            .SingleOrDefaultAsync(p => p.Id == id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsOwnerOrAdmin(poll, user)) return Forbid();

        if (poll.State != PollState.Draft)
            return RedirectToDetailsWithError(poll.Id, "Only draft exams can be published.");

        var questions = poll.Questions?.ToList() ?? [];
        if (questions.Count == 0)
            return RedirectToDetailsWithError(poll.Id, "Add at least one question before publishing the exam.");
        if (poll.QuestionsPerAttempt > questions.Count)
            return RedirectToDetailsWithError(poll.Id,
                $"Questions per attempt is {poll.QuestionsPerAttempt}, but this exam currently contains only {questions.Count}. Add more questions or lower the sampled question count.");
        var scoredQuestionCount = poll.QuestionsPerAttempt <= 0 ? questions.Count : poll.QuestionsPerAttempt;
        if (poll.PassingScore > scoredQuestionCount * poll.FullScore)
            return RedirectToDetailsWithError(poll.Id,
                $"The passing score cannot exceed the maximum score of {scoredQuestionCount * poll.FullScore} for one attempt.");
        if (poll.DurationMinutes <= 0 || poll.OverSelectionScore > poll.PartialScore || poll.PartialScore > poll.FullScore)
            return RedirectToDetailsWithError(poll.Id,
                "Review the exam duration and scoring rules before publishing.");
        foreach (var question in questions)
        {
            if ((question.Options?.Count ?? 0) < 2)
                return RedirectToDetailsWithError(poll.Id,
                    $"Question '{question.Title}' must contain at least two options.");
            var correctCount = question.Options?.Count(o => o.IsCorrect) ?? 0;
            if (correctCount == 0 || (question.Type == QuestionType.SingleChoice && correctCount != 1))
                return RedirectToDetailsWithError(poll.Id,
                    $"Question '{question.Title}' does not have a valid set of correct options.");
        }

        poll.State = PollState.Published;
        poll.UpdatedTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await LogOperationAsync(poll.Id, user!.Id, "Published", "Poll published and now accepting submissions.");

        TempData["SuccessMessage"] = "The exam is published and ready for employees.";
        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate(Guid id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsOwnerOrAdmin(poll, user)) return Forbid();

        if (poll.State != PollState.Published)
            return BadRequest("Only published polls can be terminated.");

        poll.State = PollState.Terminated;
        poll.UpdatedTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await LogOperationAsync(poll.Id, user!.Id, "Terminated", "Poll terminated early.");

        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }

    [Authorize]
    public async Task<IActionResult> Extend(Guid? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsOwnerOrAdmin(poll, user)) return Forbid();

        return this.StackView(new ExtendViewModel
        {
            PollId = poll.Id,
            PollTitle = poll.Title,
            CurrentDeadline = poll.Deadline.ToSecondPrecision(),
            NewDeadline = (poll.State is PollState.Completed or PollState.Terminated
                ? (poll.Deadline > DateTime.UtcNow ? poll.Deadline : DateTime.UtcNow.AddDays(7))
                : poll.Deadline.AddDays(7)).ToSecondPrecision(),
            ReactivatesExam = poll.State is PollState.Completed or PollState.Terminated
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Extend(ExtendViewModel model)
    {
        var poll = await context.Polls.FindAsync(model.PollId);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsOwnerOrAdmin(poll, user)) return Forbid();

        var reactivatesExam = poll.State is PollState.Completed or PollState.Terminated;
        model.PollTitle = poll.Title;
        model.CurrentDeadline = poll.Deadline.ToSecondPrecision();
        model.ReactivatesExam = reactivatesExam;

        if (reactivatesExam && model.NewDeadline <= DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(model.NewDeadline), "Choose a future deadline before reopening the exam.");
        }
        else if (!reactivatesExam && model.NewDeadline <= poll.Deadline)
        {
            ModelState.AddModelError(nameof(model.NewDeadline), "New deadline must be later than the current deadline.");
        }

        if (!ModelState.IsValid) return this.StackView(model);

        var oldDeadline = poll.Deadline;
        poll.Deadline = model.NewDeadline;
        poll.UpdatedTime = DateTime.UtcNow;
        if (reactivatesExam) poll.State = PollState.Published;

        await context.SaveChangesAsync();
        await LogOperationAsync(poll.Id, user!.Id, reactivatesExam ? "Reopened" : "Extended",
            reactivatesExam
                ? $"Exam reopened with deadline {model.NewDeadline:u}."
                : $"Deadline extended from {oldDeadline:u} to {model.NewDeadline:u}.");

        TempData["SuccessMessage"] = reactivatesExam
            ? "The exam is open again and ready for new attempts."
            : "The exam deadline was extended.";
        return RedirectToAction(nameof(Details), new { id = poll.Id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invalidate(Guid id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsOwnerOrAdmin(poll, user)) return Forbid();

        poll.State = PollState.Void;
        poll.IsDeleted = true;
        poll.UpdatedTime = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await LogOperationAsync(poll.Id, user!.Id, "Invalidated", "Poll has been invalidated (soft deleted).");

        return RedirectToAction(nameof(Index));
    }

    // ==================== Delete ====================

    [Authorize]
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsOwnerOrAdmin(poll, user)) return Forbid();

        return this.StackView(new DeleteViewModel { Poll = poll });
    }

    [Authorize]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll != null)
        {
            var user = await GetCurrentUserAsync();
            if (!IsOwnerOrAdmin(poll, user)) return Forbid();

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

    [Authorize]
    public async Task<IActionResult> AddQuestion(Guid? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(poll, user!)) return Forbid();

        return this.StackView(new AddQuestionViewModel { PollId = poll.Id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(AddQuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var poll = await context.Polls.FindAsync(model.PollId);
            if (poll == null || poll.IsDeleted) return NotFound();

            var user = await GetCurrentUserAsync();
            if (!IsCreatorOrAdmin(poll, user!)) return Forbid();

            var order = await context.Questions.Where(q => q.PollId == poll.Id).CountAsync();
            var question = new Question
            {
                PollId = poll.Id,
                Title = model.Title!,
                Explanation = model.Explanation,
                Type = model.Type,
                IsRequired = model.IsRequired,
                Order = order
            };
            context.Questions.Add(question);
            await context.SaveChangesAsync();

            // Add options (for choice-type questions)
            if (model.Options.Count != 0)
            {
                var displayOrder = 0;
                foreach (var opt in model.Options.Where(o => !string.IsNullOrWhiteSpace(o.Content)))
                {
                    context.Options.Add(new Option
                    {
                        QuestionId = question.Id,
                        Content = opt.Content.Trim(),
                        AllowCustomText = opt.AllowCustomText,
                        IsCorrect = opt.IsCorrect,
                        DisplayOrder = displayOrder++
                    });
                }
                await context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        return this.StackView(model);
    }

    [Authorize]
    public async Task<IActionResult> EditQuestion(int? id)
    {
        if (id == null) return NotFound();
        var question = await context.Questions
            .Include(q => q.Poll)
            .Include(q => q.Options)
            .SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(question.Poll!, user!)) return Forbid();

        return this.StackView(new EditQuestionViewModel
        {
            Id = question.Id,
            PollId = question.PollId,
            Title = question.Title,
            Explanation = question.Explanation,
            Type = question.Type,
            IsRequired = question.IsRequired,
            Options = question.Options?
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new QuestionOptionViewModel
                {
                    Id = o.Id,
                    Content = o.Content,
                    AllowCustomText = o.AllowCustomText,
                    IsCorrect = o.IsCorrect
                }).ToList() ?? []
        });
    }

    [Authorize]
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
            if (!IsCreatorOrAdmin(question.Poll!, user!)) return Forbid();

            question.Title = model.Title!;
            question.Explanation = model.Explanation;
            question.Type = model.Type;
            question.IsRequired = model.IsRequired;

            var existingOptions = question.Options?.ToList() ?? [];
            var modelOptions = model.Options;

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
                        IsCorrect = optModel.IsCorrect,
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
                        existingOpt.IsCorrect = optModel.IsCorrect;
                        existingOpt.DisplayOrder = displayOrder++;
                    }
                }
            }

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = question.PollId });
        }
        return this.StackView(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(question.Poll!, user!)) return Forbid();

        context.Questions.Remove(question);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = question.PollId });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveQuestionUp(int id)
    {
        var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(question.Poll!, user!)) return Forbid();

        var previousQuestion = await context.Questions
            .Where(q => q.PollId == question.PollId && q.Order < question.Order)
            .OrderByDescending(q => q.Order)
            .FirstOrDefaultAsync();

        if (previousQuestion != null)
        {
            (question.Order, previousQuestion.Order) = (previousQuestion.Order, question.Order);
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id = question.PollId });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveQuestionDown(int id)
    {
        var question = await context.Questions.Include(q => q.Poll).SingleOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsCreatorOrAdmin(question.Poll!, user!)) return Forbid();

        var nextQuestion = await context.Questions
            .Where(q => q.PollId == question.PollId && q.Order > question.Order)
            .OrderBy(q => q.Order)
            .FirstOrDefaultAsync();

        if (nextQuestion != null)
        {
            (question.Order, nextQuestion.Order) = (nextQuestion.Order, question.Order);
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id = question.PollId });
    }

    // ==================== Vote ====================

    // ReSharper disable once UnusedMember.Local
    [AllowAnonymous]
    private async Task<IActionResult> LegacyVote(Guid? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.Questions!.OrderBy(q => q.Order))
            .ThenInclude(q => q.Options!.OrderBy(o => o.DisplayOrder))
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        if (poll.State != PollState.Published)
        {
            return this.SimpleView(new PollMessageViewModel
            {
                Message = "This poll is not yet open.",
                SubMessage = "The creator has not yet published this poll. Please come back later.",
                Icon = "calendar",
                IconColor = "text-info"
            }, "PollMessage");
        }

        if (poll.Deadline <= DateTime.UtcNow)
        {
            return this.SimpleView(new PollMessageViewModel
            {
                Message = "This poll has ended.",
                SubMessage = "The deadline for this poll has passed. You can no longer submit your response.",
                Icon = "clock",
                IconColor = "text-warning",
                ButtonText = "View Results",
                ButtonUrl = Url.Action(nameof(Results), new { id = poll.Id })
            }, "PollMessage");
        }

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
            if (hasSubmitted)
            {
                return RedirectToAction(nameof(Results), new { id = poll.Id });
            }
        }

        return this.SimpleView(new VoteViewModel { PollId = poll.Id, Poll = poll });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    private async Task<IActionResult> LegacyVote(VoteViewModel model)
    {
        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == model.PollId && !p.IsDeleted);

        if (poll == null) return NotFound();

        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
        {
            return this.SimpleView(new PollMessageViewModel
            {
                Message = "This poll is not active.",
                SubMessage = "The poll might be a draft or has already closed.",
                Icon = "alert-circle",
                IconColor = "text-warning"
            }, "PollMessage");
        }

        var user = await GetCurrentUserAsync();
        if (user == null) return Challenge();
        if (!await CanUserAccessPoll(poll, user))
        {
            return Forbid();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        var hasSubmitted = await context.Submissions.AnyAsync(s => s.UserId == user.Id && s.PollId == poll.Id);
        if (hasSubmitted)
        {
            return RedirectToAction(nameof(Results), new { id = poll.Id });
        }

        // Validate custom texts (non-empty when checked)
        foreach (var question in poll.Questions ?? [])
        {
            switch (question.Type)
            {
                case QuestionType.SingleChoice:
                    if (model.SelectedOptions.TryGetValue(question.Id, out var singleVal) &&
                        int.TryParse(singleVal, out var singleOptId))
                    {
                        var option = question.Options?.FirstOrDefault(o => o.Id == singleOptId);
                        if (option != null && option.AllowCustomText)
                        {
                            if (!model.CustomTexts.TryGetValue(question.Id, out var customText) || string.IsNullOrWhiteSpace(customText))
                            {
                                ModelState.AddModelError(string.Empty, "Please provide additional text for the selected option.");
                            }
                        }
                    }
                    break;

                case QuestionType.MultipleChoice:
                    if (model.SelectedOptions.TryGetValue(question.Id, out var multiVal) && !string.IsNullOrWhiteSpace(multiVal))
                    {
                        var optionIds = multiVal.Split(',')
                            .Where(s => int.TryParse(s, out _))
                            .Select(int.Parse)
                            .ToList();

                        foreach (var optId in optionIds)
                        {
                            var option = question.Options?.FirstOrDefault(o => o.Id == optId);
                            if (option != null && option.AllowCustomText)
                            {
                                if (!model.CustomTexts.TryGetValue(question.Id, out var customText) || string.IsNullOrWhiteSpace(customText))
                                {
                                    ModelState.AddModelError(string.Empty, "Please provide additional text for the selected option.");
                                }
                            }
                        }
                    }
                    break;
            }
        }

        if (!ModelState.IsValid)
        {
            model.Poll = poll;
            return this.SimpleView(model);
        }

        // Create submission
        var submission = new Submission
        {
            PollId = poll.Id,
            UserId = poll.IsAnonymous ? null : user.Id,
            IpAddress = poll.IsAnonymous ? null : ip,
            BrowserFingerprint = poll.IsAnonymous ? null : model.BrowserFingerprint
        };
        context.Submissions.Add(submission);
        await context.SaveChangesAsync();

        // Create answers
        foreach (var question in poll.Questions ?? [])
        {
            switch (question.Type)
            {
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

        return RedirectToAction(nameof(Results), new { id = poll.Id });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Vote(Guid? id)
    {
        if (id == null) return NotFound();
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (poll == null) return NotFound();
        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
        {
            return this.SimpleView(new PollMessageViewModel
            {
                Message = poll.State == PollState.Draft ? "This poll is not yet open." : "This exam is not active.",
                SubMessage = poll.State == PollState.Draft
                    ? "The creator has not yet published this poll. Please come back later."
                    : "The exam may not be published yet or its availability window has ended.",
                Icon = "clock",
                IconColor = "text-warning"
            }, "PollMessage");
        }

        var user = await GetCurrentUserAsync();
        if (user == null) return Challenge();
        if (!await CanUserAccessPoll(poll, user))
        {
            return Forbid();
        }

        Submission? attempt = null;
        if (!poll.IsAnonymous)
        {
            attempt = await context.Submissions
                .Where(s => s.PollId == poll.Id && s.UserId == user.Id && s.Status == AttemptStatus.InProgress)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();
            if (attempt != null && attempt.ExpiresAt <= DateTime.UtcNow)
            {
                await attemptService.FinalizeAsync(attempt, expired: true);
                return RedirectToAction(nameof(AttemptResult), new { id = attempt.Id });
            }

            if (!poll.AllowRepeatedSubmissions)
            {
                var completed = await context.Submissions
                    .Where(s => s.PollId == poll.Id && s.UserId == user.Id && s.Status != AttemptStatus.InProgress)
                    .OrderByDescending(s => s.SubmittedAt)
                    .FirstOrDefaultAsync();
                if (completed != null) return RedirectToAction(nameof(AttemptResult), new { id = completed.Id });
            }
        }

        attempt ??= await attemptService.StartAttemptAsync(
            poll, user, HttpContext.Connection.RemoteIpAddress?.ToString(), null);
        attempt = await attemptService.GetAttemptAsync(attempt.Id);
        return this.SimpleView(new VoteViewModel
        {
            PollId = poll.Id,
            Poll = poll,
            AttemptId = attempt!.Id,
            Attempt = attempt
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(VoteViewModel model)
    {
        var attempt = await attemptService.GetAttemptAsync(model.AttemptId);
        if (attempt?.Poll == null) return NotFound();
        var user = await GetCurrentUserAsync();
        if (attempt.UserId != null && attempt.UserId != user?.Id) return Forbid();

        foreach (var question in attempt.AttemptQuestions ?? [])
        {
            var ids = model.SelectedOptions?.TryGetValue(question.Id, out var value) == true &&
                      !string.IsNullOrWhiteSpace(value)
                ? value.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(x => int.TryParse(x, out _)).Select(int.Parse)
                : [];
            await attemptService.SaveSelectionsAsync(attempt, question.Id, ids);
        }
        await attemptService.FinalizeAsync(attempt, expired: attempt.ExpiresAt <= DateTime.UtcNow);
        return RedirectToAction(nameof(AttemptResult), new { id = attempt.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAttemptAnswer([FromBody] SaveAttemptAnswerRequest model)
    {
        var attempt = await attemptService.GetAttemptAsync(model.AttemptId);
        if (attempt == null) return NotFound();
        var user = await GetCurrentUserAsync();
        if (attempt.UserId == null || attempt.UserId != user?.Id) return Forbid();
        if (attempt.Status != AttemptStatus.InProgress || attempt.ExpiresAt <= DateTime.UtcNow)
        {
            await attemptService.FinalizeAsync(attempt, expired: true);
            return Conflict(new { expired = true });
        }
        await attemptService.SaveSelectionsAsync(attempt, model.AttemptQuestionId, model.OptionIds);
        return Ok(new { saved = true });
    }

    public async Task<IActionResult> AttemptResult(int id)
    {
        var attempt = await attemptService.GetAttemptAsync(id);
        if (attempt?.Poll == null) return NotFound();
        var user = await GetCurrentUserAsync();
        var ownsAttempt = attempt.UserId != null && attempt.UserId == user?.Id;
        if (!ownsAttempt && !await CanViewPollManagementAsync(attempt.Poll, user)) return Forbid();
        if (attempt.Status == AttemptStatus.InProgress)
        {
            if (attempt.ExpiresAt > DateTime.UtcNow && ownsAttempt)
                return RedirectToAction(nameof(Vote), new { id = attempt.PollId });
            await attemptService.FinalizeAsync(attempt, expired: true);
            attempt = await attemptService.GetAttemptAsync(id);
        }

        var results = (attempt!.AttemptQuestions ?? []).Select(question =>
        {
            var selected = (attempt.AttemptSelections ?? [])
                .Where(s => s.AttemptQuestionId == question.Id)
                .Select(s => s.AttemptOptionId).ToList();
            var score = ExamAttemptService.ScoreQuestion(attempt, question, selected);
            var correct = (question.Options ?? []).Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
            return new AttemptQuestionResult
            {
                Question = question,
                SelectedOptionIds = selected,
                Score = score,
                IsFullyCorrect = selected.ToHashSet().SetEquals(correct) && correct.Count > 0
            };
        }).ToList();
        return this.SimpleView(new AttemptResultViewModel { Poll = attempt.Poll!, Attempt = attempt, Questions = results });
    }

    // ==================== Results ====================

    [AllowAnonymous]
    public async Task<IActionResult> Results(Guid? id)
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
        var isManager = await CanViewPollManagementAsync(poll, user);

        // Check visibility
        var isAuthorized = true;
        switch (poll.Visibility)
        {
            case ResultVisibility.CreatorOnly when !isManager:
                isAuthorized = false;
                break;
            case ResultVisibility.Participants when !isManager:
                if (user == null) return Challenge();
                var hasSubmitted = await context.Submissions.AnyAsync(s => s.UserId == user.Id && s.PollId == poll.Id);
                if (!hasSubmitted) isAuthorized = false;
                break;
            case ResultVisibility.Public:
                break;
        }

        if (!isAuthorized)
        {
            return this.StackView(new ResultsViewModel
            {
                Poll = poll,
                IsAuthorized = false
            });
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
                TotalAnswers = qAnswers.Select(a => a.SubmissionId).Distinct().Count()
            };

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

            questionResults.Add(qResult);
        }

        int pendingCount = 0;
        int eligibleCount = 0;
        if (isManager && poll.AccessType is AccessType.RoleBased or AccessType.Assigned)
        {
            var (eligible, pending) = await GetEligibleAndPendingUsers(poll);
            eligibleCount = eligible.Count;
            pendingCount = pending.Count;
        }

        return this.StackView(new ResultsViewModel
        {
            Poll = poll,
            TotalSubmissions = totalSubmissions,
            QuestionResults = questionResults,
            CanExport = isManager,
            PendingVotersCount = pendingCount,
            EligibleVotersCount = eligibleCount
        });
    }

    // ==================== Export CSV ====================

    [Authorize]
    public async Task<IActionResult> ExportCsv(Guid id)
    {
        var poll = await context.Polls
            .Include(p => p.Questions!.OrderBy(q => q.Order))
            .ThenInclude(q => q.Options)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!await CanViewPollManagementAsync(poll, user)) return Forbid();

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
        var headers = new List<string>();
        if (!poll.IsAnonymous)
        {
            headers.Add("Submission ID");
            headers.Add("User");
            headers.Add("IP Address");
        }
        headers.Add("Submit Time");

        foreach (var q in questions)
        {
            headers.Add($"\"{q.Title.Replace("\"", "\"\"")}\"");
        }
        sb.AppendLine(string.Join(",", headers));

        // Rows
        foreach (var submission in submissions)
        {
            var row = new List<string>();
            if (!poll.IsAnonymous)
            {
                row.Add(submission.Id.ToString());
                row.Add($"\"{(submission.User?.DisplayName ?? "Anonymous").Replace("\"", "\"\"")}\"");
                row.Add($"\"{submission.IpAddress}\"");
            }
            row.Add(submission.SubmitTime.ToString("u"));

            foreach (var q in questions)
            {
                var answers = submission.Answers?.Where(a => a.QuestionId == q.Id).ToList() ?? [];
                
                var parts = answers.Select(a =>
                    {
                        var content = a.Option?.Content ?? "";
                        if (!string.IsNullOrWhiteSpace(a.CustomText))
                            content += $" ({a.CustomText})";
                        return content;
                    });
                    row.Add($"\"{string.Join("; ", parts).Replace("\"", "\"\"")}\"");
                
            }

            sb.AppendLine(string.Join(",", row));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"poll-{poll.Id}-results.csv");
    }

    [Authorize]
    public async Task<IActionResult> Submissions(Guid id)
    {
        var poll = await context.Polls
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!await CanViewPollManagementAsync(poll, user)) return Forbid();

        var submissions = await context.Submissions
            .Include(s => s.User)
            .Where(s => s.PollId == id)
            .OrderByDescending(s => s.SubmitTime)
            .ToListAsync();

        foreach (var expired in submissions.Where(s => s.Status == AttemptStatus.InProgress && s.ExpiresAt <= DateTime.UtcNow))
            await attemptService.FinalizeAsync(expired, expired: true);
        submissions = await context.Submissions.Include(s => s.User).Where(s => s.PollId == id)
            .OrderByDescending(s => s.SubmitTime).ToListAsync();

        var summaries = submissions.Where(s => s.Status != AttemptStatus.InProgress)
            .GroupBy(s => s.UserId)
            .Select(group => new EmployeeAttemptSummary
            {
                User = group.First().User,
                AttemptCount = group.Count(),
                HighestScore = group.Max(s => s.Score),
                Attempts = group.OrderBy(s => s.AttemptNumber).ToList()
            }).OrderBy(s => s.User?.DisplayName).ToList();

        return this.StackView(new SubmissionsViewModel
        {
            Poll = poll,
            Submissions = submissions,
            EmployeeSummaries = summaries
        });
    }

    [Authorize]
    public async Task<IActionResult> SubmissionDetail(int id)
    {
        var submission = await context.Submissions
            .Include(s => s.User)
            .Include(s => s.Answers!)
            .ThenInclude(a => a.Option)
            .SingleOrDefaultAsync(s => s.Id == id);

        if (submission == null) return NotFound();

        var poll = await context.Polls
            .Include(p => p.Questions!)
            .ThenInclude(q => q.Options)
            .SingleOrDefaultAsync(p => p.Id == submission.PollId && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!await CanViewPollManagementAsync(poll, user)) return Forbid();

        return this.StackView(new SubmissionDetailViewModel
        {
            Poll = poll,
            Submission = submission,
            Questions = poll.Questions?.OrderBy(q => q.Order).ToList() ?? []
        });
    }

    [Authorize]
    public async Task<IActionResult> ExportPendingUsers(Guid id)
    {
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!await CanViewPollManagementAsync(poll, user)) return Forbid();

        var (_, pendingUsers) = await GetEligibleAndPendingUsers(poll);

        var sb = new StringBuilder();
        sb.AppendLine("User ID,Display Name,Email,UserName");

        foreach (var u in pendingUsers)
        {
            sb.AppendLine($"{u.Id},\"{u.DisplayName.Replace("\"", "\"\"")}\",\"{u.Email}\",\"{u.UserName}\"");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"poll-{poll.Id}-pending-users.csv");
    }

    [Authorize]
    public async Task<IActionResult> VoterStatus(Guid id)
    {
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!await CanViewPollManagementAsync(poll, user)) return Forbid();

        var (eligibleUsers, _) = await GetEligibleAndPendingUsers(poll);

        var submissions = await context.Submissions
            .Where(s => s.PollId == poll.Id && s.UserId != null && s.Status != AttemptStatus.InProgress)
            .GroupBy(s => s.UserId!)
            .Select(group => new { UserId = group.Key, SubmitTime = group.Max(s => s.SubmitTime) })
            .ToDictionaryAsync(s => s.UserId, s => s.SubmitTime);

        var userStatusList = eligibleUsers.Select(u => new UserStatus
        {
            User = u,
            HasVoted = submissions.ContainsKey(u.Id),
            VoteTime = submissions.TryGetValue(u.Id, out var time) ? time : null
        }).ToList();

        return this.StackView(new VoterStatusViewModel
        {
            Poll = poll,
            Users = userStatusList
        });
    }

    // ==================== Send Reminder ====================

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendReminder(Guid id)
    {
        var poll = await context.Polls
            .Include(p => p.RoleRestrictions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (poll == null) return NotFound();

        var user = await GetCurrentUserAsync();
        if (!IsOwnerOrAdmin(poll, user)) return Forbid();

        if (poll.State != PollState.Published || poll.Deadline <= DateTime.UtcNow)
            return BadRequest("Can only send reminders for active polls.");

        if (poll.AccessType != AccessType.RoleBased)
            return BadRequest("Reminders are only supported for RoleBased polls.");

        // Queue background job for sending reminders
        taskQueue.QueueWithDependency<TemplateDbContext>(
            $"reminder-poll-{poll.Id}",
            $"Send reminders for poll: {poll.Title}",
            async dbContext =>
            {
                // Get users who should participate but haven't
                var targetUserIds = new HashSet<string>();

                if (poll.RoleRestrictions != null)
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

                // Remove those who already submitted
                var submittedUserIds = await dbContext.Submissions
                    .Where(s => s.PollId == poll.Id && s.UserId != null && s.Status != AttemptStatus.InProgress)
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

    public async Task<IActionResult> ManageShares(Guid id)
    {
        var poll = await context.Polls.Include(p => p.Shares!).ThenInclude(s => s.SharedWithUser)
            .SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (poll == null) return NotFound();
        var user = await GetCurrentUserAsync();
        if (poll.CreatedById != user?.Id && !HasManagePermission()) return Forbid();
        return this.StackView(new ManageSharesViewModel
        {
            Poll = poll,
            ExistingShares = poll.Shares?.ToList() ?? [],
            AvailableUsers = await userManager.Users.OrderBy(u => u.DisplayName).ToListAsync(),
            AvailableRoles = await roleManager.Roles.OrderBy(r => r.Name).ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddShare(Guid id, AddPollShareViewModel model)
    {
        var poll = await context.Polls.FindAsync(id);
        if (poll == null || poll.IsDeleted) return NotFound();
        var user = await GetCurrentUserAsync();
        if (poll.CreatedById != user?.Id && !HasManagePermission()) return Forbid();
        var userId = string.IsNullOrWhiteSpace(model.TargetUserId) ? null : model.TargetUserId;
        var roleId = string.IsNullOrWhiteSpace(model.TargetRoleId) ? null : model.TargetRoleId;
        if ((userId == null) == (roleId == null)) return BadRequest("Select exactly one employee or role.");
        if (userId != null && await userManager.FindByIdAsync(userId) == null) return BadRequest("Employee not found.");
        if (roleId != null && await roleManager.FindByIdAsync(roleId) == null) return BadRequest("Role not found.");
        var exists = await context.PollShares.AnyAsync(s => s.PollId == id &&
            ((userId != null && s.SharedWithUserId == userId) || (roleId != null && s.SharedWithRoleId == roleId)));
        if (!exists)
        {
            context.PollShares.Add(new PollShare
            {
                PollId = id,
                SharedWithUserId = userId,
                SharedWithRoleId = roleId,
                Permission = model.Permission
            });
            await context.SaveChangesAsync();
            await LogOperationAsync(id, user!.Id, "Shared", $"Exam shared with {(userId ?? roleId)} as {model.Permission}.");
        }
        return RedirectToAction(nameof(ManageShares), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveShare(Guid id)
    {
        var share = await context.PollShares.Include(s => s.Poll).SingleOrDefaultAsync(s => s.Id == id);
        if (share?.Poll == null) return NotFound();
        var user = await GetCurrentUserAsync();
        if (share.Poll.CreatedById != user?.Id && !HasManagePermission()) return Forbid();
        var pollId = share.PollId;
        context.PollShares.Remove(share);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(ManageShares), new { id = pollId });
    }
}
