using System.Collections.Generic;

namespace UART.Models;

/// <summary>
/// セッション設定（JSON保存用）
/// </summary>
public class SessionSettings
{
    public string PortName { get; set; } = "";
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public string DisplayMode { get; set; } = "ASCII";
    public bool AutoScroll { get; set; } = true;
    public List<MacroItemData> Macros { get; set; } = new();
}

/// <summary>
/// JSON保存用マクロデータ（ObservableObjectを継承しない純粋なデータクラス）
/// </summary>
public class MacroItemData
{
    public string Name { get; set; } = "New Macro";
    public string Command { get; set; } = "";
    public string NewLine { get; set; } = "CRLF";
}
