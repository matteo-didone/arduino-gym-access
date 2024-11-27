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
                ReadTimeout = _settings.ReadTimeout > 0 ? _settings.ReadTimeout : 1000,
                WriteTimeout = _settings.WriteTimeout > 0 ? _settings.WriteTimeout : 500
            };

            port.DataReceived += (sender, e) =>
            {
                try
                {
                    if (port.IsOpen)
                    {
                        _logger.LogInformation($"[SerialPortManager] DataReceived event triggered from port: {port.PortName}");
                        string data = port.ReadLine().Trim();
                        _logger.LogInformation($"[SerialPortManager] Raw data received: {data}");
                        DataReceived?.Invoke(this, data);
                    }
                }
                catch (TimeoutException ex)
                {
                    _logger.LogWarning(ex, "[SerialPortManager] Timeout reading from serial port");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SerialPortManager] Error during DataReceived event");
                }
            };

            return port;
        }

        public void ConnectToPort(string portName)
        {
            try
            {
                if (_serialPort?.IsOpen == true && _serialPort.PortName == portName)
                {
                    _logger.LogInformation($"[SerialPortManager] Port {portName} is already open");
                    return;
                }

                _serialPort?.Close();
                _serialPort = InitializeSerialPort();
                _serialPort.PortName = portName;
                _serialPort.Open();

                if (_settings.AutoReconnect)
                {
                    _reconnectTimer.Change(_settings.ReconnectInterval, _settings.ReconnectInterval);
                }

                _logger.LogInformation($"[SerialPortManager] Connected to port {portName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SerialPortManager] Failed to connect to port {portName}");
            }
        }


        private void TryReconnect(object? state)
        {
            if (!IsConnected && !_disposed)
            {
                _logger.LogInformation("[SerialPortManager] Attempting to reconnect...");
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
                    _logger.LogInformation($"[SerialPortManager] Data sent: {data}");
                }
                else
                {
                    _logger.LogWarning("[SerialPortManager] Cannot send data: Serial port is not open");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SerialPortManager] Error sending data");
            }
        }

        public string[] GetAvailablePorts()
        {
            _logger.LogInformation("[SerialPortManager] Retrieving available ports");
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
