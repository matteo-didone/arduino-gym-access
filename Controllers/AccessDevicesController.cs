using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccessDevicesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AccessDevicesController> _logger;

        public AccessDevicesController(AppDbContext context, ILogger<AccessDevicesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/access-devices
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetDevices([FromQuery] string? search)
        {
            try
            {
                var query = _context.AccessDevices
                    .Include(d => d.DeviceLogs)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(d => 
                        d.DeviceName.Contains(search) || 
                        d.Location.Contains(search));
                }

                var devices = await query
                    .Select(d => new
                    {
                        d.Id,
                        d.DeviceName,
                        d.Location,
                        d.CreatedAt,
                        LastActivity = d.DeviceLogs
                            .OrderByDescending(dl => dl.LogTime)
                            .FirstOrDefault(),
                        TotalAccesses = d.DeviceLogs.Count
                    })
                    .ToListAsync();

                return Ok(devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving access devices");
                return StatusCode(500, "Internal server error while retrieving devices");
            }
        }

        // GET: api/access-devices/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetDevice(int id)
        {
            try
            {
                var device = await _context.AccessDevices
                    .Include(d => d.DeviceLogs)
                        .ThenInclude(dl => dl.RfidToken)
                            .ThenInclude(rt => rt.User)
                    .Where(d => d.Id == id)
                    .Select(d => new
                    {
                        d.Id,
                        d.DeviceName,
                        d.Location,
                        d.CreatedAt,
                        Statistics = new
                        {
                            TotalAccesses = d.DeviceLogs.Count,
                            UniqueUsers = d.DeviceLogs
                                .Where(dl => dl.RfidToken != null)
                                .Select(dl => dl.RfidToken.UserId)
                                .Distinct()
                                .Count(),
                            LastAccess = d.DeviceLogs
                                .OrderByDescending(dl => dl.LogTime)
                                .FirstOrDefault()
                        },
                        RecentLogs = d.DeviceLogs
                            .OrderByDescending(dl => dl.LogTime)
                            .Take(10)
                            .Select(dl => new
                            {
                                dl.Id,
                                dl.LogTime,
                                User = dl.RfidToken != null ? new
                                {
                                    dl.RfidToken.User.Id,
                                    dl.RfidToken.User.Name,
                                    dl.RfidToken.RfidCode
                                } : null
                            })
                    })
                    .FirstOrDefaultAsync();

                if (device == null)
                {
                    return NotFound($"Device with ID {id} not found");
                }

                return Ok(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving device {DeviceId}", id);
                return StatusCode(500, "Internal server error while retrieving device details");
            }
        }

        // POST: api/access-devices
        [HttpPost]
        public async Task<ActionResult<AccessDevice>> CreateDevice([FromBody] CreateDeviceRequest request)
        {
            try
            {
                var device = new AccessDevice
                {
                    DeviceName = request.DeviceName,
                    Location = request.Location,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AccessDevices.Add(device);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating access device");
                return StatusCode(500, "Internal server error while creating device");
            }
        }

        // PUT: api/access-devices/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDevice(int id, [FromBody] UpdateDeviceRequest request)
        {
            try
            {
                var device = await _context.AccessDevices.FindAsync(id);
                if (device == null)
                {
                    return NotFound($"Device with ID {id} not found");
                }

                device.DeviceName = request.DeviceName;
                device.Location = request.Location;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device {DeviceId}", id);
                return StatusCode(500, "Internal server error while updating device");
            }
        }

        // GET: api/access-devices/5/logs
        [HttpGet("{id}/logs")]
        public async Task<ActionResult<object>> GetDeviceLogs(
            int id,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            try
            {
                var device = await _context.AccessDevices.FindAsync(id);
                if (device == null)
                {
                    return NotFound($"Device with ID {id} not found");
                }

                var query = _context.DeviceLogs
                    .Include(dl => dl.RfidToken)
                        .ThenInclude(rt => rt.User)
                    .Where(dl => dl.DeviceId == id);

                if (from.HasValue)
                    query = query.Where(dl => dl.LogTime >= from.Value);

                if (to.HasValue)
                    query = query.Where(dl => dl.LogTime <= to.Value);

                var logs = await query
                    .OrderByDescending(dl => dl.LogTime)
                    .Select(dl => new
                    {
                        dl.Id,
                        dl.LogTime,
                        User = dl.RfidToken != null ? new
                        {
                            dl.RfidToken.User.Id,
                            dl.RfidToken.User.Name,
                            dl.RfidToken.RfidCode
                        } : null
                    })
                    .ToListAsync();

                return Ok(new
                {
                    DeviceId = id,
                    DeviceName = device.DeviceName,
                    Location = device.Location,
                    TotalLogs = logs.Count,
                    UniqueUsers = logs
                        .Where(l => l.User != null)
                        .Select(l => l.User.Id)
                        .Distinct()
                        .Count(),
                    Logs = logs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs for device {DeviceId}", id);
                return StatusCode(500, "Internal server error while retrieving device logs");
            }
        }
    }

    public class CreateDeviceRequest
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class UpdateDeviceRequest
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}