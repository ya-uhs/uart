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
    public List<TriggerItemData> Triggers { get; set; } = new();
    public ProtocolSettings Protocol { get; set; } = new();
    public GraphSettings Graph { get; set; } = new();
    public List<LuaPluginData> LuaPlugins { get; set; } = new();
}

public class GraphSettings
{
    public bool IsEnabled { get; set; } = false;
    public string Pattern { get; set; } = @"[-+]?\d+(?:\.\d+)?";
    public int MaxPoints { get; set; } = 500;
}

public class ProtocolSettings
{
    public bool IsEnabled { get; set; } = false;
    public string DelimiterMode { get; set; } = "Newline";
    public string StxHex { get; set; } = "02";
    public string EtxHex { get; set; } = "03";
    public int FixedLength { get; set; } = 10;
    public string ChecksumMode { get; set; } = "None";
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

/// <summary>
/// JSON保存用Luaプラグインデータ
/// </summary>
public class LuaPluginData
{
    public string Name { get; set; } = "New Plugin";
    public string FilePath { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// JSON保存用トリガーデータ
/// </summary>
public class TriggerItemData
{
    public string Name { get; set; } = "New Trigger";
    public string Pattern { get; set; } = "";
    public string Response { get; set; } = "";
    public string NewLine { get; set; } = "CRLF";
    public bool IsEnabled { get; set; } = true;
}
