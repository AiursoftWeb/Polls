using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.Polls.Entities;

public class PollAssignment
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    [ForeignKey(nameof(PollId))] public Poll? Poll { get; set; }
    [MaxLength(64)] public string? AssignedUserId { get; set; }
    [ForeignKey(nameof(AssignedUserId))] public User? AssignedUser { get; set; }
    [MaxLength(450)] public string? AssignedRoleId { get; set; }
    [ForeignKey(nameof(AssignedRoleId))] public IdentityRole? AssignedRole { get; set; }
    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
