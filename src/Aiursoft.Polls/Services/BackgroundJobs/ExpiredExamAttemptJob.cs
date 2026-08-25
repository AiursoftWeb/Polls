using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.Polls.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Services.BackgroundJobs;

public class ExpiredExamAttemptJob(
    TemplateDbContext context,
    ExamAttemptService attemptService,
    ILogger<ExpiredExamAttemptJob> logger) : IBackgroundJob
{
    public string Name => "Expired Exam Attempt Finalizer";
    public string Description => "Automatically grades and submits exam attempts whose server-side deadline has passed.";

    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        var expiredIds = await context.Submissions
            .Where(s => s.Status == AttemptStatus.InProgress && s.ExpiresAt <= now)
            .Select(s => s.Id)
            .ToListAsync();
        foreach (var id in expiredIds)
        {
            var attempt = await attemptService.GetAttemptAsync(id);
            if (attempt != null) await attemptService.FinalizeAsync(attempt, expired: true);
        }
        if (expiredIds.Count > 0)
            logger.LogInformation("Finalized {Count} expired exam attempt(s).", expiredIds.Count);
    }
}
