using System.Security.Cryptography;
using Aiursoft.Polls.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Services;

public class ExamAttemptService(TemplateDbContext context)
{
    public async Task<Submission> StartAttemptAsync(Poll poll, User? user, string? ipAddress, string? fingerprint)
    {
        var questions = await context.Questions
            .Where(q => q.PollId == poll.Id)
            .Include(q => q.Options)
            .OrderBy(q => q.Order)
            .ToListAsync();

        var sampleCount = poll.QuestionsPerAttempt <= 0
            ? questions.Count
            : Math.Min(poll.QuestionsPerAttempt, questions.Count);
        var sampled = Shuffle(questions).Take(sampleCount).ToList();
        if (!poll.ShuffleQuestions)
        {
            sampled = sampled.OrderBy(q => q.Order).ToList();
        }

        var attemptNumber = user == null
            ? 1
            : await context.Submissions.CountAsync(s => s.PollId == poll.Id && s.UserId == user.Id) + 1;
        var now = DateTime.UtcNow;
        var attempt = new Submission
        {
            PollId = poll.Id,
            UserId = user?.Id,
            IpAddress = poll.IsAnonymous ? null : ipAddress,
            BrowserFingerprint = poll.IsAnonymous ? null : fingerprint,
            AttemptNumber = attemptNumber,
            StartedAt = now,
            ExpiresAt = now.AddMinutes(poll.DurationMinutes),
            SubmitTime = now,
            Status = AttemptStatus.InProgress,
            FullScore = poll.FullScore,
            PartialScore = poll.PartialScore,
            OverSelectionScore = poll.OverSelectionScore,
            PassingScore = poll.PassingScore,
            MaxScore = sampleCount * poll.FullScore
        };
        context.Submissions.Add(attempt);
        await context.SaveChangesAsync();

        for (var questionIndex = 0; questionIndex < sampled.Count; questionIndex++)
        {
            var source = sampled[questionIndex];
            var snapshot = new AttemptQuestion
            {
                SubmissionId = attempt.Id,
                SourceQuestionId = source.Id,
                Title = source.Title,
                Explanation = source.Explanation,
                Type = source.Type,
                IsRequired = source.IsRequired,
                DisplayOrder = questionIndex
            };

            var options = source.Options?.OrderBy(o => o.DisplayOrder).ToList() ?? [];
            if (poll.ShuffleOptions)
            {
                options = Shuffle(options);
            }

            snapshot.Options = options.Select((option, optionIndex) => new AttemptOption
                {
                    SourceOptionId = option.Id,
                    Content = option.Content,
                    IsCorrect = option.IsCorrect,
                    DisplayOrder = optionIndex
                }).ToList();
            context.AttemptQuestions.Add(snapshot);
        }

        await context.SaveChangesAsync();
        return await GetAttemptAsync(attempt.Id) ?? attempt;
    }

    public Task<Submission?> GetAttemptAsync(int id) => context.Submissions
        .Include(s => s.Poll)
        .Include(s => s.User)
        .Include(s => s.AttemptQuestions!.OrderBy(q => q.DisplayOrder))
            .ThenInclude(q => q.Options!.OrderBy(o => o.DisplayOrder))
        .Include(s => s.AttemptSelections)
        .SingleOrDefaultAsync(s => s.Id == id);

    public async Task SaveSelectionsAsync(Submission attempt, int attemptQuestionId, IEnumerable<int> optionIds)
    {
        if (attempt.Status != AttemptStatus.InProgress || attempt.ExpiresAt <= DateTime.UtcNow)
        {
            await FinalizeAsync(attempt, expired: true);
            return;
        }

        var question = attempt.AttemptQuestions?.SingleOrDefault(q => q.Id == attemptQuestionId)
                       ?? throw new InvalidOperationException("Question is not part of this attempt.");
        var validIds = question.Options?.Select(o => o.Id).ToHashSet() ?? [];
        var selectedIds = optionIds.Distinct().Where(validIds.Contains).ToList();
        if (question.Type == QuestionType.SingleChoice && selectedIds.Count > 1)
        {
            throw new InvalidOperationException("A single-choice question accepts one option only.");
        }

        var existing = await context.AttemptSelections
            .Where(x => x.SubmissionId == attempt.Id && x.AttemptQuestionId == question.Id)
            .ToListAsync();
        context.AttemptSelections.RemoveRange(existing);
        context.AttemptSelections.AddRange(selectedIds.Select(optionId => new AttemptSelection
        {
            SubmissionId = attempt.Id,
            AttemptQuestionId = question.Id,
            AttemptOptionId = optionId
        }));
        await context.SaveChangesAsync();
    }

    public async Task FinalizeAsync(Submission attempt, bool expired = false)
    {
        if (attempt.Status != AttemptStatus.InProgress) return;

        var fresh = await GetAttemptAsync(attempt.Id) ?? throw new InvalidOperationException("Attempt was not found.");
        decimal total = 0;
        foreach (var question in fresh.AttemptQuestions ?? [])
        {
            var selected = (fresh.AttemptSelections ?? [])
                .Where(s => s.AttemptQuestionId == question.Id)
                .Select(s => s.AttemptOptionId)
                .ToHashSet();
            var correct = (question.Options ?? []).Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();

            if (selected.Count == 0 || correct.Count == 0)
            {
                continue;
            }

            if (selected.Any(id => !correct.Contains(id)))
            {
                total += fresh.OverSelectionScore;
            }
            else if (selected.SetEquals(correct))
            {
                total += fresh.FullScore;
            }
            else
            {
                total += fresh.PartialScore;
            }
        }

        fresh.Score = total;
        fresh.Passed = total >= fresh.PassingScore;
        fresh.Status = expired || fresh.ExpiresAt <= DateTime.UtcNow ? AttemptStatus.Expired : AttemptStatus.Submitted;
        fresh.SubmittedAt = DateTime.UtcNow;
        fresh.SubmitTime = fresh.SubmittedAt.Value;
        await context.SaveChangesAsync();
    }

    public static decimal ScoreQuestion(Submission attempt, AttemptQuestion question, IEnumerable<int> selectedOptionIds)
    {
        var selected = selectedOptionIds.ToHashSet();
        var correct = (question.Options ?? []).Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
        if (selected.Count == 0 || correct.Count == 0) return 0;
        if (selected.Any(id => !correct.Contains(id))) return attempt.OverSelectionScore;
        return selected.SetEquals(correct) ? attempt.FullScore : attempt.PartialScore;
    }

    private static List<T> Shuffle<T>(IEnumerable<T> source)
    {
        var result = source.ToList();
        for (var i = result.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }
}
