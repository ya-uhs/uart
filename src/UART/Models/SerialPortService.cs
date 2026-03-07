using System;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace UART.Models;

/// <summary>
/// シリアル通信の核心ロジック。
/// DataReceivedイベントはバックグラウンドスレッドで発火する。
/// </summary>
public class SerialPortService : IDisposable
{
    private SerialPort? _port;

    /// <summary>受信データイベント（バックグラウンドスレッドで発火）</summary>
    public event EventHandler<byte[]>? DataReceived;

    /// <summary>ポートが予期せず切断されたときのイベント</summary>
    public event EventHandler? Disconnected;

    public bool IsConnected => _port?.IsOpen ?? false;

    public void Open(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits, Handshake handshake)
    {
        if (_port?.IsOpen == true)
            throw new InvalidOperationException("すでに接続中です");

        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            Handshake = handshake,
            ReadBufferSize = 65536,
            WriteTimeout = 5000,
        };
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;
        _port.Open();
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_port == null || !_port.IsOpen) return;
            var count = _port.BytesToRead;
            if (count <= 0) return;
            var buffer = new byte[count];
            _port.Read(buffer, 0, count);
            DataReceived?.Invoke(this, buffer);
        }
        catch (Exception)
        {
            // ポートが閉じられた場合など
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        if (e.EventType == SerialError.TXFull || e.EventType == SerialError.Overrun) return;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Send(byte[] data)
    {
        if (_port?.IsOpen != true)
            throw new InvalidOperationException("接続されていません");
        _port.Write(data, 0, data.Length);
    }

    public void Close()
    {
        if (_port != null)
        {
            _port.DataReceived -= OnDataReceived;
            _port.ErrorReceived -= OnErrorReceived;
            if (_port.IsOpen)
                _port.Close();
            _port.Dispose();
            _port = null;
        }
    }

    public void Dispose() => Close();

    /// <summary>
    /// プラットフォーム別のシリアルポート一覧取得。
    /// Linux: /dev/ttyUSB*, /dev/ttyACM* を優先して返す。
    /// </summary>
    public static string[] GetPortNames()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var ports = new System.Collections.Generic.List<string>();
                if (Directory.Exists("/dev"))
                {
                    ports.AddRange(Directory.GetFiles("/dev", "ttyUSB*"));
                    ports.AddRange(Directory.GetFiles("/dev", "ttyACM*"));
                    ports.AddRange(Directory.GetFiles("/dev", "ttyS*").Take(8));
                }
                return ports.Distinct().OrderBy(p => p).ToArray();
            }
            return SerialPort.GetPortNames().OrderBy(p => p).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
