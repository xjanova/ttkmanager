using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TTKManager.App.ViewModels;

namespace TTKManager.App.Views;

public partial class ConnectAccountWindow : Window
{
    public ConnectAccountWindow()
    {
        InitializeComponent();
    }

    public ConnectAccountWindow(ConnectAccountViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose = success => Dispatcher.UIThread.Post(() =>
        {
            Close(success);
        });
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
