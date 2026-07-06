using System.Security.Cryptography;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DInventory.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordResetTokenRepository _resetTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly ILoginAttemptGuard _loginAttemptGuard;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordResetTokenRepository resetTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        ILoginAttemptGuard loginAttemptGuard,
        IOptions<JwtSettings> jwtOptions)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _resetTokenRepository = resetTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _loginAttemptGuard = loginAttemptGuard;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress)
    {
        if (_loginAttemptGuard.IsLockedOut(request.Username, out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((retryAfter ?? TimeSpan.FromMinutes(15)).TotalMinutes));
            return AuthResult.Failure($"Too many failed login attempts for this account. Please try again in {minutes} minute(s).");
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username.Trim());
        if (user is null)
        {
            _loginAttemptGuard.RegisterFailure(request.Username);
            return AuthResult.Failure("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return AuthResult.Failure("Your account has been deactivated. Please contact an administrator.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _loginAttemptGuard.RegisterFailure(request.Username);
            return AuthResult.Failure("Invalid username or password.");
        }

        _loginAttemptGuard.ResetFailures(request.Username);

        var role = await _roleRepository.GetByIdAsync(user.RoleId);
        var roleName = role?.RoleName ?? "Staff";

        var tokens = await IssueTokensAsync(user, roleName, ipAddress);
        await _userRepository.UpdateLastLoginAsync(user.UserId);

        user.RoleName = roleName;
        return AuthResult.AuthSuccess(user, tokens);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Failure("Missing refresh token.");
        }

        var tokenHash = _jwtTokenService.HashToken(refreshToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (existingToken is null || !existingToken.IsActive)
        {
            return AuthResult.Failure("Session expired. Please log in again.");
        }

        var user = await _userRepository.GetByIdAsync(existingToken.UserId);
        if (user is null || !user.IsActive)
        {
            return AuthResult.Failure("Account not available. Please log in again.");
        }

        // Rotate: revoke old, issue new
        var role = await _roleRepository.GetByIdAsync(user.RoleId);
        var roleName = role?.RoleName ?? "Staff";
        var newTokens = await IssueTokensAsync(user, roleName, ipAddress);

        await _refreshTokenRepository.RevokeAsync(tokenHash, ipAddress, _jwtTokenService.HashToken(newTokens.RefreshToken));

        user.RoleName = roleName;
        return AuthResult.AuthSuccess(user, newTokens);
    }

    public async Task<bool> LogoutAsync(string refreshToken, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var tokenHash = _jwtTokenService.HashToken(refreshToken);
        return await _refreshTokenRepository.RevokeAsync(tokenHash, ipAddress);
    }

    public async Task<int> RevokeAllTokensForUserAsync(int userId, string? ipAddress)
    {
        return await _refreshTokenRepository.RevokeAllForUserAsync(userId, ipAddress);
    }

    public async Task<Result> ForgotPasswordAsync(string email, string resetUrlBase)
    {
        var user = await _userRepository.GetByEmailAsync(email.Trim());
        if (user is null)
        {
            // Do not reveal whether the email exists
            return Result.Success();
        }

        try
        {
            await _resetTokenRepository.InvalidateAllForUserAsync(user.UserId);

            var rawToken = GenerateSecureToken();
            var tokenHash = _jwtTokenService.HashToken(rawToken);

            await _resetTokenRepository.CreateAsync(new PasswordResetToken
            {
                UserId = user.UserId,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });

            var resetLink = $"{resetUrlBase.TrimEnd('/')}?token={Uri.EscapeDataString(rawToken)}&email={Uri.EscapeDataString(user.Email)}";
            var body = $@"<p>Hello {user.FullName},</p>
<p>We received a request to reset your DInventory password. Click the link below to choose a new password. This link expires in 1 hour.</p>
<p><a href=""{resetLink}"">Reset your password</a></p>
<p>If you did not request this, you can safely ignore this email.</p>";

            await _emailService.SendEmailAsync(user.Email, "DInventory - Password Reset", body);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to process password reset request: {ex.Message}");
        }
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var tokenHash = _jwtTokenService.HashToken(request.Token);
        var resetToken = await _resetTokenRepository.GetByTokenHashAsync(tokenHash);

        if (resetToken is null || resetToken.IsUsed || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return Result.Failure("This password reset link is invalid or has expired.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 5)
        {
            return Result.Failure("Password must be at least 5 characters long.");
        }

        var user = await _userRepository.GetByIdAsync(resetToken.UserId);
        if (user is null)
        {
            return Result.Failure("Account not found.");
        }

        var newHash = _passwordHasher.Hash(request.NewPassword);

        try
        {
            await _userRepository.UpdatePasswordAsync(user.UserId, newHash);
            await _resetTokenRepository.MarkUsedAsync(resetToken.ResetTokenId);
            await _refreshTokenRepository.RevokeAllForUserAsync(user.UserId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to reset password: {ex.Message}");
        }
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user is null)
        {
            return Result.Failure("Account not found.");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure("Current password is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 5)
        {
            return Result.Failure("New password must be at least 5 characters long.");
        }

        var newHash = _passwordHasher.Hash(request.NewPassword);

        try
        {
            await _userRepository.UpdatePasswordAsync(user.UserId, newHash);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to change password: {ex.Message}");
        }
    }

    private async Task<TokenPair> IssueTokensAsync(User user, string roleName, string? ipAddress)
    {
        var (accessToken, accessExpiresAt) = _jwtTokenService.GenerateAccessToken(user, roleName);
        var refreshTokenValue = _jwtTokenService.GenerateRefreshTokenValue();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        await _refreshTokenRepository.CreateAsync(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = _jwtTokenService.HashToken(refreshTokenValue),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        });

        return new TokenPair
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshExpiresAt
        };
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
