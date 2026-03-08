using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class ProtocolViewModel : ViewModelBase, IDisposable
{
    private readonly SerialPortService _serialPortService;

    private readonly ConcurrentQueue<byte[]> _receiveQueue = new();
    private readonly List<byte> _parseBuffer = new();
    private const int MaxPackets = 200;

    private readonly DispatcherTimer _timer;

    // --- 設定 ---

    [ObservableProperty]
    private bool _isEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStxEtxMode))]
    [NotifyPropertyChangedFor(nameof(IsFixedLengthMode))]
    private string _delimiterMode = "Newline";

    [ObservableProperty]
    private string _stxHex = "02";

    [ObservableProperty]
    private string _etxHex = "03";

    [ObservableProperty]
    private int _fixedLength = 10;

    [ObservableProperty]
    private string _checksumMode = "None";

    // --- 解析結果 ---

    [ObservableProperty]
    private ObservableCollection<ParsedPacket> _packets = new();

    [ObservableProperty]
    private ParsedPacket? _selectedPacket;

    // --- UI用オプションリスト ---

    public List<string> DelimiterModes { get; } =
        new() { "Newline", "STX/ETX", "Fixed Length", "None" };

    public List<string> ChecksumModes { get; } =
        new() { "None", "XOR", "Sum8", "CRC16" };

    public bool IsStxEtxMode => DelimiterMode == "STX/ETX";
    public bool IsFixedLengthMode => DelimiterMode == "Fixed Length";

    public ProtocolViewModel(SerialPortService serialPortService)
    {
        _serialPortService = serialPortService;
        _serialPortService.DataReceived += OnDataReceived;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += Process;
        _timer.Start();
    }

    private void OnDataReceived(object? sender, byte[] data)
    {
        _receiveQueue.Enqueue(data);
    }

    private void Process(object? sender, EventArgs e)
    {
        if (_receiveQueue.IsEmpty) return;

        while (_receiveQueue.TryDequeue(out var chunk))
            _parseBuffer.AddRange(chunk);

        if (!IsEnabled) return;

        var emitted = DelimiterMode switch
        {
            "Newline" => ParseNewline(),
            "STX/ETX" => ParseStxEtx(),
            "Fixed Length" => ParseFixedLength(),
            "None" => ParseNone(),
            _ => 0
        };
        _ = emitted;
    }

    // --- パーサー ---

    private int ParseNewline()
    {
        int count = 0;
        while (true)
        {
            int nl = FindNewline();
            if (nl < 0) break;

            var line = _parseBuffer.Take(nl).ToArray();
            _parseBuffer.RemoveRange(0, nl + NewlineLen(nl));

            EmitPacket(line);
            count++;
        }
        return count;
    }

    private int FindNewline()
    {
        for (int i = 0; i < _parseBuffer.Count; i++)
        {
            if (_parseBuffer[i] == 0x0A) return i;
            if (_parseBuffer[i] == 0x0D && i + 1 < _parseBuffer.Count && _parseBuffer[i + 1] == 0x0A)
                return i;
        }
        return -1;
    }

    private int NewlineLen(int pos)
    {
        if (_parseBuffer[pos] == 0x0D) return 2;
        return 1;
    }

    private int ParseStxEtx()
    {
        int count = 0;
        byte stx = ParseHexByte(StxHex, 0x02);
        byte etx = ParseHexByte(EtxHex, 0x03);

        while (true)
        {
            int stxIdx = _parseBuffer.IndexOf(stx);
            if (stxIdx < 0) break;

            int etxIdx = -1;
            for (int i = stxIdx + 1; i < _parseBuffer.Count; i++)
            {
                if (_parseBuffer[i] == etx) { etxIdx = i; break; }
            }
            if (etxIdx < 0) break;

            // STX..payload..checksum..ETX
            var inner = _parseBuffer.Skip(stxIdx + 1).Take(etxIdx - stxIdx - 1).ToArray();
            _parseBuffer.RemoveRange(0, etxIdx + 1);

            EmitPacket(inner);
            count++;
        }
        return count;
    }

    private int ParseFixedLength()
    {
        int count = 0;
        int len = Math.Max(1, FixedLength);
        while (_parseBuffer.Count >= len)
        {
            var packet = _parseBuffer.Take(len).ToArray();
            _parseBuffer.RemoveRange(0, len);
            EmitPacket(packet);
            count++;
        }
        return count;
    }

    private int ParseNone()
    {
        if (_parseBuffer.Count == 0) return 0;
        var packet = _parseBuffer.ToArray();
        _parseBuffer.Clear();
        EmitPacket(packet);
        return 1;
    }

    // --- チェックサム ---

    private void EmitPacket(byte[] raw)
    {
        byte[] data;
        bool? checksumValid = null;

        if (ChecksumMode == "None" || raw.Length == 0)
        {
            data = raw;
        }
        else
        {
            int csLen = ChecksumMode == "CRC16" ? 2 : 1;
            if (raw.Length <= csLen)
            {
                data = raw;
                checksumValid = false;
            }
            else
            {
                data = raw[..^csLen];
                var receivedCs = raw[^csLen..];
                var calcCs = ComputeChecksum(data);
                checksumValid = receivedCs.SequenceEqual(calcCs);
            }
        }

        var packet = new ParsedPacket
        {
            Timestamp = DateTime.Now,
            Data = data,
            IsChecksumValid = checksumValid
        };

        Packets.Add(packet);
        if (Packets.Count > MaxPackets)
            Packets.RemoveAt(0);
    }

    private byte[] ComputeChecksum(byte[] data)
    {
        return ChecksumMode switch
        {
            "XOR" => new[] { data.Aggregate((byte)0, (a, b) => (byte)(a ^ b)) },
            "Sum8" => new[] { (byte)(data.Sum(b => b) & 0xFF) },
            "CRC16" => ComputeCrc16(data),
            _ => Array.Empty<byte>()
        };
    }

    private static byte[] ComputeCrc16(byte[] data)
    {
        // CRC-16/ARC (IBM)
        ushort crc = 0x0000;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return new[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
    }

    private static byte ParseHexByte(string hex, byte fallback)
    {
        try { return Convert.ToByte(hex.TrimStart('0', 'x', 'X').PadLeft(2, '0')[..2], 16); }
        catch { return fallback; }
    }

    // --- IsEnabled が変わったらバッファをクリア ---
    partial void OnIsEnabledChanged(bool value)
    {
        if (!value) _parseBuffer.Clear();
    }

    public void LoadFromSettings(ProtocolSettings s)
    {
        IsEnabled = s.IsEnabled;
        DelimiterMode = s.DelimiterMode;
        StxHex = s.StxHex;
        EtxHex = s.EtxHex;
        FixedLength = s.FixedLength;
        ChecksumMode = s.ChecksumMode;
    }

    public ProtocolSettings GetSettings() => new()
    {
        IsEnabled = IsEnabled,
        DelimiterMode = DelimiterMode,
        StxHex = StxHex,
        EtxHex = EtxHex,
        FixedLength = FixedLength,
        ChecksumMode = ChecksumMode
    };

    [RelayCommand]
    private void ClearPackets()
    {
        Packets.Clear();
        _parseBuffer.Clear();
    }

    public void Dispose()
    {
        _timer.Stop();
        _serialPortService.DataReceived -= OnDataReceived;
    }
}
