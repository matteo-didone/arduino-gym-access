using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;
using ArduinoGymAccess.Services;
using ArduinoGymAccess.Utilities;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;

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

            _logger.LogInformation("Registrazione dell'evento DataReceived...");
            _serialPortManager.DataReceived += HandleDataReceived;
        }

        private async void HandleDataReceived(object? sender, string data)
        {
            try
            {
                _logger.LogInformation($"[HandleDataReceived] Dati ricevuti dalla porta seriale: {data}");

                var parsedData = SerialDataParser.Parse(data);
                _logger.LogInformation($"[HandleDataReceived] Dati parsati: Tipo={parsedData.Type}, Validi={parsedData.IsValid}, Valore={parsedData.Value}");

                if (!parsedData.IsValid)
                {
                    _logger.LogWarning($"[HandleDataReceived] Dati non validi: {parsedData.Error}");
                    return;
                }

                if (parsedData.Type == "RFID")
                {
                    _logger.LogInformation($"[HandleDataReceived] Inizio gestione del codice RFID: {parsedData.Value}");
                    var (isGranted, userName) = await HandleRfidData(parsedData.Value);

                    _logger.LogInformation(
                        "[HandleDataReceived] Risultato verifica RFID: Accesso {Status} per {User}",
                        isGranted ? "AUTORIZZATO" : "NON AUTORIZZATO",
                        userName
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[HandleDataReceived] Errore durante la gestione dei dati seriali: {ex.Message}");
            }
        }

        private async Task<(bool isGranted, string userName)> HandleRfidData(string rfidCode)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var rfidToken = await context.RfidTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.RfidCode == rfidCode);

                if (rfidToken != null)
                {
                    bool isGranted = rfidToken.IsActive && (rfidToken.User?.IsActive ?? false);
                    string userName = rfidToken.User?.Name ?? "Sconosciuto";

                    var accessLog = new AccessLog
                    {
                        RfidTokenId = rfidToken.Id,
                        AccessTime = DateTime.UtcNow,
                        AccessStatus = isGranted ? AccessStatus.AUTHORIZED : AccessStatus.UNAUTHORIZED,
                        IsGranted = isGranted
                    };

                    context.AccessLogs.Add(accessLog);
                    await context.SaveChangesAsync();

                    _logger.LogInformation(
                        "[HandleRfidData] Accesso {Status} per utente {User}",
                        isGranted ? "AUTORIZZATO" : "NON AUTORIZZATO",
                        userName
                    );

                    _serialPortManager.SendData(isGranted ? "A" : "N");

                    return (isGranted, userName);
                }
                else
                {
                    _logger.LogWarning($"[HandleRfidData] Token sconosciuto: {rfidCode}");
                    _serialPortManager.SendData("N");
                    return (false, "Token non riconosciuto");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[HandleRfidData] Errore nella gestione del codice RFID: {ex.Message}");
                return (false, $"Errore: {ex.Message}");
            }
        }

        private async Task SendRfidDataToApi(string rfidCode)
        {
            try
            {
                using var client = new HttpClient();
                var url = "http://localhost:5074/api/serial/rfid-tokens/verify";

                var payload = new
                {
                    RfidCode = rfidCode
                };

                _logger.LogInformation($"[SendRfidDataToApi] Tentativo di invio POST: RFID={rfidCode}");
                var response = await client.PostAsJsonAsync(url, payload);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[SendRfidDataToApi] POST successo per RFID={rfidCode}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[SendRfidDataToApi] Errore POST: StatusCode={response.StatusCode}, Messaggio={errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SendRfidDataToApi] Errore durante la chiamata POST: {ex.Message}");
            }
        }

        [HttpGet("ports")]
        public IActionResult GetAvailablePorts()
        {
            _logger.LogInformation("[GetAvailablePorts] Recupero delle porte disponibili...");
            return Ok(_serialPortManager.GetAvailablePorts());
        }

        [HttpPost("rfid-tokens/verify")]
        public async Task<IActionResult> VerifyRfidToken([FromBody] RfidTokenRequest request)
        {
            _logger.LogInformation($"[VerifyRfidToken] Verifica del token RFID: {request.RfidCode}");

            if (string.IsNullOrWhiteSpace(request.RfidCode))
            {
                _logger.LogWarning("[VerifyRfidToken] RFID Code non valido");
                return BadRequest(new { message = "RFID Code non valido" });
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var rfidToken = await context.RfidTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.RfidCode == request.RfidCode);

                if (rfidToken == null)
                {
                    _logger.LogWarning("[VerifyRfidToken] Token non trovato");
                    return NotFound(new { message = "Token non trovato" });
                }

                _logger.LogInformation("[VerifyRfidToken] Token verificato con successo");
                return Ok(new
                {
                    message = "Token verificato",
                    isActive = rfidToken.IsActive,
                    userName = rfidToken.User?.Name ?? "Utente sconosciuto"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[VerifyRfidToken] Errore durante la verifica del token RFID: {ex.Message}");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        public class RfidTokenRequest
        {
            public string RfidCode { get; set; } = string.Empty;
        }
    }
}
