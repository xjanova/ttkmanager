using System.Net;

namespace TTKManager.App.Services;

public static class OAuthHelper
{
    private const string AuthorizeUrl = "https://business-api.tiktok.com/portal/auth";

    public static string BuildAuthorizationUrl(TikTokAppCredentials creds, string state)
    {
        var qs = $"app_id={WebUtility.UrlEncode(creds.AppId)}" +
                 $"&state={WebUtility.UrlEncode(state)}" +
                 $"&redirect_uri={WebUtility.UrlEncode(creds.RedirectUri)}";
        return $"{AuthorizeUrl}?{qs}";
    }

    public static string GenerateState()
    {
        Span<byte> bytes = stackalloc byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
