using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UART.Models;

namespace UART.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly SerialPortService _serialPortService;

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private int _selectedBaudRate = 115200;

    [ObservableProperty]
    private int _selectedDataBits = 8;

    [ObservableProperty]
    private string _selectedParity = "None";

    [ObservableProperty]
    private string _selectedStopBits = "One";

    [ObservableProperty]
    private string _selectedHandshake = "None";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorColor))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "Disconnected";

    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";
    public string StatusIndicatorColor => IsConnected ? "#50FA7B" : "#F38BA8";
    public string StatusBackground => IsConnected ? "#1A3A1A" : "#3A1A1A";

    public List<int> BaudRates { get; } = new() { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };
    public List<int> DataBitsList { get; } = new() { 5, 6, 7, 8 };
    public List<string> ParityList { get; } = new() { "None", "Odd", "Even", "Mark", "Space" };
    public List<string> StopBitsList { get; } = new() { "One", "OnePointFive", "Two" };
    public List<string> HandshakeList { get; } = new() { "None", "XOnXOff", "RequestToSend", "RequestToSendXOnXOff" };

    public ConnectionViewModel(SerialPortService serialPortService)
    {
        _serialPortService = serialPortService;
        _serialPortService.Disconnected += OnUnexpectedDisconnect;
        RefreshPorts();
    }

    private void OnUnexpectedDisconnect(object? sender, EventArgs e)
    {
        IsConnected = false;
        StatusMessage = "Disconnected (lost)";
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts = new ObservableCollection<string>(SerialPortService.GetPortNames());
        if (AvailablePorts.Count > 0 && (SelectedPort == null || !AvailablePorts.Contains(SelectedPort)))
            SelectedPort = AvailablePorts[0];
    }

    [RelayCommand]
    private void ToggleConnection()
    {
        if (IsConnected)
            Disconnect();
        else
            Connect();
    }

    private void Connect()
    {
        if (SelectedPort == null)
        {
            StatusMessage = "Select a port";
            return;
        }

        try
        {
            var parity = Enum.Parse<Parity>(SelectedParity);
            var stopBits = Enum.Parse<StopBits>(SelectedStopBits);
            var handshake = Enum.Parse<Handshake>(SelectedHandshake);

            _serialPortService.Open(SelectedPort, SelectedBaudRate, SelectedDataBits, parity, stopBits, handshake);
            IsConnected = true;
            StatusMessage = $"Connected: {SelectedPort} @ {SelectedBaudRate}bps";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void Disconnect()
    {
        _serialPortService.Close();
        IsConnected = false;
        StatusMessage = "Disconnected";
    }

    public void LoadSettings(SessionSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.PortName))
            SelectedPort = settings.PortName;
        SelectedBaudRate = settings.BaudRate;
        SelectedDataBits = settings.DataBits;
        SelectedParity = settings.Parity;
        SelectedStopBits = settings.StopBits;
        SelectedHandshake = settings.Handshake;
    }

    public void SaveSettings(SessionSettings settings)
    {
        settings.PortName = SelectedPort ?? "";
        settings.BaudRate = SelectedBaudRate;
        settings.DataBits = SelectedDataBits;
        settings.Parity = SelectedParity;
        settings.StopBits = SelectedStopBits;
        settings.Handshake = SelectedHandshake;
    }
}
