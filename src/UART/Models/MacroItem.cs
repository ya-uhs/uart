using CommunityToolkit.Mvvm.ComponentModel;

namespace UART.Models;

/// <summary>
/// マクロボタンのデータモデル
/// </summary>
public partial class MacroItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "New Macro";

    [ObservableProperty]
    private string _command = "";

    /// <summary>改行コード: None / CR / LF / CRLF</summary>
    [ObservableProperty]
    private string _newLine = "CRLF";
}
