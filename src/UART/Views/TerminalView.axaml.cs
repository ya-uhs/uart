using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using UART.ViewModels;

namespace UART.Views;

public partial class TerminalView : UserControl
{
    public TerminalView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private TerminalViewModel? _viewModel;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as TerminalViewModel;

        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalViewModel.DisplayText) &&
            _viewModel?.AutoScroll == true)
        {
            var sv = this.FindControl<ScrollViewer>("TerminalScrollViewer");
            sv?.ScrollToEnd();
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var tb = this.FindControl<TextBox>("SendTextBox");
        if (tb != null)
            tb.KeyDown += OnSendTextBoxKeyDown;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        var tb = this.FindControl<TextBox>("SendTextBox");
        if (tb != null)
            tb.KeyDown -= OnSendTextBoxKeyDown;
    }

    private void OnSendTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.Key)
        {
            case Key.Up:
                _viewModel.NavigateHistory(true);
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel.NavigateHistory(false);
                e.Handled = true;
                break;
            case Key.Return:
                _viewModel.SendCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
