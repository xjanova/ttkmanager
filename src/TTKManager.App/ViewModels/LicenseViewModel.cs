using CommunityToolkit.Mvvm.Input;
using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class LicenseViewModel : ViewModelBase
{
    private readonly XmanLicenseService? _license;

    private string _enteredKey = "";
    public string EnteredKey { get => _enteredKey; set => SetProperty(ref _enteredKey, value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public string MachineId { get; }

    private string _currentLicenseType = "free";
    public string CurrentLicenseType { get => _currentLicenseType; set => SetProperty(ref _currentLicenseType, value); }

    private string _currentLicenseKey = "";
    public string CurrentLicenseKey { get => _currentLicenseKey; set => SetProperty(ref _currentLicenseKey, value); }

    private bool _isActive;
    public bool IsActive { get => _isActive; set { if (SetProperty(ref _isActive, value)) OnPropertyChanged(nameof(IsInactive)); } }
    public bool IsInactive => !IsActive;

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    public IAsyncRelayCommand ActivateCommand { get; }
    public IAsyncRelayCommand DeactivateCommand { get; }
    public IAsyncRelayCommand ValidateCommand { get; }

    public Action<bool>? RequestClose { get; set; }

    public LicenseViewModel(XmanLicenseService license)
    {
        _license = license;
        MachineId = license.MachineId;
        Sync(license.CachedStatus);
        license.StateChanged += Sync;
        ActivateCommand = new AsyncRelayCommand(ActivateAsync);
        DeactivateCommand = new AsyncRelayCommand(DeactivateAsync);
        ValidateCommand = new AsyncRelayCommand(ValidateAsync);
    }

    public LicenseViewModel()
    {
        MachineId = "—";
        ActivateCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        DeactivateCommand = new AsyncRelayCommand(() => Task.CompletedTask);
        ValidateCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    private void Sync(LicenseStatus status)
    {
        IsActive = status.IsActive;
        CurrentLicenseType = status.IsActive ? status.DisplayType : "Free / not activated";
        CurrentLicenseKey = status.MaskedKey;
    }

    private async Task ActivateAsync()
    {
        if (_license is null) return;
        IsBusy = true;
        StatusMessage = "Contacting license server…";
        var res = await _license.ActivateAsync(EnteredKey);
        StatusMessage = res.Message;
        IsBusy = false;
        if (res.Success)
        {
            EnteredKey = "";
            RequestClose?.Invoke(true);
        }
    }

    private async Task DeactivateAsync()
    {
        if (_license is null) return;
        IsBusy = true;
        StatusMessage = "Releasing license…";
        var res = await _license.DeactivateAsync();
        StatusMessage = res.Message;
        IsBusy = false;
    }

    private async Task ValidateAsync()
    {
        if (_license is null) return;
        IsBusy = true;
        StatusMessage = "Re-validating with server…";
        var res = await _license.ValidateAsync();
        StatusMessage = res.Message;
        IsBusy = false;
    }
}
