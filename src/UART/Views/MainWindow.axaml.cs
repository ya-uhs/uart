using Avalonia.Controls;
using UART.ViewModels;

namespace UART.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        // ウィンドウを閉じる前にセッションを保存
        if (DataContext is MainWindowViewModel vm)
            vm.SaveSession().GetAwaiter().GetResult();
    }
}
