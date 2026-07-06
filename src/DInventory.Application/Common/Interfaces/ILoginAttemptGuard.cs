namespace DInventory.Application.Common.Interfaces;

/// <summary>Tracks failed login attempts per-username and enforces a temporary lockout after too
/// many in a row. This is the app-level brute-force guard that sits alongside (not instead of) the
/// per-IP rate limiter on the /Account/Login endpoint - the rate limiter throttles request volume,
/// this guard throttles a specific account regardless of which IP the attempts come from.</summary>
public interface ILoginAttemptGuard
{
    /// <summary>True if this username is currently locked out from further login attempts.
    /// <paramref name="retryAfter"/> is how long until the lockout clears, when locked.</summary>
    bool IsLockedOut(string username, out TimeSpan? retryAfter);

    /// <summary>Call after a failed password/username check. Increments the failure count and
    /// starts a lockout once the threshold is reached.</summary>
    void RegisterFailure(string username);

    /// <summary>Call after a successful login. Clears any accumulated failure count for this
    /// username so a genuine owner isn't penalized by earlier mistyped attempts.</summary>
    void ResetFailures(string username);
}
