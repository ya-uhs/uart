using CommunityToolkit.Mvvm.ComponentModel;

namespace UART.Models;

/// <summary>
/// 受信トリガーのデータモデル
/// </summary>
public partial class TriggerItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "New Trigger";

    /// <summary>受信データにこの文字列が含まれたら発火</summary>
    [ObservableProperty]
    private string _pattern = "";

    /// <summary>発火時に送信するコマンド</summary>
    [ObservableProperty]
    private string _response = "";

    /// <summary>改行コード: None / CR / LF / CRLF</summary>
    [ObservableProperty]
    private string _newLine = "CRLF";

    [ObservableProperty]
    private bool _isEnabled = true;
}
