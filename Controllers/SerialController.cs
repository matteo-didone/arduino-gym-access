using Microsoft.AspNetCore.Mvc;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;
using ArduinoGymAccess.Services;
using Microsoft.EntityFrameworkCore;

namespace ArduinoGymAccess.Controllers
{
    /// <summary>
    /// Controller per la gestione della comunicazione seriale con Arduino
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SerialController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SerialController> _logger;
        private readonly SerialPortManager _serialPortManager;

        /// <summary>
        /// Costruttore che inizializza il controller con le sue dipendenze
        /// </summary>
        public SerialController(
            AppDbContext context,
            ILogger<SerialController> logger,
            SerialPortManager serialPortManager)
        {
            _context = context;
            _logger = logger;
            _serialPortManager = serialPortManager;

            // Registriamo il gestore eventi per i dati in arrivo dalla porta seriale
            _serialPortManager.DataReceived += HandleDataReceived;
        }

        /// <summary>
        /// Gestisce i dati ricevuti dalla porta seriale
        /// Verifica il token RFID e registra l'accesso nel database
        /// </summary>
        private async void HandleDataReceived(object sender, string data)
        {
            try
            {
                _logger.LogInformation($"Dati ricevuti: {data}");

                // Cerchiamo il token RFID nel database, includendo i dati dell'utente associato
                var rfidToken = await _context.RfidTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.RfidCode == data);

                // Verifichiamo che sia il token che l'utente siano attivi
                bool isGranted = rfidToken?.IsActive == true && rfidToken.User.IsActive == true;
                string response = isGranted ? "A" : "N"; // A = Accesso consentito, N = Negato

                // Se il token esiste (anche se non attivo), registriamo il tentativo di accesso
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

                // Inviamo la risposta ad Arduino
                _serialPortManager.SendData(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella gestione dei dati seriali");
            }
        }

        /// <summary>
        /// Endpoint GET che restituisce l'elenco delle porte seriali disponibili
        /// </summary>
        [HttpGet("ports")]
        public IActionResult GetAvailablePorts()
        {
            return Ok(_serialPortManager.GetAvailablePorts());
        }

        /// <summary>
        /// Endpoint POST per aprire una connessione sulla porta seriale specificata
        /// </summary>
        [HttpPost("open")]
        public IActionResult OpenPort([FromBody] SerialPortRequest request)
        {
            try
            {
                bool success = _serialPortManager.ConnectToPort(request.PortName);
                if (success)
                {
                    return Ok(new { message = "Porta seriale aperta con successo" });
                }
                return BadRequest(new { message = "Impossibile aprire la porta seriale" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint POST per chiudere la connessione seriale corrente
        /// </summary>
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

    /// <summary>
    /// Classe di richiesta per l'apertura della porta seriale
    /// </summary>
    public class SerialPortRequest
    {
        public string PortName { get; set; } = "COM5";  // Valore predefinito aggiornato alla porta corretta
        public int BaudRate { get; set; } = 9600;       // Baud rate standard per la maggior parte delle comunicazioni Arduino
    }
}