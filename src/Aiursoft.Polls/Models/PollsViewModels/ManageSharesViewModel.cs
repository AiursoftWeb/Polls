using Aiursoft.Polls.Entities;
using Aiursoft.UiStack.Layout;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class ManageSharesViewModel : UiStackLayoutViewModel
{
    public ManageSharesViewModel() => PageTitle = "Manage Exam Shares";
    public required Poll Poll { get; set; }
    public List<PollShare> ExistingShares { get; set; } = [];
    public List<User> AvailableUsers { get; set; } = [];
    public List<IdentityRole> AvailableRoles { get; set; } = [];
}

public class AddPollShareViewModel
{
    public string? TargetUserId { get; set; }
    public string? TargetRoleId { get; set; }
    public SharePermission Permission { get; set; }
}
