using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class MacroViewModel : ViewModelBase
{
    private readonly TerminalViewModel _terminalViewModel;

    [ObservableProperty]
    private ObservableCollection<MacroItem> _macros = new();

    [ObservableProperty]
    private MacroItem? _selectedMacro;

    public List<string> NewLineOptions { get; } = new() { "None", "CR", "LF", "CRLF" };

    public MacroViewModel(TerminalViewModel terminalViewModel)
    {
        _terminalViewModel = terminalViewModel;
    }

    [RelayCommand]
    private void AddMacro()
    {
        var macro = new MacroItem { Name = $"Macro {Macros.Count + 1}" };
        Macros.Add(macro);
        SelectedMacro = macro;
    }

    [RelayCommand]
    private void RemoveMacro(MacroItem? macro)
    {
        if (macro == null) return;
        Macros.Remove(macro);
        if (SelectedMacro == macro)
            SelectedMacro = Macros.Count > 0 ? Macros[0] : null;
    }

    [RelayCommand]
    private void ExecuteMacro(MacroItem? macro)
    {
        if (macro == null) return;

        byte[] newLineBytes = macro.NewLine switch
        {
            "CR" => new byte[] { 0x0D },
            "LF" => new byte[] { 0x0A },
            "CRLF" => new byte[] { 0x0D, 0x0A },
            _ => Array.Empty<byte>()
        };

        var commandBytes = Encoding.UTF8.GetBytes(macro.Command);
        var data = commandBytes.Concat(newLineBytes).ToArray();
        _terminalViewModel.SendBytes(data);
    }

    [RelayCommand]
    private async Task SaveMacros()
    {
        try
        {
            var path = GetMacroFilePath();
            var dataList = Macros.Select(m => new MacroItemData
            {
                Name = m.Name,
                Command = m.Command,
                NewLine = m.NewLine
            }).ToList();
            var json = JsonSerializer.Serialize(dataList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // 保存エラーは無視（v1.0）
            _ = ex;
        }
    }

    [RelayCommand]
    private async Task LoadMacros()
    {
        try
        {
            var path = GetMacroFilePath();
            if (!File.Exists(path)) return;

            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var dataList = JsonSerializer.Deserialize<List<MacroItemData>>(json);
            if (dataList != null)
            {
                Macros = new ObservableCollection<MacroItem>(
                    dataList.Select(d => new MacroItem
                    {
                        Name = d.Name,
                        Command = d.Command,
                        NewLine = d.NewLine
                    })
                );
            }
        }
        catch (Exception ex)
        {
            _ = ex;
        }
    }

    private static string GetMacroFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UART"
        );
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "macros.json");
    }

    public void LoadFromSettings(List<MacroItemData> macros)
    {
        Macros = new ObservableCollection<MacroItem>(
            macros.Select(d => new MacroItem
            {
                Name = d.Name,
                Command = d.Command,
                NewLine = d.NewLine
            })
        );
    }

    public List<MacroItemData> GetMacroData() =>
        Macros.Select(m => new MacroItemData
        {
            Name = m.Name,
            Command = m.Command,
            NewLine = m.NewLine
        }).ToList();
}
