using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class TerminalViewModel : ViewModelBase, IDisposable
{
    private readonly SerialPortService _serialPortService;

    // シリアル受信スレッドからデータを受け取るキュー
    private readonly ConcurrentQueue<byte[]> _receiveBuffer = new();

    // ログエントリ（UIスレッドのみアクセス）
    private readonly List<TerminalEntry> _entries = new();
    private const int MaxEntries = 500;

    // 送信履歴
    private readonly List<string> _sendHistory = new();
    private int _historyIndex = -1;
    private const int MaxHistoryCount = 100;

    private readonly DispatcherTimer _updateTimer;

    [ObservableProperty]
    private string _displayText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayModeLabel))]
    private bool _isHexMode = false;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private string _sendText = "";

    public string DisplayModeLabel => IsHexMode ? "HEX" : "ASCII";

    /// <summary>Viewがファイル保存ダイアログを表示するためのコールバック</summary>
    public Func<string, Task<string?>>? RequestSavePath { get; set; }

    public TerminalViewModel(SerialPortService serialPortService)
    {
        _serialPortService = serialPortService;
        _serialPortService.DataReceived += OnDataReceived;

        // 16ms（約60fps）間隔でUIを更新
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _updateTimer.Tick += FlushReceiveBuffer;
        _updateTimer.Start();
    }

    // IsHexModeが切り替わったとき、全エントリを再描画
    partial void OnIsHexModeChanged(bool value) => RefreshDisplayText();

    /// <summary>シリアル受信スレッドから呼ばれる（UIに直接触れないこと）</summary>
    private void OnDataReceived(object? sender, byte[] data)
    {
        _receiveBuffer.Enqueue(data);
    }

    /// <summary>DispatcherTimerコールバック（UIスレッド）</summary>
    private void FlushReceiveBuffer(object? sender, EventArgs e)
    {
        if (_receiveBuffer.IsEmpty) return;

        // 今周期分のデータをまとめる
        var chunks = new List<byte[]>();
        while (_receiveBuffer.TryDequeue(out var chunk))
            chunks.Add(chunk);

        var totalLen = chunks.Sum(c => c.Length);
        var combined = new byte[totalLen];
        int offset = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(combined, offset);
            offset += chunk.Length;
        }

        var entry = new TerminalEntry
        {
            Timestamp = DateTime.Now,
            Data = combined,
            IsSent = false
        };

        AddEntry(entry);
    }

    private void AddEntry(TerminalEntry entry)
    {
        _entries.Add(entry);
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(0, _entries.Count - MaxEntries);
            // DisplayText も _entries に合わせて再構築（無制限増加を防ぐ）
            RefreshDisplayText();
            return;
        }

        DisplayText += FormatEntry(entry);
    }

    private void RefreshDisplayText()
    {
        var sb = new StringBuilder();
        foreach (var entry in _entries)
            sb.Append(FormatEntry(entry));
        DisplayText = sb.ToString();
    }

    private string FormatEntry(TerminalEntry entry)
    {
        var ts = entry.Timestamp.ToString("HH:mm:ss.fff");
        var dir = entry.IsSent ? "TX" : "RX";
        string content;

        if (IsHexMode)
        {
            content = BitConverter.ToString(entry.Data).Replace("-", " ");
        }
        else
        {
            // 不正なバイトは?で表示
            content = Encoding.UTF8.GetString(entry.Data)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .TrimEnd('\n');
        }

        return $"[{ts}] [{dir}] {content}\n";
    }

    [RelayCommand]
    private void Send()
    {
        if (string.IsNullOrEmpty(SendText)) return;
        SendLine(SendText);
        SendText = "";
    }

    /// <summary>改行コードを付けて送信（UI送信テキストボックス用）</summary>
    private void SendLine(string text)
    {
        // 送信履歴に追加
        if (_sendHistory.Count == 0 || _sendHistory[^1] != text)
        {
            _sendHistory.Add(text);
            if (_sendHistory.Count > MaxHistoryCount)
                _sendHistory.RemoveAt(0);
        }
        _historyIndex = -1;

        var data = Encoding.UTF8.GetBytes(text + "\r\n");
        SendBytesInternal(data, text);
    }

    /// <summary>マクロ等から生バイト列を送信する</summary>
    public void SendBytes(byte[] data)
    {
        SendBytesInternal(data, null);
    }

    /// <summary>Luaプラグイン等からログメッセージをターミナルに追記する（UIスレッド想定）</summary>
    public void AppendLog(string message)
    {
        DisplayText += $"[{DateTime.Now:HH:mm:ss.fff}] [LOG] {message}\n";
    }

    private void SendBytesInternal(byte[] data, string? displayText)
    {
        try
        {
            _serialPortService.Send(data);
        }
        catch (Exception ex)
        {
            DisplayText += $"[{DateTime.Now:HH:mm:ss.fff}] [ERR] Send error: {ex.Message}\n";
            return;
        }

        // 送信エコーをターミナルに表示
        var echoData = displayText != null ? Encoding.UTF8.GetBytes(displayText) : data;
        var entry = new TerminalEntry
        {
            Timestamp = DateTime.Now,
            Data = echoData,
            IsSent = true
        };
        AddEntry(entry);
    }

    /// <summary>↑↓キーで送信履歴をナビゲートする（Viewのcode-behindから呼ぶ）</summary>
    public void NavigateHistory(bool up)
    {
        if (_sendHistory.Count == 0) return;

        if (up)
        {
            _historyIndex = _historyIndex == -1
                ? _sendHistory.Count - 1
                : Math.Max(0, _historyIndex - 1);
        }
        else
        {
            if (_historyIndex == -1) return;
            _historyIndex++;
            if (_historyIndex >= _sendHistory.Count)
            {
                _historyIndex = -1;
                SendText = "";
                return;
            }
        }

        SendText = _sendHistory[_historyIndex];
    }

    [RelayCommand]
    private async Task ExportLog()
    {
        if (RequestSavePath == null) return;

        var path = await RequestSavePath("log");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                ExportCsv(path);
            else
                ExportTxt(path);
        }
        catch (Exception ex)
        {
            DisplayText += $"[{DateTime.Now:HH:mm:ss.fff}] [ERR] Export error: {ex.Message}\n";
        }
    }

    private void ExportTxt(string path)
    {
        var sb = new StringBuilder();
        foreach (var entry in _entries)
            sb.Append(FormatEntry(entry));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void ExportCsv(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Direction,HEX,ASCII");
        foreach (var entry in _entries)
        {
            var ts = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var dir = entry.IsSent ? "TX" : "RX";
            var hex = BitConverter.ToString(entry.Data).Replace("-", " ");
            var ascii = Encoding.UTF8.GetString(entry.Data)
                .Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ")
                .Replace("\"", "\"\"");
            sb.AppendLine($"\"{ts}\",{dir},\"{hex}\",\"{ascii}\"");
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    [RelayCommand]
    private void ClearTerminal()
    {
        _entries.Clear();
        DisplayText = "";
    }

    public void Dispose()
    {
        _updateTimer.Stop();
        _serialPortService.DataReceived -= OnDataReceived;
    }
}
