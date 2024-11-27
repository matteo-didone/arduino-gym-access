using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;
using ArduinoGymAccess.Services;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;

namespace ArduinoGymAccess.Controllers
{
    public class SerialDataParser
    {
        private const string RFID_PREFIX = "RFID:";

        public class SerialData
        {
            public string Type { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public bool IsValid { get; set; }
            public string Error { get; set; } = string.Empty;
        }

        public static SerialData Parse(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData))
            {
                return new SerialData { IsValid = false, Error = "Dati ricevuti vuoti o nulli" };
            }

            try
            {
                if (rawData.StartsWith(RFID_PREFIX))
                {
                    string rfidCode = rawData.Substring(RFID_PREFIX.Length).Trim();
                    return new SerialData
                    {
                        Type = "RFID",
                        Value = rfidCode,
                        IsValid = IsValidRfidFormat(rfidCode)
                    };
                }

                return new SerialData { IsValid = false, Error = "Formato dati non riconosciuto" };
            }
            catch (Exception ex)
            {
                return new SerialData { IsValid = false, Error = $"Errore nel parsing dei dati: {ex.Message}" };
            }
        }

        private static bool IsValidRfidFormat(string rfidCode)
        {
            return !string.IsNullOrWhiteSpace(rfidCode) &&
                   rfidCode.Length >= 8 &&
                   rfidCode.All(c => char.IsLetterOrDigit(c));
        }
    }

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

        private async void HandleDataReceived(object sender, string data)
        {
            try
            {
                _logger.LogInformation($"Dati ricevuti dalla porta seriale: {data}");

                var parsedData = SerialDataParser.Parse(data);

                if (!parsedData.IsValid)
                {
                    _logger.LogWarning($"Dati non validi ricevuti: {parsedData.Error}");
                    return;
                }

                if (parsedData.Type == "RFID")
                {
                    _logger.LogInformation($"RFID valido ricevuto: {parsedData.Value}");
                    await HandleRfidData(parsedData.Value); // Gestisci i dati ricevuti
                    await SendRfidDataToApi(parsedData.Value); // Invia i dati al backend
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Errore durante la gestione dei dati seriali: {ex.Message}");
            }
        }

        private async Task HandleRfidData(string rfidCode)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var rfidToken = await context.RfidTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.RfidCode == rfidCode);

                bool isGranted = rfidToken?.IsActive == true && rfidToken.User.IsActive == true;

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

                    _logger.LogInformation(
                        "Tentativo di accesso: Utente={User}, Autorizzato={IsGranted}",
                        rfidToken.User.Name,
                        isGranted);
                }
                else
                {
                    _logger.LogWarning($"Tentativo di accesso con token sconosciuto: {rfidCode}");
                }

                _serialPortManager.SendData(isGranted ? "A" : "N");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Errore nella gestione dei dati RFID: {ex.Message}");
            }
        }

        private async Task SendRfidDataToApi(string rfidCode)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5074/api/serial/rfid-tokens/verify");
            var payload = new { RfidCode = rfidCode };

            try
            {
                _logger.LogInformation($"Invio del codice RFID {rfidCode} al backend...");
                var response = await client.PostAsJsonAsync("", payload);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"POST successo: {result}");
                }
                else
                {
                    _logger.LogError($"Errore POST: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Errore durante la chiamata POST: {ex.Message}");
            }
        }

        [HttpGet("ports")]
        public IActionResult GetAvailablePorts()
        {
            return Ok(_serialPortManager.GetAvailablePorts());
        }

        [HttpPost("rfid-tokens/verify")]
        public async Task<IActionResult> VerifyRfidToken([FromBody] RfidTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RfidCode))
            {
                return BadRequest(new { message = "RFID Code non valido" });
            }

            using var context = await _contextFactory.CreateDbContextAsync();
            var rfidToken = await context.RfidTokens
                .FirstOrDefaultAsync(rt => rt.RfidCode == request.RfidCode);

            if (rfidToken == null)
            {
                return NotFound(new { message = "Token non trovato" });
            }

            return Ok(new
            {
                message = "Token verificato",
                isActive = rfidToken.IsActive
            });
        }

        public class RfidTokenRequest
        {
            public string RfidCode { get; set; } = string.Empty;
        }
    }
}
