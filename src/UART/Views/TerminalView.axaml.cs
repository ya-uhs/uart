using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.RequestSavePath = ShowSaveFileDialogAsync;
        }
    }

    private async Task<string?> ShowSaveFileDialogAsync(string defaultName)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Log",
            SuggestedFileName = $"uart-log-{DateTime.Now:yyyyMMdd-HHmmss}",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("CSV file")  { Patterns = new[] { "*.csv" } },
            }
        });

        return file?.TryGetLocalPath();
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
