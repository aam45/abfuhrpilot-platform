using System.Collections.Concurrent;
using AbfuhrPilot.Application.Auth;

namespace AbfuhrPilot.Infrastructure.Security;

/// <summary>
/// Simple in-memory brute-force throttle: after <see cref="MaxAttempts"/> failed
/// logins for an identifier, further attempts are locked for <see cref="LockoutWindow"/>.
/// A successful login resets the counter. For a single instance this is enough;
/// a scaled-out deployment would back this with a distributed cache instead.
/// </summary>
public class InMemoryLoginThrottle : ILoginThrottle
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(10);

    private sealed class Entry { public int Failures; public DateTime? LockedUntil; }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void EnsureNotLocked(string key)
    {
        if (_entries.TryGetValue(key, out var e) && e.LockedUntil is { } until)
        {
            if (DateTime.UtcNow < until)
                throw new TooManyRequestsException("Too many failed login attempts. Please try again later.");
            // window elapsed → clear
            _entries.TryRemove(key, out _);
        }
    }

    public void RegisterFailure(string key)
    {
        var e = _entries.GetOrAdd(key, _ => new Entry());
        lock (e)
        {
            e.Failures++;
            if (e.Failures >= MaxAttempts)
                e.LockedUntil = DateTime.UtcNow.Add(LockoutWindow);
        }
    }

    public void Reset(string key) => _entries.TryRemove(key, out _);
}
