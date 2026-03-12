using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UART.ViewModels;

namespace UART.Views;

public partial class LuaPluginView : UserControl
{
    public LuaPluginView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is LuaPluginViewModel vm)
        {
            vm.RequestOpenFilePath = ShowOpenFileDialogAsync;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Browse ボタンにクリックハンドラを設定
        if (this.FindControl<Button>("BrowseButton") is { } btn)
            btn.Click += OnBrowseClick;
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LuaPluginViewModel vm || vm.SelectedPlugin == null) return;

        var path = await ShowOpenFileDialogAsync();
        if (path == null) return;

        vm.SelectedPlugin.FilePath = path;
        if (string.IsNullOrEmpty(vm.SelectedPlugin.Name) || vm.SelectedPlugin.Name == "New Plugin")
            vm.SelectedPlugin.Name = System.IO.Path.GetFileNameWithoutExtension(path);

        vm.ReloadPluginCommand.Execute(vm.SelectedPlugin);
    }

    private async Task<string?> ShowOpenFileDialogAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Luaスクリプトを選択",
                AllowMultiple = false,
                FileTypeFilter = new List<Avalonia.Platform.Storage.FilePickerFileType>
                {
                    new("Lua Script") { Patterns = new[] { "*.lua" } },
                    new("All Files")  { Patterns = new[] { "*" } }
                }
            });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
