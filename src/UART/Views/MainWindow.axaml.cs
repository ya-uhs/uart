using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using UART.ViewModels;

namespace UART.Views;

public partial class MainWindow : Window
{
    // グラフ有効時に使うピクセル高さ（スプリッターで変更した値を保持）
    private double _graphRowHeight = 200;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.GraphViewModel.PropertyChanged += OnGraphPropertyChanged;
    }

    private void OnGraphPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GraphViewModel.IsEnabled)) return;

        var row = RightPaneGrid.RowDefinitions[2];
        if ((sender as GraphViewModel)?.IsEnabled == true)
            row.Height = new GridLength(_graphRowHeight, GridUnitType.Pixel);
        else
        {
            // 現在の高さを保存してから Auto に戻す
            if (row.Height.GridUnitType == GridUnitType.Pixel)
                _graphRowHeight = row.Height.Value;
            row.Height = GridLength.Auto;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (DataContext is MainWindowViewModel vm)
            vm.SaveSession();
    }
}
