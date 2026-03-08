using System;

namespace UART.Models;

/// <summary>
/// プロトコル解析で切り出された1パケット
/// </summary>
public class ParsedPacket
{
    public DateTime Timestamp { get; init; }

    /// <summary>ペイロードバイト列（デリミタ・チェックサムを除く）</summary>
    public byte[] Data { get; init; } = Array.Empty<byte>();

    /// <summary>チェックサム検証結果（null = チェックサム無効）</summary>
    public bool? IsChecksumValid { get; init; }

    public string HexString => BitConverter.ToString(Data).Replace("-", " ");

    public string TimestampStr => Timestamp.ToString("HH:mm:ss.fff");

    public string StatusLabel => IsChecksumValid switch
    {
        true => "CHK:OK ",
        false => "CHK:ERR",
        null => "       "
    };

    public string Display =>
        $"[{TimestampStr}] {StatusLabel}  len={Data.Length,3}  {HexString}";
}
