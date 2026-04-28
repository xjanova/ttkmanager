namespace TTKManager.App.ViewModels;

public class ShortcutsViewModel : ViewModelBase
{
    public IReadOnlyList<ShortcutEntry> Navigation { get; } = new[]
    {
        new ShortcutEntry("Dashboard", "Ctrl+1"),
        new ShortcutEntry("Accounts", "Ctrl+2"),
        new ShortcutEntry("Schedules", "Ctrl+3"),
        new ShortcutEntry("Activity Log", "Ctrl+4"),
        new ShortcutEntry("Reports", "Ctrl+5"),
        new ShortcutEntry("Auto-Rules", "Ctrl+6"),
        new ShortcutEntry("Anomalies", "Ctrl+7"),
        new ShortcutEntry("Pacing", "Ctrl+8"),
        new ShortcutEntry("Settings", "Ctrl+,"),
    };

    public IReadOnlyList<ShortcutEntry> Actions { get; } = new[]
    {
        new ShortcutEntry("Refresh current view", "F5"),
        new ShortcutEntry("Search anywhere", "/"),
    };
}

public sealed record ShortcutEntry(string Name, string Keys);
