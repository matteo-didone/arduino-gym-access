using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccessLogsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AccessLogsController> _logger;

        public AccessLogsController(AppDbContext context, ILogger<AccessLogsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/access-logs
        [HttpGet]
        public async Task<ActionResult<object>> GetLogs(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] bool? granted,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _context.AccessLogs
                    .Include(al => al.RfidToken)
                        .ThenInclude(rt => rt.User)
                    .AsQueryable();

                // Filtri temporali
                if (from.HasValue)
                    query = query.Where(al => al.AccessTime >= from.Value);
                
                if (to.HasValue)
                    query = query.Where(al => al.AccessTime <= to.Value);

                // Filtro per stato di accesso
                if (granted.HasValue)
                    query = query.Where(al => al.IsGranted == granted.Value);

                // Ricerca per utente o codice RFID
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(al =>
                        al.RfidToken.RfidCode.Contains(search) ||
                        al.RfidToken.User.Name.Contains(search) ||
                        al.RfidToken.User.Email.Contains(search));

                // Conteggio totale per paginazione
                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                // Applicazione paginazione
                var logs = await query
                    .OrderByDescending(al => al.AccessTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(al => new
                    {
                        al.Id,
                        al.AccessTime,
                        al.AccessStatus,
                        al.IsGranted,
                        al.DeniedReason,
                        RfidToken = new
                        {
                            al.RfidToken.Id,
                            al.RfidToken.RfidCode
                        },
                        User = new
                        {
                            al.RfidToken.User.Id,
                            al.RfidToken.User.Name,
                            al.RfidToken.User.Email
                        }
                    })
                    .ToListAsync();

                // Statistiche generali
                var statistics = new
                {
                    TotalAccesses = totalItems,
                    GrantedAccesses = await query.CountAsync(al => al.IsGranted),
                    DeniedAccesses = await query.CountAsync(al => !al.IsGranted),
                    UniqueUsers = await query
                        .Select(al => al.RfidToken.UserId)
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
                _logger.LogError(ex, "Error retrieving access logs");
                return StatusCode(500, "Internal server error while retrieving logs");
            }
        }

        // GET: api/access-logs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetLog(int id)
        {
            try
            {
                var log = await _context.AccessLogs
                    .Include(al => al.RfidToken)
                        .ThenInclude(rt => rt.User)
                    .Where(al => al.Id == id)
                    .Select(al => new
                    {
                        al.Id,
                        al.AccessTime,
                        al.AccessStatus,
                        al.IsGranted,
                        al.DeniedReason,
                        RfidToken = new
                        {
                            al.RfidToken.Id,
                            al.RfidToken.RfidCode,
                            al.RfidToken.IsActive
                        },
                        User = new
                        {
                            al.RfidToken.User.Id,
                            al.RfidToken.User.Name,
                            al.RfidToken.User.Email,
                            al.RfidToken.User.IsActive
                        }
                    })
                    .FirstOrDefaultAsync();

                if (log == null)
                {
                    return NotFound($"Access log with ID {id} not found");
                }

                return Ok(log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving access log {LogId}", id);
                return StatusCode(500, "Internal server error while retrieving log details");
            }
        }

        // GET: api/access-logs/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetStatistics(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            try
            {
                var query = _context.AccessLogs
                    .Include(al => al.RfidToken)
                        .ThenInclude(rt => rt.User)
                    .AsQueryable();

                if (from.HasValue)
                    query = query.Where(al => al.AccessTime >= from.Value);
                
                if (to.HasValue)
                    query = query.Where(al => al.AccessTime <= to.Value);

                // Statistiche generali
                var totalAccesses = await query.CountAsync();
                var grantedAccesses = await query.CountAsync(al => al.IsGranted);
                var deniedAccesses = totalAccesses - grantedAccesses;
                var uniqueUsers = await query
                    .Select(al => al.RfidToken.UserId)
                    .Distinct()
                    .CountAsync();

                // Statistiche per ora del giorno
                var accessesByHour = await query
                    .GroupBy(al => al.AccessTime.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        TotalAccesses = g.Count(),
                        GrantedAccesses = g.Count(al => al.IsGranted),
                        DeniedAccesses = g.Count(al => !al.IsGranted)
                    })
                    .OrderBy(g => g.Hour)
                    .ToListAsync();

                // Statistiche per giorno della settimana
                var accessesByDayOfWeek = await query
                    .GroupBy(al => al.AccessTime.DayOfWeek)
                    .Select(g => new
                    {
                        DayOfWeek = g.Key,
                        TotalAccesses = g.Count(),
                        GrantedAccesses = g.Count(al => al.IsGranted),
                        DeniedAccesses = g.Count(al => !al.IsGranted)
                    })
                    .OrderBy(g => g.DayOfWeek)
                    .ToListAsync();

                // Top utenti
                var topUsers = await query
                    .GroupBy(al => new { al.RfidToken.UserId, al.RfidToken.User.Name })
                    .Select(g => new
                    {
                        UserId = g.Key.UserId,
                        UserName = g.Key.Name,
                        TotalAccesses = g.Count(),
                        GrantedAccesses = g.Count(al => al.IsGranted),
                        DeniedAccesses = g.Count(al => !al.IsGranted)
                    })
                    .OrderByDescending(u => u.TotalAccesses)
                    .Take(10)
                    .ToListAsync();

                return Ok(new
                {
                    Summary = new
                    {
                        TotalAccesses = totalAccesses,
                        GrantedAccesses = grantedAccesses,
                        DeniedAccesses = deniedAccesses,
                        UniqueUsers = uniqueUsers,
                        Period = new
                        {
                            From = from ?? await query.MinAsync(al => al.AccessTime),
                            To = to ?? await query.MaxAsync(al => al.AccessTime)
                        }
                    },
                    AccessesByHour = accessesByHour,
                    AccessesByDayOfWeek = accessesByDayOfWeek,
                    TopUsers = topUsers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving access statistics");
                return StatusCode(500, "Internal server error while retrieving statistics");
            }
        }

        // Opzionale: Endpoint per eliminare vecchi log
        [HttpDelete("cleanup")]
        public async Task<IActionResult> CleanupOldLogs([FromQuery] int daysToKeep = 90)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                var oldLogs = await _context.AccessLogs
                    .Where(al => al.AccessTime < cutoffDate)
                    .ToListAsync();

                if (oldLogs.Any())
                {
                    _context.AccessLogs.RemoveRange(oldLogs);
                    await _context.SaveChangesAsync();
                    return Ok(new
                    {
                        DeletedCount = oldLogs.Count,
                        Message = $"Successfully deleted {oldLogs.Count} logs older than {cutoffDate}"
                    });
                }

                return Ok(new
                {
                    DeletedCount = 0,
                    Message = "No logs found to delete"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old access logs");
                return StatusCode(500, "Internal server error while cleaning up logs");
            }
        }
    }
}