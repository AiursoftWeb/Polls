using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create Poll";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Title")]
    [MaxLength(200, ErrorMessage = "The {0} must be at max {1} characters long.")]
    public string? Title { get; set; }

    [Display(Name = "Description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Access Type")]
    public AccessType AccessType { get; set; } = AccessType.RegisteredOnly;

    [Display(Name = "Result Visibility")]
    public ResultVisibility Visibility { get; set; } = ResultVisibility.CreatorOnly;

    [Required]
    [Display(Name = "Deadline")]
    public DateTime Deadline { get; set; } = DateTime.UtcNow.AddDays(7);

    [Display(Name = "Allowed Roles")]
    public List<string> SelectedRoles { get; set; } = [];

    public List<IdentityRole> AllRoles { get; set; } = [];
}
