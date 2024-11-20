using System.IO.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SerialController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SerialController> _logger;
        private static SerialPort _serialPort;

        public SerialController(AppDbContext context, ILogger<SerialController> logger)
        {
            _context = context;
            _logger = logger;

            if (_serialPort == null || !_serialPort.IsOpen)
            {
                InitializeSerialPort();
            }
        }

        private void InitializeSerialPort()
        {
            try
            {
                _serialPort = new SerialPort()
                {
                    PortName = "COM3", // Modifica con la tua porta
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                _logger.LogInformation("Porta seriale aperta con successo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'apertura della porta seriale");
            }
        }

        private async void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadLine().Trim();
                _logger.LogInformation($"Dati ricevuti: {data}");

                // Verifica il token RFID
                var rfidToken = await _context.RfidTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.RfidCode == data);

                bool isGranted = rfidToken?.IsActive == true && rfidToken.User.IsActive == true;
                string response = isGranted ? "A" : "N"; // A = Accesso consentito, N = Negato

                // Log dell'accesso
                if (rfidToken != null)
                {
                    var accessLog = new AccessLog
                    {
                        RfidTokenId = rfidToken.Id,
                        AccessTime = DateTime.UtcNow,
                        AccessStatus = isGranted ? AccessStatus.AUTHORIZED : AccessStatus.UNAUTHORIZED,
                        IsGranted = isGranted
                    };

                    _context.AccessLogs.Add(accessLog);
                    await _context.SaveChangesAsync();
                }

                // Invia risposta ad Arduino
                _serialPort.WriteLine(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella gestione dei dati seriali");
            }
        }

        [HttpGet("ports")]
        public IActionResult GetAvailablePorts()
        {
            return Ok(SerialPort.GetPortNames());
        }

        [HttpPost("open")]
        public IActionResult OpenPort([FromBody] SerialPortRequest request)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort = new SerialPort()
                {
                    PortName = request.PortName,
                    BaudRate = request.BaudRate,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                return Ok(new { message = "Porta seriale aperta con successo" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("close")]
        public IActionResult ClosePort()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                    return Ok(new { message = "Porta seriale chiusa con successo" });
                }
                return BadRequest(new { message = "La porta seriale è già chiusa" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class SerialPortRequest
    {
        public string PortName { get; set; } = "COM3";
        public int BaudRate { get; set; } = 9600;
    }
}