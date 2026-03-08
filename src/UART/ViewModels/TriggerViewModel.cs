using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class TriggerViewModel : ViewModelBase, IDisposable
{
    private readonly SerialPortService _serialPortService;
    private readonly TerminalViewModel _terminalViewModel;

    // 受信データをバックグラウンドスレッドから受け取るキュー
    private readonly ConcurrentQueue<byte[]> _receiveQueue = new();

    // パターンマッチ用のローリングバッファ（最大4KB）
    private readonly StringBuilder _rollingBuffer = new();
    private const int MaxBufferLength = 4096;

    private readonly DispatcherTimer _checkTimer;

    [ObservableProperty]
    private ObservableCollection<TriggerItem> _triggers = new();

    [ObservableProperty]
    private TriggerItem? _selectedTrigger;

    public List<string> NewLineOptions { get; } = new() { "None", "CR", "LF", "CRLF" };

    public TriggerViewModel(SerialPortService serialPortService, TerminalViewModel terminalViewModel)
    {
        _serialPortService = serialPortService;
        _terminalViewModel = terminalViewModel;

        _serialPortService.DataReceived += OnDataReceived;

        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _checkTimer.Tick += ProcessQueue;
        _checkTimer.Start();
    }

    /// <summary>バックグラウンドスレッドから呼ばれる（UIに直接触れないこと）</summary>
    private void OnDataReceived(object? sender, byte[] data)
    {
        _receiveQueue.Enqueue(data);
    }

    /// <summary>DispatcherTimerコールバック（UIスレッド）</summary>
    private void ProcessQueue(object? sender, EventArgs e)
    {
        if (_receiveQueue.IsEmpty) return;

        var sb = new StringBuilder();
        while (_receiveQueue.TryDequeue(out var chunk))
            sb.Append(Encoding.UTF8.GetString(chunk));

        var received = sb.ToString();
        _rollingBuffer.Append(received);

        // バッファが上限を超えたら先頭を削る
        if (_rollingBuffer.Length > MaxBufferLength)
            _rollingBuffer.Remove(0, _rollingBuffer.Length - MaxBufferLength);

        var bufferText = _rollingBuffer.ToString();

        foreach (var trigger in Triggers)
        {
            if (!trigger.IsEnabled || string.IsNullOrEmpty(trigger.Pattern)) continue;
            if (!bufferText.Contains(trigger.Pattern, StringComparison.Ordinal)) continue;

            FireTrigger(trigger);
        }
    }

    private void FireTrigger(TriggerItem trigger)
    {
        // マッチ後にバッファをクリアして連続発火を防ぐ
        _rollingBuffer.Clear();

        byte[] newLineBytes = trigger.NewLine switch
        {
            "CR" => new byte[] { 0x0D },
            "LF" => new byte[] { 0x0A },
            "CRLF" => new byte[] { 0x0D, 0x0A },
            _ => Array.Empty<byte>()
        };

        var responseBytes = Encoding.UTF8.GetBytes(trigger.Response).Concat(newLineBytes).ToArray();
        _terminalViewModel.SendBytes(responseBytes);
    }

    [RelayCommand]
    private void AddTrigger()
    {
        var trigger = new TriggerItem { Name = $"Trigger {Triggers.Count + 1}" };
        Triggers.Add(trigger);
        SelectedTrigger = trigger;
    }

    [RelayCommand]
    private void RemoveTrigger(TriggerItem? trigger)
    {
        if (trigger == null) return;
        Triggers.Remove(trigger);
        if (SelectedTrigger == trigger)
            SelectedTrigger = Triggers.Count > 0 ? Triggers[0] : null;
    }

    public void LoadFromSettings(List<TriggerItemData> data)
    {
        Triggers = new ObservableCollection<TriggerItem>(
            data.Select(d => new TriggerItem
            {
                Name = d.Name,
                Pattern = d.Pattern,
                Response = d.Response,
                NewLine = d.NewLine,
                IsEnabled = d.IsEnabled
            })
        );
    }

    public List<TriggerItemData> GetTriggerData() =>
        Triggers.Select(t => new TriggerItemData
        {
            Name = t.Name,
            Pattern = t.Pattern,
            Response = t.Response,
            NewLine = t.NewLine,
            IsEnabled = t.IsEnabled
        }).ToList();

    public void Dispose()
    {
        _checkTimer.Stop();
        _serialPortService.DataReceived -= OnDataReceived;
    }
}
