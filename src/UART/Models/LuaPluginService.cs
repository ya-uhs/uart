using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Threading;
using MoonSharp.Interpreter;

namespace UART.Models;

/// <summary>
/// Luaプラグインエンジンの管理クラス。
/// 各プラグインに独立したMoonSharp Scriptインスタンスを持ち、
/// シリアル受信データをコールバック経由でLuaスクリプトに通知する。
///
/// スレッド設計:
///   DataReceived（バックグラウンド）→ ConcurrentQueue → DispatcherTimer（UIスレッド）→ Luaコールバック
/// </summary>
public class LuaPluginService : IDisposable
{
    private readonly SerialPortService _serialPortService;
    private readonly Action<string> _logToTerminal;

    // バックグラウンドスレッドから受け取る受信データキュー
    private readonly ConcurrentQueue<byte[]> _receiveQueue = new();

    // UIスレッドで実行するタイマー
    private readonly DispatcherTimer _updateTimer;

    // ロード済みプラグインとそのScriptエンジン
    private readonly Dictionary<LuaPlugin, Script> _scripts = new();

    public LuaPluginService(SerialPortService serialPortService, Action<string> logToTerminal)
    {
        _serialPortService = serialPortService;
        _logToTerminal = logToTerminal;

        // MoonSharp UserDataの登録
        UserData.RegisterType<UartApi>();

        _serialPortService.DataReceived += OnDataReceived;
        _serialPortService.Disconnected += OnSerialDisconnected;

        // 50ms間隔でLuaコールバックを処理（UIスレッド）
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _updateTimer.Tick += ProcessReceiveQueue;
        _updateTimer.Start();
    }

    // ─── シリアル受信ハンドラ（バックグラウンドスレッド）────────────────────

    private void OnDataReceived(object? sender, byte[] data)
    {
        _receiveQueue.Enqueue(data);
    }

    private void OnSerialDisconnected(object? sender, EventArgs e)
    {
        // UIスレッドで on_disconnect を呼ぶ
        Dispatcher.UIThread.Post(NotifyDisconnected);
    }

    // ─── UIスレッド処理 ─────────────────────────────────────────────────────

    private void ProcessReceiveQueue(object? sender, EventArgs e)
    {
        if (_receiveQueue.IsEmpty) return;

        var chunks = new List<byte[]>();
        while (_receiveQueue.TryDequeue(out var chunk))
            chunks.Add(chunk);

        var totalLen = chunks.Sum(c => c.Length);
        var combined = new byte[totalLen];
        int offset = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(combined, offset);
            offset += chunk.Length;
        }

        var hexStr = BitConverter.ToString(combined).Replace("-", " ");
        var asciiStr = Encoding.UTF8.GetString(combined);

        foreach (var (plugin, script) in _scripts.ToList())
        {
            if (!plugin.IsEnabled) continue;
            CallLuaFunction(plugin, script, "on_receive", hexStr, asciiStr);
        }
    }

    // ─── プラグイン管理 API ──────────────────────────────────────────────────

    /// <summary>プラグインをロードしてスクリプトエンジンを初期化する</summary>
    public void LoadPlugin(LuaPlugin plugin)
    {
        UnloadPlugin(plugin);

        if (string.IsNullOrEmpty(plugin.FilePath))
        {
            plugin.LastError = "ファイルパスが未設定です";
            return;
        }
        if (!File.Exists(plugin.FilePath))
        {
            plugin.LastError = $"ファイルが見つかりません: {plugin.FilePath}";
            return;
        }

        try
        {
            // HardSandbox: ファイルI/O・OS操作を禁止し安全な実行環境を提供
            var script = new Script(CoreModules.Preset_HardSandbox);
            var api = new UartApi(_serialPortService, text => _logToTerminal($"[LUA:{plugin.Name}] {text}"));
            script.Globals["uart"] = api;

            var code = File.ReadAllText(plugin.FilePath, Encoding.UTF8);
            script.DoString(code);

            _scripts[plugin] = script;
            plugin.LastError = "";
        }
        catch (Exception ex)
        {
            plugin.LastError = ex.Message;
        }
    }

    /// <summary>プラグインのスクリプトエンジンをアンロードする</summary>
    public void UnloadPlugin(LuaPlugin plugin)
    {
        _scripts.Remove(plugin);
    }

    /// <summary>プラグインをファイルから再読み込みする</summary>
    public void ReloadPlugin(LuaPlugin plugin)
    {
        LoadPlugin(plugin);
    }

    /// <summary>接続時にすべての有効プラグインへ on_connect を呼び出す</summary>
    public void NotifyConnected(string port, int baud)
    {
        foreach (var (plugin, script) in _scripts.ToList())
        {
            if (!plugin.IsEnabled) continue;
            CallLuaFunction(plugin, script, "on_connect", port, (double)baud);
        }
    }

    /// <summary>切断時にすべての有効プラグインへ on_disconnect を呼び出す</summary>
    public void NotifyDisconnected()
    {
        foreach (var (plugin, script) in _scripts.ToList())
        {
            if (!plugin.IsEnabled) continue;
            CallLuaFunction(plugin, script, "on_disconnect");
        }
    }

    // ─── 内部ヘルパー ────────────────────────────────────────────────────────

    private void CallLuaFunction(LuaPlugin plugin, Script script, string funcName, params object[] args)
    {
        try
        {
            var func = script.Globals.Get(funcName);
            if (func.Type == DataType.Function)
                script.Call(func, args);
        }
        catch (Exception ex)
        {
            plugin.LastError = ex.Message;
        }
    }

    public void Dispose()
    {
        _updateTimer.Stop();
        _serialPortService.DataReceived -= OnDataReceived;
        _serialPortService.Disconnected -= OnSerialDisconnected;
        _scripts.Clear();
    }
}
