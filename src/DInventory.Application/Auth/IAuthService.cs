using DInventory.Application.Common.Models;

namespace DInventory.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, string? ipAddress);
    Task<bool> LogoutAsync(string refreshToken, string? ipAddress);
    Task<int> RevokeAllTokensForUserAsync(int userId, string? ipAddress);
    Task<Result> ForgotPasswordAsync(string email, string resetUrlBase);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request);
    Task<Result> ChangePasswordAsync(ChangePasswordRequest request);
}
