using Emi.Qms.Api.Calendar;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationEscalationService(
    WorkItemEscalationStore escalationStore,
    TimeProvider timeProvider,
    IOptionsMonitor<NotificationOptions> options,
    ILogger<NotificationEscalationService> logger)
{
    private const string CandidateEvaluationFailureCode = "ESCALATION_CANDIDATE_EVALUATION_FAILED";
    private static readonly EventId CandidateEvaluationFailedEvent =
        new(1, "NotificationEscalationCandidateEvaluationFailed");

    public async Task<NotificationEscalationSummary> EvaluateAsync(CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue.Escalation;
        if (!currentOptions.Enabled)
        {
            return new NotificationEscalationSummary(0, 0, 0, 0);
        }

        var resolved = await escalationStore.ResolveClosedOrUndatedWorkItemsAsync(cancellationToken);
        var candidates = await escalationStore.ReadOpenCandidatesAsync(currentOptions.MaxBatchSize, cancellationToken);
        if (candidates.Count == 0)
        {
            return new NotificationEscalationSummary(0, 0, 0, resolved);
        }

        var minDate = candidates.Min(candidate => candidate.DueDate).AddDays(-45);
        var maxDate = candidates.Max(candidate => candidate.DueDate).AddDays(45);
        var holidays = await escalationStore.ReadHolidaysAsync(minDate, maxDate, cancellationToken);
        var calculator = new BusinessDayCalculator(holidays);
        var localToday = GetLocalToday(currentOptions.TimeZone);
        var notificationCount = 0;
        var deliveryCount = 0;
        var failedCandidateCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var nextCheck = CalculateNextCheckAtUtc(candidate.DueDate, calculator, localToday, currentOptions.TimeZone);
                await escalationStore.UpsertActiveEscalationAsync(candidate, nextCheck, cancellationToken);
                var level = DetermineDueLevel(candidate, calculator, localToday);
                if (level is null)
                {
                    continue;
                }

                var result = await escalationStore.CreateEscalationAsync(candidate, level, currentOptions, cancellationToken);
                notificationCount += result.NotificationCount;
                deliveryCount += result.DeliveryCount;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failedCandidateCount += 1;
            }
        }

        if (failedCandidateCount > 0)
        {
            logger.LogWarning(
                CandidateEvaluationFailedEvent,
                "Notification escalation candidate evaluation completed with failures. FailureCode={FailureCode} FailedCandidateCount={FailedCandidateCount}.",
                CandidateEvaluationFailureCode,
                failedCandidateCount);
        }

        return new NotificationEscalationSummary(candidates.Count, notificationCount, deliveryCount, resolved);
    }

    internal static string? DetermineDueLevel(
        WorkItemEscalationCandidate candidate,
        BusinessDayCalculator calculator,
        DateOnly today)
    {
        var l0Date = calculator.GetPreviousBusinessDay(candidate.DueDate);
        var l2Date = calculator.AddBusinessDays(candidate.DueDate, 2);
        var l3Date = calculator.AddBusinessDays(candidate.DueDate, 3);

        if (today >= l3Date && candidate.L3SentAtUtc is null)
        {
            return WorkItemEscalationLevels.L3;
        }

        if (today >= l2Date && candidate.L2SentAtUtc is null)
        {
            return WorkItemEscalationLevels.L2;
        }

        if (today > candidate.DueDate && candidate.L1SentAtUtc is null)
        {
            return WorkItemEscalationLevels.L1;
        }

        if (today >= l0Date && today <= candidate.DueDate && candidate.L0SentAtUtc is null)
        {
            return WorkItemEscalationLevels.L0;
        }

        return null;
    }

    private DateOnly GetLocalToday(string timeZoneId)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    private DateTimeOffset? CalculateNextCheckAtUtc(
        DateOnly dueDate,
        BusinessDayCalculator calculator,
        DateOnly today,
        string timeZoneId)
    {
        var candidates = new[]
            {
                calculator.GetPreviousBusinessDay(dueDate),
                dueDate.AddDays(1),
                calculator.AddBusinessDays(dueDate, 2),
                calculator.AddBusinessDays(dueDate, 3)
            }
            .Where(date => date >= today)
            .Order()
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        var timeZone = ResolveTimeZone(timeZoneId);
        var nextLocal = candidates[0].ToDateTime(new TimeOnly(8, 0));
        return TimeZoneInfo.ConvertTimeToUtc(nextLocal, timeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Seoul" : timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        }
    }
}
