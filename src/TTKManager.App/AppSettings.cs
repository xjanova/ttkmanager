namespace TTKManager.App;

public sealed class AppSettings
{
    public string DatabasePath { get; init; } = "ttkmanager.db";

    public string TikTokAppId { get; init; } = "";
    public string TikTokAppSecret { get; init; } = "";
    public string RedirectUri { get; init; } = "https://xman4289.com/ttkmanager/callback";

    public bool UseMockApi { get; init; } = true;

    public static AppSettings LoadOrDefault(string folderPath)
    {
        var file = Path.Combine(folderPath, "appsettings.local.json");
        if (!File.Exists(file)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(file);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
