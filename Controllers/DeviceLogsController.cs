using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceLogsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DeviceLogsController> _logger;

        public DeviceLogsController(AppDbContext context, ILogger<DeviceLogsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/device-logs
        [HttpGet]
        public async Task<ActionResult<object>> GetLogs(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? deviceId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _context.DeviceLogs
                    .Include(dl => dl.Device)
                    .Include(dl => dl.RfidToken)
                        .ThenInclude(rt => rt.User)
                    .AsQueryable();

                // Filtri temporali
                if (from.HasValue)
                    query = query.Where(dl => dl.LogTime >= from.Value);

                if (to.HasValue)
                    query = query.Where(dl => dl.LogTime <= to.Value);

                // Filtro per dispositivo specifico
                if (deviceId.HasValue)
                    query = query.Where(dl => dl.DeviceId == deviceId.Value);

                // Ricerca per nome dispositivo, utente o codice RFID
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(dl =>
                        dl.Device.DeviceName.Contains(search) ||
                        dl.Device.Location.Contains(search) ||
                        (dl.RfidToken != null && (
                            dl.RfidToken.RfidCode.Contains(search) ||
                            dl.RfidToken.User.Name.Contains(search) ||
                            dl.RfidToken.User.Email.Contains(search)
                        ))
                    );

                // Conteggio totale per paginazione
                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                // Applicazione paginazione
                var logs = await query
                    .OrderByDescending(dl => dl.LogTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(dl => new
                    {
                        dl.Id,
                        dl.LogTime,
                        Device = new
                        {
                            dl.Device.Id,
                            dl.Device.DeviceName,
                            dl.Device.Location
                        },
                        RfidToken = dl.RfidToken == null ? null : new
                        {
                            dl.RfidToken.Id,
                            dl.RfidToken.RfidCode,
                            User = new
                            {
                                dl.RfidToken.User.Id,
                                dl.RfidToken.User.Name,
                                dl.RfidToken.User.Email
                            }
                        }
                    })
                    .ToListAsync();

                // Statistiche
                var statistics = new
                {
                    TotalLogs = totalItems,
                    UniqueDevices = await query.Select(dl => dl.DeviceId).Distinct().CountAsync(),
                    UniqueUsers = await query
                        .Where(dl => dl.RfidToken != null)
                        .Select(dl => dl.RfidToken.UserId)
                        .Distinct()
                        .CountAsync()
                };

                return Ok(new
                {
                    Logs = logs,
                    Pagination = new
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = totalItems,
                        TotalPages = totalPages
                    },
                    Statistics = statistics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving device logs");
                return StatusCode(500, "Internal server error while retrieving logs");
            }
        }

        // GET: api/device-logs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetLog(int id)
        {
            try
            {
                var log = await _context.DeviceLogs
                    .Include(dl => dl.Device)
                    .Include(dl => dl.RfidToken)
                        .ThenInclude(rt => rt.User)
                    .Where(dl => dl.Id == id)
                    .Select(dl => new
                    {
                        dl.Id,
                        dl.LogTime,
                        Device = new
                        {
                            dl.Device.Id,
                            dl.Device.DeviceName,
                            dl.Device.Location
                        },
                        RfidToken = dl.RfidToken == null ? null : new
                        {
                            dl.RfidToken.Id,
                            dl.RfidToken.RfidCode,
                            User = new
                            {
                                dl.RfidToken.User.Id,
                                dl.RfidToken.User.Name,
                                dl.RfidToken.User.Email
                            }
                        }
                    })
                    .FirstOrDefaultAsync();

                if (log == null)
                {
                    return NotFound($"Device log with ID {id} not found");
                }

                return Ok(log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving device log {LogId}", id);
                return StatusCode(500, "Internal server error while retrieving log details");
            }
        }

        // POST: api/device-logs
        [HttpPost]
        public async Task<ActionResult<object>> CreateLog([FromBody] CreateDeviceLogRequest request)
        {
            try
            {
                // Verifica che il dispositivo esista
                var device = await _context.AccessDevices.FindAsync(request.DeviceId);
                if (device == null)
                {
                    return NotFound($"Device with ID {request.DeviceId} not found");
                }

                // Verifica del token RFID se fornito
                RfidToken? rfidToken = null;
                if (!string.IsNullOrEmpty(request.RfidCode))
                {
                    rfidToken = await _context.RfidTokens
                        .Include(rt => rt.User)
                        .FirstOrDefaultAsync(rt => rt.RfidCode == request.RfidCode);
                }

                var deviceLog = new DeviceLog
                {
                    DeviceId = request.DeviceId,
                    RfidTokenId = rfidToken?.Id,
                    LogTime = DateTime.UtcNow
                };

                _context.DeviceLogs.Add(deviceLog);
                await _context.SaveChangesAsync();

                // Se c'è un token RFID, crea anche un AccessLog
                if (rfidToken != null)
                {
                    var accessLog = new AccessLog
                    {
                        RfidTokenId = rfidToken.Id,
                        AccessTime = deviceLog.LogTime,
                        IsGranted = rfidToken.IsActive && rfidToken.User.IsActive,
                        AccessStatus = rfidToken.IsActive && rfidToken.User.IsActive 
                            ? AccessStatus.AUTHORIZED 
                            : AccessStatus.UNAUTHORIZED,
                        DeniedReason = !rfidToken.IsActive 
                            ? "Inactive RFID token" 
                            : !rfidToken.User.IsActive 
                                ? "Inactive user" 
                                : null
                    };

                    _context.AccessLogs.Add(accessLog);
                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction(nameof(GetLog), new { id = deviceLog.Id },
                    await GetLog(deviceLog.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating device log");
                return StatusCode(500, "Internal server error while creating log");
            }
        }

        // GET: api/device-logs/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetStatistics(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? deviceId)
        {
            try
            {
                var query = _context.DeviceLogs
                    .Include(dl => dl.Device)
                    .Include(dl => dl.RfidToken)
                        .ThenInclude(rt => rt.User)
                    .AsQueryable();

                if (from.HasValue)
                    query = query.Where(dl => dl.LogTime >= from.Value);

                if (to.HasValue)
                    query = query.Where(dl => dl.LogTime <= to.Value);

                if (deviceId.HasValue)
                    query = query.Where(dl => dl.DeviceId == deviceId.Value);

                // Statistiche per dispositivo
                var deviceStats = await query
                    .GroupBy(dl => new { dl.DeviceId, dl.Device.DeviceName, dl.Device.Location })
                    .Select(g => new
                    {
                        DeviceId = g.Key.DeviceId,
                        DeviceName = g.Key.DeviceName,
                        Location = g.Key.Location,
                        TotalLogs = g.Count(),
                        UniqueUsers = g.Where(dl => dl.RfidToken != null)
                            .Select(dl => dl.RfidToken.UserId)
                            .Distinct()
                            .Count(),
                        FirstLog = g.Min(dl => dl.LogTime),
                        LastLog = g.Max(dl => dl.LogTime)
                    })
                    .ToListAsync();

                // Statistiche orarie
                var hourlyStats = await query
                    .GroupBy(dl => dl.LogTime.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        TotalLogs = g.Count()
                    })
                    .OrderBy(x => x.Hour)
                    .ToListAsync();

                // Statistiche giornaliere
                var dailyStats = await query
                    .GroupBy(dl => EF.Functions.DatePart("weekday", dl.LogTime))
                    .Select(g => new
                    {
                        DayOfWeek = g.Key,
                        TotalLogs = g.Count()
                    })
                    .OrderBy(x => x.DayOfWeek)
                    .ToListAsync();

                return Ok(new
                {
                    Summary = new
                    {
                        TotalLogs = await query.CountAsync(),
                        UniqueDevices = await query.Select(dl => dl.DeviceId).Distinct().CountAsync(),
                        UniqueUsers = await query
                            .Where(dl => dl.RfidToken != null)
                            .Select(dl => dl.RfidToken.UserId)
                            .Distinct()
                            .CountAsync(),
                        Period = new
                        {
                            From = from ?? await query.Min(dl => dl.LogTime),
                            To = to ?? await query.Max(dl => dl.LogTime)
                        }
                    },
                    DeviceStatistics = deviceStats,
                    HourlyStatistics = hourlyStats,
                    DailyStatistics = dailyStats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving device log statistics");
                return StatusCode(500, "Internal server error while retrieving statistics");
            }
        }
    }

    public class CreateDeviceLogRequest
    {
        public int DeviceId { get; set; }
        public string? RfidCode { get; set; }
    }
}