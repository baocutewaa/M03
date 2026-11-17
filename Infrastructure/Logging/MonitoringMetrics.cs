using System.Collections.Concurrent;

namespace M03_Opus.Infrastructure.Logging;

/// <summary>
/// Theo dõi bộ đếm sự kiện 429 thời gian thực cho cảnh báo đột biến DoS và dashboard F9.6 (Tình huống 26).
/// </summary>
public sealed partial class MonitoringMetrics
{
    private readonly ConcurrentQueue<long> _rateLimitTicks = new();

    public void RecordRateLimitHit(ILogger logger, string userName, string clientIp, string requestPath)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        _rateLimitTicks.Enqueue(now);
        PruneOldEntries(now - TimeSpan.FromHours(24).Ticks);

        var minuteCount = CountSince(now - TimeSpan.FromMinutes(1).Ticks);
        LogRateLimitExceeded(logger, userName, clientIp, requestPath, minuteCount);

        if (minuteCount > 100)
        {
            LogRateLimitSpike(logger, minuteCount);
        }
    }

    public (int LastMinute, int Last24Hours, bool IsSpike) GetRateLimitStats()
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        PruneOldEntries(now - TimeSpan.FromHours(24).Ticks);

        var minuteCount = CountSince(now - TimeSpan.FromMinutes(1).Ticks);
        var dayCount = _rateLimitTicks.Count;
        return (minuteCount, dayCount, minuteCount > 100);
    }

    private void PruneOldEntries(long cutoffTicks)
    {
        while (_rateLimitTicks.TryPeek(out var oldest) && oldest < cutoffTicks)
        {
            _rateLimitTicks.TryDequeue(out _);
        }
    }

    private int CountSince(long cutoffTicks)
    {
        var count = 0;
        foreach (var ticks in _rateLimitTicks)
        {
            if (ticks >= cutoffTicks)
            {
                count++;
            }
        }

        return count;
    }

    [LoggerMessage(
        EventId = SecurityEventIds.RateLimitExceeded,
        Level = LogLevel.Warning,
        Message = "Rate limit exceeded (429) for User={UserName} Ip={ClientIp} Path={RequestPath} Count1m={Count1m}")]
    private static partial void LogRateLimitExceeded(
        ILogger logger,
        string userName,
        string clientIp,
        string requestPath,
        int count1m);

    [LoggerMessage(
        EventId = SecurityEventIds.RateLimitExceeded,
        Level = LogLevel.Critical,
        Message = "ALERT: Rate limit spike detected ({Count1m} rejections in last 1 minute > 100 threshold).")]
    private static partial void LogRateLimitSpike(ILogger logger, int count1m);
}
