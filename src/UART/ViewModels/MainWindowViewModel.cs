using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly SerialPortService _serialPortService;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UART", "session.json"
    );

    public ConnectionViewModel ConnectionViewModel { get; }
    public TerminalViewModel TerminalViewModel { get; }
    public MacroViewModel MacroViewModel { get; }
    public TriggerViewModel TriggerViewModel { get; }
    public ProtocolViewModel ProtocolViewModel { get; }
    public GraphViewModel GraphViewModel { get; }

    public MainWindowViewModel()
    {
        _serialPortService = new SerialPortService();
        ConnectionViewModel = new ConnectionViewModel(_serialPortService);
        TerminalViewModel = new TerminalViewModel(_serialPortService);
        MacroViewModel = new MacroViewModel(TerminalViewModel);
        TriggerViewModel = new TriggerViewModel(_serialPortService, TerminalViewModel);
        ProtocolViewModel = new ProtocolViewModel(_serialPortService);
        GraphViewModel = new GraphViewModel(_serialPortService);

        LoadSession();
    }

    private void LoadSession()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<SessionSettings>(json);
            if (settings == null) return;

            ConnectionViewModel.LoadSettings(settings);
            MacroViewModel.LoadFromSettings(settings.Macros);
            TriggerViewModel.LoadFromSettings(settings.Triggers);
            ProtocolViewModel.LoadFromSettings(settings.Protocol);
            GraphViewModel.LoadFromSettings(settings.Graph);
            TerminalViewModel.IsHexMode = settings.DisplayMode == "HEX";
            TerminalViewModel.AutoScroll = settings.AutoScroll;
        }
        catch
        {
            // 設定ファイルが壊れていても起動は継続
        }
    }

    [RelayCommand]
    public void SaveSession()
    {
        try
        {
            var settings = new SessionSettings();
            ConnectionViewModel.SaveSettings(settings);
            settings.Macros = MacroViewModel.GetMacroData();
            settings.Triggers = TriggerViewModel.GetTriggerData();
            settings.Protocol = ProtocolViewModel.GetSettings();
            settings.Graph = GraphViewModel.GetSettings();
            settings.DisplayMode = TerminalViewModel.IsHexMode ? "HEX" : "ASCII";
            settings.AutoScroll = TerminalViewModel.AutoScroll;

            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json, Encoding.UTF8);
        }
        catch
        {
            // 保存失敗は無視
        }
    }

    public void Dispose()
    {
        SaveSession();
        GraphViewModel.Dispose();
        ProtocolViewModel.Dispose();
        TriggerViewModel.Dispose();
        TerminalViewModel.Dispose();
        _serialPortService.Dispose();
    }
}
