using CommunityToolkit.Mvvm.ComponentModel;

namespace UART.Models;

/// <summary>
/// Luaプラグインのデータモデル
/// </summary>
public partial class LuaPlugin : ObservableObject
{
    [ObservableProperty]
    private string _name = "New Plugin";

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>最後のロード/実行エラーメッセージ（空なら正常）</summary>
    [ObservableProperty]
    private string _lastError = "";
}
