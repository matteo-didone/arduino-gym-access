using System.IO.Ports;
using Microsoft.Extensions.Options;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Services
{
    public class SerialPortManager : IDisposable
    {
        private readonly SerialPortSettings _settings;
        private SerialPort _serialPort;
        private readonly ILogger<SerialPortManager> _logger;
        private readonly Timer _reconnectTimer;
        private bool _disposed;

        public event EventHandler<string>? DataReceived;
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public SerialPortManager(IOptions<SerialPortSettings> settings, ILogger<SerialPortManager> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _serialPort = InitializeSerialPort();
            _reconnectTimer = new Timer(TryReconnect, null, Timeout.Infinite, Timeout.Infinite);

            if (_settings.AutoReconnect)
            {
                ConnectToPort(_settings.PortName);
            }
        }

        private SerialPort InitializeSerialPort()
        {
            var port = new SerialPort
            {
                PortName = _settings.PortName,
                BaudRate = _settings.BaudRate,
                DataBits = _settings.DataBits,
                Parity = _settings.Parity,
                StopBits = _settings.StopBits,
                ReadTimeout = _settings.ReadTimeout,
                WriteTimeout = _settings.WriteTimeout
            };

            port.DataReceived += (sender, e) =>
            {
                if (port.IsOpen)
                {
                    string data = port.ReadLine().Trim();
                    DataReceived?.Invoke(this, data);
                }
            };

            return port;
        }

        public bool ConnectToPort(string portName)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }

                _serialPort = InitializeSerialPort();
                _serialPort.PortName = portName;
                _serialPort.Open();

                if (_settings.AutoReconnect)
                {
                    _reconnectTimer.Change(_settings.ReconnectInterval, _settings.ReconnectInterval);
                }

                _logger.LogInformation($"Connected to port {portName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to connect to port {portName}");
                return false;
            }
        }

        private void TryReconnect(object? state)
        {
            if (!IsConnected && !_disposed)
            {
                _logger.LogInformation("Attempting to reconnect...");
                ConnectToPort(_settings.PortName);
            }
        }

        public void SendData(string data)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.WriteLine(data);
                    _logger.LogInformation($"Data sent: {data}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending data");
            }
        }

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _reconnectTimer?.Dispose();
                    if (_serialPort?.IsOpen == true)
                    {
                        _serialPort.Close();
                    }
                    _serialPort?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}