namespace DInventory.Web.Common;

/// <summary>Cookie names shared between AccountController and the JWT bearer authentication handler.</summary>
public static class AuthCookies
{
    /// <summary>Holds the JWT access token. Not HttpOnly so site.js can attach it as a Bearer header on API calls.</summary>
    public const string AccessToken = "dinv_access_token";

    /// <summary>Holds the opaque refresh token. HttpOnly - only read server-side by the refresh/logout endpoints.</summary>
    public const string RefreshToken = "dinv_refresh_token";
}
