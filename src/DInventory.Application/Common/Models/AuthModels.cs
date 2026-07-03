using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class TokenPair
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
}

public class AuthResult : Result
{
    public User? User { get; set; }
    public TokenPair? Tokens { get; set; }

    public static AuthResult AuthSuccess(User user, TokenPair tokens) =>
        new() { Succeeded = true, User = user, Tokens = tokens };

    public new static AuthResult Failure(string error) =>
        new() { Succeeded = false, Error = error, Errors = new List<string> { error } };
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public int UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
