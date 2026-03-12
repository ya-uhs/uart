using System;
using System.Linq;
using System.Text;
using MoonSharp.Interpreter;

namespace UART.Models;

/// <summary>
/// Luaスクリプトに公開するUART操作API。
/// MoonSharpのUserDataとして登録し、スクリプト内から uart.xxx() で呼び出す。
///
/// Luaから使用できる関数:
///   uart.send(text)          -- テキストをCRLF付きで送信
///   uart.send_raw(text)      -- テキストをそのまま送信（改行なし）
///   uart.send_bytes(hex)     -- HEX文字列 "AA BB CC" をバイト列として送信
///   uart.log(text)           -- ターミナルにログメッセージを表示
///   uart.is_connected()      -- 接続中なら true を返す
/// </summary>
[MoonSharpUserData]
public class UartApi
{
    private readonly SerialPortService _serialPortService;
    private readonly Action<string> _log;

    internal UartApi(SerialPortService serialPortService, Action<string> logAction)
    {
        _serialPortService = serialPortService;
        _log = logAction;
    }

    /// <summary>テキストをUTF-8 + CRLFで送信する</summary>
    public void send(string text)
    {
        if (!_serialPortService.IsConnected) return;
        var bytes = Encoding.UTF8.GetBytes(text + "\r\n");
        _serialPortService.Send(bytes);
    }

    /// <summary>テキストをそのまま送信する（改行コードを付けない）</summary>
    public void send_raw(string text)
    {
        if (!_serialPortService.IsConnected) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        _serialPortService.Send(bytes);
    }

    /// <summary>HEX文字列 "AA BB CC" をバイト列に変換して送信する</summary>
    public void send_bytes(string hexStr)
    {
        if (!_serialPortService.IsConnected) return;
        var parts = hexStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = parts.Select(p => Convert.ToByte(p, 16)).ToArray();
        _serialPortService.Send(bytes);
    }

    /// <summary>ターミナルにログメッセージを追記する</summary>
    public void log(string text) => _log(text);

    /// <summary>シリアルポートが接続中かどうかを返す</summary>
    public bool is_connected() => _serialPortService.IsConnected;
}
