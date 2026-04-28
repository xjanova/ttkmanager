namespace TTKManager.App.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private AccountsViewModel _accounts;
    public AccountsViewModel Accounts { get => _accounts; set => SetProperty(ref _accounts, value); }

    private SchedulesViewModel _schedules;
    public SchedulesViewModel Schedules { get => _schedules; set => SetProperty(ref _schedules, value); }

    private LogsViewModel _logs;
    public LogsViewModel Logs { get => _logs; set => SetProperty(ref _logs, value); }

    public string Title => "TTKManager — TikTok Marketing API Manager";

    public MainWindowViewModel(AccountsViewModel accounts, SchedulesViewModel schedules, LogsViewModel logs)
    {
        _accounts = accounts;
        _schedules = schedules;
        _logs = logs;
    }

    public MainWindowViewModel()
    {
        _accounts = new AccountsViewModel();
        _schedules = new SchedulesViewModel();
        _logs = new LogsViewModel();
    }
}
