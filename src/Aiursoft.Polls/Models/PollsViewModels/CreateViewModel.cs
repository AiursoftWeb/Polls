using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;
using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Services;
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

    [Display(Name = "Anonymous Poll")]
    public bool IsAnonymous { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Questions per Attempt (0 = all)")]
    public int QuestionsPerAttempt { get; set; }

    [Display(Name = "Shuffle questions")]
    public bool ShuffleQuestions { get; set; } = true;

    [Display(Name = "Shuffle options")]
    public bool ShuffleOptions { get; set; } = true;

    [Display(Name = "Allow repeated attempts")]
    public bool AllowRepeatedSubmissions { get; set; } = true;

    [Range(1, 10080)]
    public int DurationMinutes { get; set; } = 60;

    [Range(typeof(decimal), "0", "1000000")]
    public decimal FullScore { get; set; } = 4;
    [Range(typeof(decimal), "0", "1000000")]
    public decimal PartialScore { get; set; } = 2;
    [Range(typeof(decimal), "0", "1000000")]
    public decimal OverSelectionScore { get; set; }
    [Range(typeof(decimal), "0", "1000000")]
    public decimal PassingScore { get; set; } = 90;
    [Required, MaxLength(2000)] public string PassMessage { get; set; } = "You passed the exam.";
    [Required, MaxLength(2000)] public string FailMessage { get; set; } = "Unfortunately, you did not pass the exam.";

    [Required]
    [Display(Name = "Deadline")]
    public DateTime Deadline { get; set; } = DateTime.UtcNow.AddDays(7).ToSecondPrecision();

    [Display(Name = "Allowed Roles")]
    public List<string> SelectedRoles { get; set; } = [];

    public List<IdentityRole> AllRoles { get; set; } = [];
    public List<string> SelectedUserIds { get; set; } = [];
    public List<string> SelectedAssignmentRoleIds { get; set; } = [];
    public List<User> AllUsers { get; set; } = [];
}
