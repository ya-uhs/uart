# UART - Unified Avalonia Realtime Terminal

## 概要
組み込みエンジニア向けのマルチプラットフォーム対応シリアル通信GUIツール。
TeraTerm・PuTTY等の既存ツールにない「マクロ送信」「HEX/ASCII表示」を中心に、
開発者が日常的なシリアルデバッグを効率化することを目的とする。

詳細な要件は @docs/requirements.md を参照すること。

---

## 技術スタック

- **UIフレームワーク**: Avalonia UI 11.x
- **アーキテクチャ**: MVVM（CommunityToolkit.Mvvm）
- **シリアル通信**: System.IO.Ports
- **ターゲット**: .NET 8
- **言語**: C#
- **設定保存**: System.Text.Json

---

## ディレクトリ構成

```
UART/
├── CLAUDE.md
├── docs/
│   └── requirements.md
├── src/
│   └── UART/
│       ├── UART.csproj
│       ├── App.axaml
│       ├── App.axaml.cs
│       ├── Models/
│       │   ├── SerialPortService.cs      # シリアル通信の核心ロジック
│       │   ├── MacroItem.cs              # マクロデータモデル
│       │   └── SessionSettings.cs       # セッション設定モデル
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   ├── TerminalViewModel.cs      # 受信・表示ロジック
│       │   ├── MacroViewModel.cs         # マクロ管理
│       │   └── ConnectionViewModel.cs   # 接続設定
│       └── Views/
│           ├── MainWindow.axaml
│           ├── MainWindow.axaml.cs
│           ├── TerminalView.axaml
│           ├── MacroView.axaml
│           └── ConnectionView.axaml
└── UART.sln
```

---

## アーキテクチャ方針

### MVVMの徹底
- `Views/`: XAMLとcode-behindのみ。ロジックは書かない
- `ViewModels/`: バインディング・コマンド・UI状態管理のみ
- `Models/`: シリアル通信・データ処理・永続化ロジック

### スレッド設計（最重要）
シリアル受信はバックグラウンドスレッドで発生するため、以下のパターンを厳守する。

```csharp
// 受信データはConcurrentQueueに積む（シリアル受信スレッド）
private readonly ConcurrentQueue<byte[]> _receiveBuffer = new();

// DispatcherTimerで16ms間隔でまとめてUI更新（UIスレッド）
_updateTimer = new DispatcherTimer
{
    Interval = TimeSpan.FromMilliseconds(16)
};
_updateTimer.Tick += FlushReceiveBuffer;

// UIスレッドへの更新はInvokeAsync経由
await Dispatcher.UIThread.InvokeAsync(() => { ... });
```

- 受信のたびにUIを直接叩かないこと
- `DataReceived`イベント内でUIプロパティを変更しないこと

---

## コーディング規約

- 非同期処理は `async/await` で統一（`Task.Run` は最小限に）
- `CancellationToken` を適切に使いリソースリークを防ぐ
- `IDisposable` を実装してシリアルポートを確実にクローズする
- nullableを有効化し、null参照例外を防ぐ
- コメントは日本語でOK

---

## v1.0 実装スコープ（これだけ実装する）

以下の機能のみ実装する。スコープ外の機能は実装しないこと。

### 必須機能
1. **接続管理**
   - COMポートの自動検出・一覧表示
   - ボーレート・データビット・パリティ・ストップビット設定
   - 接続 / 切断のワンクリック操作

2. **HEX / ASCII 切替表示**
   - 受信データをHEXまたはASCIIで表示
   - 切替はリアルタイムで反映
   - タイムスタンプ表示

3. **マクロ・コマンド送信**
   - マクロボタン（名前・送信文字列・改行コードを設定）
   - マクロのJSON保存・読み込み
   - 送信履歴（↑↓キーで呼び出し）

4. **セッション保存**
   - 接続設定・マクロをJSONで保存・復元

### スコープ外（v2.0以降）
- リアルタイムグラフ（ScottPlot）
- 送受信トリガー
- プロトコル解析
- ログエクスポート

---

## ビルド・確認手順

実装後は必ず以下を実行してエラーがないことを確認すること。

```bash
cd src/UART
dotnet build
dotnet run
```

---

## その他

- UIテーマはダークモード対応
- 日本語環境で動作すること（フォント・文字コードに注意）
- `SerialPort.GetPortNames()` はWindows・Linux・macOSで挙動が異なるため、
  プラットフォーム別の処理を考慮すること
