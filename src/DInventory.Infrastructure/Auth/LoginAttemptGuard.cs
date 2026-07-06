using DInventory.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DInventory.Infrastructure.Auth;

/// <summary>In-memory (per-process) failed-login tracker - deliberately simple rather than DB-backed
/// so it needs no schema change. Resets on app restart, which is an acceptable trade-off for a
/// brute-force speed bump; it is not meant to be a permanent audit trail (AuditLogs already records
/// every LOGIN_FAILED event durably for that purpose).</summary>
public class LoginAttemptGuard : ILoginAttemptGuard
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private sealed class AttemptState
    {
        public int Count;
        public DateTime WindowStartUtc;
        public DateTime? LockedUntilUtc;
    }

    private readonly IMemoryCache _cache;
    private readonly bool _enforce;
    private static readonly object SyncRoot = new();

    /// <summary><paramref name="enforce"/> is false outside Production (see
    /// DependencyInjection.AddInfrastructureServices) so local/staging testing - where testers or
    /// the developer routinely mistype passwords - never gets locked out; the real brute-force
    /// protection only needs to bite on a live, internet-reachable deployment.</summary>
    public LoginAttemptGuard(IMemoryCache cache, bool enforce = true)
    {
        _cache = cache;
        _enforce = enforce;
    }

    private static string CacheKey(string username) => $"login-attempt::{username.Trim().ToLowerInvariant()}";

    public bool IsLockedOut(string username, out TimeSpan? retryAfter)
    {
        retryAfter = null;

        if (!_enforce || string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        if (_cache.TryGetValue<AttemptState>(CacheKey(username), out var state) && state is not null && state.LockedUntilUtc.HasValue)
        {
            var remaining = state.LockedUntilUtc.Value - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                retryAfter = remaining;
                return true;
            }
        }

        return false;
    }

    public void RegisterFailure(string username)
    {
        if (!_enforce || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var key = CacheKey(username);

        lock (SyncRoot)
        {
            var state = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = AttemptWindow > LockoutDuration ? AttemptWindow : LockoutDuration;
                return new AttemptState { Count = 0, WindowStartUtc = DateTime.UtcNow };
            })!;

            if (DateTime.UtcNow - state.WindowStartUtc > AttemptWindow)
            {
                state.Count = 0;
                state.WindowStartUtc = DateTime.UtcNow;
                state.LockedUntilUtc = null;
            }

            state.Count++;

            if (state.Count >= MaxAttempts)
            {
                state.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
            }

            _cache.Set(key, state, AttemptWindow > LockoutDuration ? AttemptWindow : LockoutDuration);
        }
    }

    public void ResetFailures(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        _cache.Remove(CacheKey(username));
    }
}
