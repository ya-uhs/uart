using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class GraphViewModel : ViewModelBase, IDisposable
{
    private readonly SerialPortService _serialPortService;

    private readonly ConcurrentQueue<byte[]> _receiveQueue = new();
    private readonly List<double> _yData = new();
    private readonly StringBuilder _lineBuffer = new();
    private Regex? _compiledRegex;

    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private bool _isEnabled = false;

    [ObservableProperty]
    private string _pattern = @"[-+]?\d+(?:\.\d+)?";

    [ObservableProperty]
    private int _maxPoints = 500;

    /// <summary>ViewがPlotを更新するためのイベント（UIスレッドから発火）</summary>
    public event Action? PlotDataUpdated;

    public GraphViewModel(SerialPortService serialPortService)
    {
        _serialPortService = serialPortService;
        _serialPortService.DataReceived += OnDataReceived;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += Process;
        _timer.Start();
    }

    private void OnDataReceived(object? sender, byte[] data)
        => _receiveQueue.Enqueue(data);

    /// <summary>DispatcherTimerコールバック（UIスレッド）</summary>
    private void Process(object? sender, EventArgs e)
    {
        // キューは常にドレイン（IsEnabled=false でもバッファが溜まらないように）
        while (_receiveQueue.TryDequeue(out var chunk))
            _lineBuffer.Append(Encoding.UTF8.GetString(chunk));

        if (!IsEnabled) return;

        var str = _lineBuffer.ToString();
        var lastNl = str.LastIndexOf('\n');
        if (lastNl < 0) return;

        var lines = str[..lastNl].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _lineBuffer.Remove(0, lastNl + 1);

        var regex = GetRegex();
        if (regex == null) return;

        bool updated = false;
        foreach (var line in lines)
        {
            var match = regex.Match(line.TrimEnd('\r'));
            if (!match.Success) continue;
            if (!double.TryParse(match.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var val)) continue;

            _yData.Add(val);
            updated = true;
        }

        if (!updated) return;

        if (_yData.Count > MaxPoints)
            _yData.RemoveRange(0, _yData.Count - MaxPoints);

        PlotDataUpdated?.Invoke();
    }

    private Regex? GetRegex()
    {
        if (_compiledRegex != null) return _compiledRegex;
        try
        {
            _compiledRegex = new Regex(Pattern, RegexOptions.Compiled);
            return _compiledRegex;
        }
        catch
        {
            return null;
        }
    }

    partial void OnPatternChanged(string value) => _compiledRegex = null;

    partial void OnIsEnabledChanged(bool value)
    {
        if (!value) _lineBuffer.Clear();
    }

    /// <summary>現在のYデータのスナップショットを返す</summary>
    public double[] GetYData() => _yData.ToArray();

    public void LoadFromSettings(GraphSettings s)
    {
        IsEnabled = s.IsEnabled;
        Pattern = s.Pattern;
        MaxPoints = s.MaxPoints;
    }

    public GraphSettings GetSettings() => new()
    {
        IsEnabled = IsEnabled,
        Pattern = Pattern,
        MaxPoints = MaxPoints
    };

    [RelayCommand]
    private void ClearGraph()
    {
        _yData.Clear();
        PlotDataUpdated?.Invoke();
    }

    public void Dispose()
    {
        _timer.Stop();
        _serialPortService.DataReceived -= OnDataReceived;
    }
}
