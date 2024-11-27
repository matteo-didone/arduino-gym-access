using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;
using ArduinoGymAccess.Utilities;
using Microsoft.EntityFrameworkCore;

namespace ArduinoGymAccess.Services
{
    public class SerialEventHandler : IHostedService
    {
        private readonly SerialPortManager _serialPortManager;
        private readonly ILogger<SerialEventHandler> _logger;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SerialEventHandler(
            SerialPortManager serialPortManager,
            ILogger<SerialEventHandler> logger,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _serialPortManager = serialPortManager;
            _logger = logger;
            _contextFactory = contextFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Avvio SerialEventHandler...");
            _serialPortManager.DataReceived += HandleDataReceived;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Arresto SerialEventHandler...");
            _serialPortManager.DataReceived -= HandleDataReceived;
            return Task.CompletedTask;
        }

        private async void HandleDataReceived(object? sender, string data)
        {
            try
            {
                var parsedData = SerialDataParser.Parse(data);

                if (!parsedData.IsValid)
                {
                    _logger.LogWarning($"[HandleDataReceived] Dati non validi: {parsedData.Error}");
                    return;
                }

                if (parsedData.Type == "RFID")
                {
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
                _serialPortManager.SendData("N");
                return (false, $"Errore: {ex.Message}");
            }
        }
    }
}