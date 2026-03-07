using System;

namespace UART.Models;

/// <summary>
/// ターミナル表示エントリ（送受信ログの1レコード）
/// </summary>
public class TerminalEntry
{
    public DateTime Timestamp { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    /// <summary>true=送信, false=受信</summary>
    public bool IsSent { get; set; }
}
