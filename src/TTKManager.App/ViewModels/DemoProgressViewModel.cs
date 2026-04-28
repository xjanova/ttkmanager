using TTKManager.App.Services;

namespace TTKManager.App.ViewModels;

public class DemoProgressViewModel : ViewModelBase
{
    private string _stage = "Preparing…";
    public string Stage { get => _stage; set => SetProperty(ref _stage, value); }

    private double _percent;
    public double Percent { get => _percent; set => SetProperty(ref _percent, value); }

    private string _title = "Building demo data";
    public string Title { get => _title; set => SetProperty(ref _title, value); }

    public string PercentText
    {
        get => $"{Percent:F0}%";
    }

    public void Apply(DemoProgress p)
    {
        Stage = p.Stage;
        Percent = p.Percent;
        OnPropertyChanged(nameof(PercentText));
    }
}
