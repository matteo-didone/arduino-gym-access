using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;
using ArduinoGymAccess.Services;
using System;
using System.Threading.Tasks;

namespace ArduinoGymAccess.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SerialController : ControllerBase
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<SerialController> _logger;
        private readonly SerialPortManager _serialPortManager;

        public SerialController(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<SerialController> logger,
            SerialPortManager serialPortManager)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _serialPortManager = serialPortManager;
            _serialPortManager.DataReceived += HandleDataReceived;
        }

        private async void HandleDataReceived(object sender, string data)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                _logger.LogInformation($"Dati ricevuti: {data}");

                var rfidToken = await context.RfidTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.RfidCode == data);

                bool isGranted = rfidToken?.IsActive == true && rfidToken.User.IsActive == true;
                string response = isGranted ? "A" : "N";

                if (rfidToken != null)
                {
                    var accessLog = new AccessLog
                    {
                        RfidTokenId = rfidToken.Id,
                        AccessTime = DateTime.UtcNow,
                        AccessStatus = isGranted ? AccessStatus.AUTHORIZED : AccessStatus.UNAUTHORIZED,
                        IsGranted = isGranted
                    };

                    context.AccessLogs.Add(accessLog);
                    await context.SaveChangesAsync();
                }

                _serialPortManager.SendData(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella gestione dei dati seriali");
            }
        }

        [HttpGet("ports")]
        public IActionResult GetAvailablePorts()
        {
            return Ok(_serialPortManager.GetAvailablePorts());
        }

        [HttpPost("open")]
        public IActionResult OpenPort([FromBody] SerialPortRequest request)
        {
            try
            {
                bool success = _serialPortManager.ConnectToPort(request.PortName);
                return success 
                    ? Ok(new { message = "Porta seriale aperta con successo" })
                    : BadRequest(new { message = "Impossibile aprire la porta seriale" });
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
                if (_serialPortManager.IsConnected)
                {
                    _serialPortManager.Dispose();
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

    // Questa classe definisce la struttura della richiesta per l'apertura della porta seriale
    public class SerialPortRequest
    {
        public string PortName { get; set; } = "COM5";  // Valore predefinito per la porta COM
        public int BaudRate { get; set; } = 9600;       // Baud rate standard per Arduino
    }
}