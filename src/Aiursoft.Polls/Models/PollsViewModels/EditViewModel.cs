using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.Polls.Models.PollsViewModels;

public class EditViewModel : UiStackLayoutViewModel
{
    public EditViewModel()
    {
        PageTitle = "Edit Poll";
    }

    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Title")]
    [MaxLength(200, ErrorMessage = "The {0} must be at max {1} characters long.")]
    public string? Title { get; set; }

    [Display(Name = "Content/Description")]
    public string? Content { get; set; }

    [Display(Name = "Anonymous Voting")]
    public bool IsAnonymous { get; set; }

    [Display(Name = "Public Poll (Anyone can vote)")]
    public bool IsPublic { get; set; }

    [Required]
    [Display(Name = "Deadline")]
    public DateTime Deadline { get; set; }

    [Display(Name = "Allowed Roles")]
    public List<string> SelectedRoles { get; set; } = [];

    public List<IdentityRole> AllRoles { get; set; } = [];
}
