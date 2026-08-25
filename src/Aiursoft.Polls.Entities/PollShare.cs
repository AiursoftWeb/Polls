using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Aiursoft.Polls.Entities;

public class PollShare
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    [ForeignKey(nameof(PollId))] public Poll? Poll { get; set; }
    [MaxLength(64)] public string? SharedWithUserId { get; set; }
    [ForeignKey(nameof(SharedWithUserId))] public User? SharedWithUser { get; set; }
    [MaxLength(450)] public string? SharedWithRoleId { get; set; }
    [ForeignKey(nameof(SharedWithRoleId))] public IdentityRole? SharedWithRole { get; set; }
    public SharePermission Permission { get; set; }
    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
