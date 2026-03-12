using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class LuaPluginViewModel : ViewModelBase, IDisposable
{
    private readonly LuaPluginService _service;

    [ObservableProperty]
    private ObservableCollection<LuaPlugin> _plugins = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPlugin))]
    private LuaPlugin? _selectedPlugin;

    public bool HasSelectedPlugin => SelectedPlugin != null;

    /// <summary>Viewがファイルオープンダイアログを表示するためのコールバック</summary>
    public Func<Task<string?>>? RequestOpenFilePath { get; set; }

    public LuaPluginViewModel(LuaPluginService service)
    {
        _service = service;
    }

    // ─── コマンド ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddPlugin()
    {
        string? path = null;
        if (RequestOpenFilePath != null)
            path = await RequestOpenFilePath();

        var plugin = new LuaPlugin
        {
            Name = path != null ? System.IO.Path.GetFileNameWithoutExtension(path) : "New Plugin",
            FilePath = path ?? "",
            IsEnabled = true
        };

        AttachPluginListener(plugin);
        Plugins.Add(plugin);
        SelectedPlugin = plugin;

        if (!string.IsNullOrEmpty(plugin.FilePath))
            _service.LoadPlugin(plugin);
    }

    [RelayCommand]
    private void RemovePlugin(LuaPlugin? plugin)
    {
        if (plugin == null) return;
        DetachPluginListener(plugin);
        _service.UnloadPlugin(plugin);
        Plugins.Remove(plugin);
        if (SelectedPlugin == plugin)
            SelectedPlugin = null;
    }

    [RelayCommand]
    private void ReloadPlugin(LuaPlugin? plugin)
    {
        if (plugin == null) return;
        _service.ReloadPlugin(plugin);
    }

    [RelayCommand]
    private void ReloadAll()
    {
        foreach (var plugin in Plugins)
            _service.ReloadPlugin(plugin);
    }

    // ─── IsEnabled変更の監視 ──────────────────────────────────────────────────

    /// <summary>プラグインのIsEnabled変更を監視し、ロード/アンロードを自動制御する</summary>
    private void AttachPluginListener(LuaPlugin plugin)
    {
        plugin.PropertyChanged += OnPluginPropertyChanged;
    }

    private void DetachPluginListener(LuaPlugin plugin)
    {
        plugin.PropertyChanged -= OnPluginPropertyChanged;
    }

    private void OnPluginPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LuaPlugin plugin) return;
        if (e.PropertyName == nameof(LuaPlugin.IsEnabled))
        {
            if (plugin.IsEnabled)
                _service.LoadPlugin(plugin);
            else
                _service.UnloadPlugin(plugin);
        }
    }

    // ─── セッション永続化 ─────────────────────────────────────────────────────

    public void LoadFromSettings(List<LuaPluginData> data)
    {
        foreach (var plugin in Plugins)
            DetachPluginListener(plugin);
        Plugins.Clear();

        foreach (var d in data)
        {
            var plugin = new LuaPlugin
            {
                Name = d.Name,
                FilePath = d.FilePath,
                IsEnabled = d.IsEnabled
            };
            AttachPluginListener(plugin);
            Plugins.Add(plugin);
            if (plugin.IsEnabled)
                _service.LoadPlugin(plugin);
        }
    }

    public List<LuaPluginData> GetPluginData()
    {
        var result = new List<LuaPluginData>();
        foreach (var p in Plugins)
        {
            result.Add(new LuaPluginData
            {
                Name = p.Name,
                FilePath = p.FilePath,
                IsEnabled = p.IsEnabled
            });
        }
        return result;
    }

    public void Dispose()
    {
        foreach (var plugin in Plugins)
            DetachPluginListener(plugin);
        _service.Dispose();
    }
}
