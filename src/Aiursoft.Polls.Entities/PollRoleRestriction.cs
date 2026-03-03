using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.Polls.Entities;

[ExcludeFromCodeCoverage]
public class PollRoleRestriction
{
    [Key]
    public int Id { get; set; }

    public int PollId { get; set; }

    [ForeignKey(nameof(PollId))]
    public Poll? Poll { get; set; }

    [Required]
    public required string RoleId { get; set; }

    [ForeignKey(nameof(RoleId))]
    public IdentityRole? Role { get; set; }
}
