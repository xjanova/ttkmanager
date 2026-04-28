namespace TTKManager.App.Models;

public sealed class TikTokAccount
{
    public required string AdvertiserId { get; init; }
    public required string Name { get; init; }
    public string? Currency { get; init; }
    public string? Country { get; init; }
    public required byte[] EncryptedRefreshToken { get; init; }
    public required byte[] EncryptedAccessToken { get; init; }
    public DateTimeOffset AccessTokenExpiresAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
