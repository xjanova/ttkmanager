using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TTKManager.App.ViewModels;

namespace TTKManager.App.Views;

public partial class LicenseDialog : Window
{
    public LicenseDialog() { InitializeComponent(); }

    public LicenseDialog(LicenseViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose = success => Dispatcher.UIThread.Post(() => Close(success));
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(false);
}
