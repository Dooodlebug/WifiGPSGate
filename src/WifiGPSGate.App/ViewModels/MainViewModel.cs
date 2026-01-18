using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WifiGPSGate.Core.Interfaces;
using WifiGPSGate.Core.Models;
using WifiGPSGate.Core.Orchestration;
using WifiGPSGate.IO.Sinks;
using WifiGPSGate.IO.Sources;

namespace WifiGPSGate.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;
    private SessionManager? _sessionManager;
    private System.Timers.Timer? _statusTimer;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Stopped";

    [ObservableProperty]
    private string _healthStatus = "Unknown";

    [ObservableProperty]
    private string _dataAge = "-";

    [ObservableProperty]
    private string _dataRate = "0.0 Hz";

    [ObservableProperty]
    private string _statisticsText = "";

    // Input settings
    [ObservableProperty]
    private bool _useUdpInput = true;

    [ObservableProperty]
    private bool _useTcpInput;

    [ObservableProperty]
    private int _udpPort = 9001;

    [ObservableProperty]
    private string _tcpHost = "192.168.1.1";

    [ObservableProperty]
    private int _tcpPort = 9001;

    // Output settings
    [ObservableProperty]
    private bool _enableSerialOutput = true;

    [ObservableProperty]
    private string _selectedSerialPort = "";

    [ObservableProperty]
    private int _selectedBaudRate = 115200;

    [ObservableProperty]
    private bool _enableFileOutput;

    [ObservableProperty]
    private string _logFilePath = "";

    // UDP broadcast output
    [ObservableProperty]
    private bool _enableUdpBroadcastOutput;

    [ObservableProperty]
    private string _udpBroadcastAddress = "255.255.255.255";

    [ObservableProperty]
    private int _udpBroadcastPort = 9002;

    // Virtual COM port output
    [ObservableProperty]
    private bool _enableVirtualComOutput;

    [ObservableProperty]
    private string _virtualComPortName = "COM10";

    [ObservableProperty]
    private bool _virtualComAutoMode = true;

    // Available options
    [ObservableProperty]
    private ObservableCollection<string> _availableSerialPorts = new();

    public ObservableCollection<int> AvailableBaudRates { get; } = new(Presets.CommonBaudRates);

    // NMEA preview
    [ObservableProperty]
    private ObservableCollection<string> _nmeaPreview = new();

    private const int MaxPreviewLines = 50;

    public MainViewModel(ILogger? logger = null)
    {
        _logger = logger?.ForContext<MainViewModel>() ?? Log.Logger.ForContext<MainViewModel>();

        RefreshSerialPorts();

        _statusTimer = new System.Timers.Timer(500);
        _statusTimer.Elapsed += (s, e) => UpdateStatus();
        _statusTimer.Start();
    }

    [RelayCommand]
    private void RefreshSerialPorts()
    {
        try
        {
            var ports = SerialDataSink.GetAvailablePorts();
            Application.Current?.Dispatcher.Invoke(() =>
            {
                AvailableSerialPorts.Clear();
                foreach (var port in ports.OrderBy(p => p))
                {
                    AvailableSerialPorts.Add(port);
                }

                if (string.IsNullOrEmpty(SelectedSerialPort) && AvailableSerialPorts.Count > 0)
                {
                    SelectedSerialPort = AvailableSerialPorts[0];
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to refresh serial ports");
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning) return;

        try
        {
            var config = BuildConfiguration();

            _sessionManager = new SessionManager(
                CreateDataSource,
                CreateDataSink,
                _logger);

            _sessionManager.StateChanged += OnSessionStateChanged;
            _sessionManager.SentenceReceived += OnSentenceReceived;

            await _sessionManager.StartAsync(config);

            IsRunning = true;
            StatusText = "Running";
            _logger.Information("Session started");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start session");
            StatusText = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to start: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!IsRunning || _sessionManager == null) return;

        try
        {
            await _sessionManager.StopAsync();
            _sessionManager.StateChanged -= OnSessionStateChanged;
            _sessionManager.SentenceReceived -= OnSentenceReceived;
            await _sessionManager.DisposeAsync();
            _sessionManager = null;

            IsRunning = false;
            StatusText = "Stopped";
            HealthStatus = "Unknown";
            DataAge = "-";
            DataRate = "0.0 Hz";
            _logger.Information("Session stopped");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error stopping session");
        }
    }

    [RelayCommand]
    private void BrowseLogFile()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "NMEA Log Files (*.nmea)|*.nmea|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".nmea",
            FileName = $"nmea_log_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() == true)
        {
            LogFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ClearPreview()
    {
        Application.Current?.Dispatcher.Invoke(() => NmeaPreview.Clear());
    }

    [RelayCommand]
    private void ApplyEmlidPreset()
    {
        UseUdpInput = true;
        UseTcpInput = false;
        UdpPort = 9001;
        EnableSerialOutput = true;
        SelectedBaudRate = 115200;
    }

    private SessionConfiguration BuildConfiguration()
    {
        InputConfiguration input = UseUdpInput
            ? new UdpInputConfiguration { Port = UdpPort }
            : new TcpClientInputConfiguration { Host = TcpHost, Port = TcpPort };

        var outputs = new List<OutputConfiguration>();

        if (EnableSerialOutput && !string.IsNullOrEmpty(SelectedSerialPort))
        {
            outputs.Add(new SerialOutputConfiguration
            {
                PortName = SelectedSerialPort,
                BaudRate = SelectedBaudRate
            });
        }

        if (EnableFileOutput && !string.IsNullOrEmpty(LogFilePath))
        {
            outputs.Add(new FileOutputConfiguration
            {
                FilePath = LogFilePath,
                AppendTimestamp = true
            });
        }

        if (EnableUdpBroadcastOutput && !string.IsNullOrEmpty(UdpBroadcastAddress))
        {
            outputs.Add(new UdpBroadcastOutputConfiguration
            {
                DestinationAddress = UdpBroadcastAddress,
                Port = UdpBroadcastPort,
                EnableBroadcast = true
            });
        }

        if (EnableVirtualComOutput && !string.IsNullOrEmpty(VirtualComPortName))
        {
            outputs.Add(new VirtualComOutputConfiguration
            {
                PortName = VirtualComPortName,
                AutoMode = VirtualComAutoMode
            });
        }

        if (outputs.Count == 0)
        {
            throw new InvalidOperationException("At least one output must be enabled");
        }

        return new SessionConfiguration
        {
            Input = input,
            Outputs = outputs,
            Filter = new SentenceFilterConfiguration { Mode = FilterMode.AllowAll }
        };
    }

    private IDataSource CreateDataSource(InputConfiguration config)
    {
        return config switch
        {
            UdpInputConfiguration udp => new UdpDataSource(udp, _logger),
            TcpClientInputConfiguration tcp => new TcpClientDataSource(tcp, _logger),
            _ => throw new NotSupportedException($"Input type {config.Type} not supported")
        };
    }

    private IDataSink CreateDataSink(OutputConfiguration config)
    {
        return config switch
        {
            SerialOutputConfiguration serial => new SerialDataSink(serial, _logger),
            FileOutputConfiguration file => new FileDataSink(file, _logger),
            UdpBroadcastOutputConfiguration udp => new UdpDataSink(udp, _logger),
            VirtualComOutputConfiguration vcom => new VirtualComDataSink(vcom, _logger),
            _ => throw new NotSupportedException($"Output type {config.Type} not supported")
        };
    }

    private void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = e.NewState.ToString();
            if (e.NewState == SessionState.Error)
            {
                StatusText = $"Error: {e.ErrorMessage}";
            }
        });
    }

    private void OnSentenceReceived(object? sender, NmeaSentence sentence)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            NmeaPreview.Insert(0, sentence.ToString());

            while (NmeaPreview.Count > MaxPreviewLines)
            {
                NmeaPreview.RemoveAt(NmeaPreview.Count - 1);
            }
        });
    }

    private void UpdateStatus()
    {
        if (_sessionManager == null || !IsRunning) return;

        var stats = _sessionManager.Statistics;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (stats.LastDataReceivedTime.HasValue)
            {
                var age = DateTimeOffset.UtcNow - stats.LastDataReceivedTime.Value;
                DataAge = $"{age.TotalSeconds:F1}s";

                HealthStatus = age.TotalSeconds switch
                {
                    < 2 => "Healthy",
                    < 5 => "Stale",
                    _ => "Error"
                };
            }

            DataRate = $"{stats.CurrentRateHz:F1} Hz";

            StatisticsText = $"Rx: {stats.SentencesReceived} | Tx: {stats.SentencesSent} | " +
                            $"Errors: {stats.ChecksumErrors + stats.ParseErrors + stats.WriteErrors}";
        });
    }

    public void LoadSettings(UserSettings settings)
    {
        UseUdpInput = settings.UseUdpInput;
        UseTcpInput = settings.UseTcpInput;
        UdpPort = settings.UdpPort;
        TcpHost = settings.TcpHost;
        TcpPort = settings.TcpPort;

        EnableSerialOutput = settings.EnableSerialOutput;
        SelectedSerialPort = settings.SelectedSerialPort;
        SelectedBaudRate = settings.SelectedBaudRate;

        EnableFileOutput = settings.EnableFileOutput;
        LogFilePath = settings.LogFilePath;

        EnableUdpBroadcastOutput = settings.EnableUdpBroadcastOutput;
        UdpBroadcastAddress = settings.UdpBroadcastAddress;
        UdpBroadcastPort = settings.UdpBroadcastPort;

        EnableVirtualComOutput = settings.EnableVirtualComOutput;
        VirtualComPortName = settings.VirtualComPortName;
        VirtualComAutoMode = settings.VirtualComAutoMode;

        _logger.Information("Settings loaded into view model");
    }

    public UserSettings GetCurrentSettings()
    {
        return new UserSettings
        {
            UseUdpInput = UseUdpInput,
            UseTcpInput = UseTcpInput,
            UdpPort = UdpPort,
            TcpHost = TcpHost,
            TcpPort = TcpPort,

            EnableSerialOutput = EnableSerialOutput,
            SelectedSerialPort = SelectedSerialPort,
            SelectedBaudRate = SelectedBaudRate,

            EnableFileOutput = EnableFileOutput,
            LogFilePath = LogFilePath,

            EnableUdpBroadcastOutput = EnableUdpBroadcastOutput,
            UdpBroadcastAddress = UdpBroadcastAddress,
            UdpBroadcastPort = UdpBroadcastPort,

            EnableVirtualComOutput = EnableVirtualComOutput,
            VirtualComPortName = VirtualComPortName,
            VirtualComAutoMode = VirtualComAutoMode
        };
    }

    public void Dispose()
    {
        _statusTimer?.Stop();
        _statusTimer?.Dispose();

        if (_sessionManager != null)
        {
            _sessionManager.StopAsync().GetAwaiter().GetResult();
            _sessionManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
