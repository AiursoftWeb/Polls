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

    public Guid Id { get; set; }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Title")]
    [MaxLength(200, ErrorMessage = "The {0} must be at max {1} characters long.")]
    public string? Title { get; set; }

    [Display(Name = "Description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Access Type")]
    public AccessType AccessType { get; set; }

    [Display(Name = "Result Visibility")]
    public ResultVisibility Visibility { get; set; }

    [Display(Name = "Anonymous Poll")]
    public bool IsAnonymous { get; set; }

    [Range(0, int.MaxValue)] public int QuestionsPerAttempt { get; set; }
    [Display(Name = "Shuffle questions")]
    public bool ShuffleQuestions { get; set; }

    [Display(Name = "Shuffle options")]
    public bool ShuffleOptions { get; set; }

    [Display(Name = "Allow repeated attempts")]
    public bool AllowRepeatedSubmissions { get; set; }
    [Range(1, 10080)] public int DurationMinutes { get; set; }
    [Range(typeof(decimal), "0", "1000000")] public decimal FullScore { get; set; }
    [Range(typeof(decimal), "0", "1000000")] public decimal PartialScore { get; set; }
    [Range(typeof(decimal), "0", "1000000")] public decimal OverSelectionScore { get; set; }
    [Range(typeof(decimal), "0", "1000000")] public decimal PassingScore { get; set; }
    [Required, MaxLength(2000)] public string PassMessage { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string FailMessage { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Deadline")]
    public DateTime Deadline { get; set; }

    [Display(Name = "Allowed Roles")]
    public List<string> SelectedRoles { get; set; } = [];

    public List<IdentityRole> AllRoles { get; set; } = [];
    public List<string> SelectedUserIds { get; set; } = [];
    public List<string> SelectedAssignmentRoleIds { get; set; } = [];
    public List<User> AllUsers { get; set; } = [];
}
