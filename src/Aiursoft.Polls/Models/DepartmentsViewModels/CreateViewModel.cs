using System.ComponentModel.DataAnnotations;
using Aiursoft.UiStack.Layout;

namespace Aiursoft.Polls.Models.DepartmentsViewModels;

public class CreateViewModel : UiStackLayoutViewModel
{
    public CreateViewModel()
    {
        PageTitle = "Create Department";
    }

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "Department Name")]
    [MaxLength(100, ErrorMessage = "The {0} must be at max {1} characters long.")]
    public string? Name { get; set; }
}
