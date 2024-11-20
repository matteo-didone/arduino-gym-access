using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RfidTokensController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RfidTokensController> _logger;

        public RfidTokensController(AppDbContext context, ILogger<RfidTokensController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/rfid-tokens
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetTokens(
            [FromQuery] string? search,
            [FromQuery] bool? active)
        {
            try
            {
                var query = _context.RfidTokens
                    .Include(rt => rt.User)
                    .Include(rt => rt.AccessLogs)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(rt =>
                        rt.RfidCode.Contains(search) ||
                        rt.User.Name.Contains(search) ||
                        rt.User.Email.Contains(search));
                }

                if (active.HasValue)
                {
                    query = query.Where(rt => rt.IsActive == active.Value);
                }

                var tokens = await query
                    .Select(rt => new
                    {
                        rt.Id,
                        rt.RfidCode,
                        rt.IsActive,
                        rt.CreatedAt,
                        User = new
                        {
                            rt.User.Id,
                            rt.User.Name,
                            rt.User.Email
                        },
                        LastAccess = rt.AccessLogs
                            .OrderByDescending(al => al.AccessTime)
                            .Select(al => new
                            {
                                al.AccessTime,
                                al.AccessStatus,
                                al.IsGranted
                            })
                            .FirstOrDefault(),
                        AccessCount = rt.AccessLogs.Count
                    })
                    .ToListAsync();

                return Ok(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving RFID tokens");
                return StatusCode(500, "Internal server error while retrieving tokens");
            }
        }

        // GET: api/rfid-tokens/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetToken(int id)
        {
            try
            {
                var token = await _context.RfidTokens
                    .Include(rt => rt.User)
                    .Include(rt => rt.AccessLogs)
                    .Where(rt => rt.Id == id)
                    .Select(rt => new
                    {
                        rt.Id,
                        rt.RfidCode,
                        rt.IsActive,
                        rt.CreatedAt,
                        User = new
                        {
                            rt.User.Id,
                            rt.User.Name,
                            rt.User.Email,
                            rt.User.IsActive
                        },
                        AccessLogs = rt.AccessLogs
                            .OrderByDescending(al => al.AccessTime)
                            .Take(10)
                            .Select(al => new
                            {
                                al.Id,
                                al.AccessTime,
                                al.AccessStatus,
                                al.IsGranted,
                                al.DeniedReason
                            })
                    })
                    .FirstOrDefaultAsync();

                if (token == null)
                {
                    return NotFound($"RFID token with ID {id} not found");
                }

                return Ok(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving RFID token {TokenId}", id);
                return StatusCode(500, "Internal server error while retrieving token details");
            }
        }

        // POST: api/rfid-tokens
        [HttpPost]
        public async Task<ActionResult<object>> CreateToken([FromBody] CreateTokenRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return NotFound($"User with ID {request.UserId} not found");
                }

                if (await _context.RfidTokens.AnyAsync(rt => rt.RfidCode == request.RfidCode))
                {
                    return BadRequest("RFID code already exists");
                }

                var token = new RfidToken
                {
                    UserId = request.UserId,
                    RfidCode = request.RfidCode,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.RfidTokens.Add(token);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetToken), new { id = token.Id }, 
                    await GetToken(token.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating RFID token");
                return StatusCode(500, "Internal server error while creating token");
            }
        }

        // PUT: api/rfid-tokens/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateToken(int id, [FromBody] UpdateTokenRequest request)
        {
            try
            {
                var token = await _context.RfidTokens.FindAsync(id);
                if (token == null)
                {
                    return NotFound($"RFID token with ID {id} not found");
                }

                if (request.RfidCode != token.RfidCode &&
                    await _context.RfidTokens.AnyAsync(rt => rt.RfidCode == request.RfidCode))
                {
                    return BadRequest("RFID code already exists");
                }

                token.RfidCode = request.RfidCode;
                token.IsActive = request.IsActive;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating RFID token {TokenId}", id);
                return StatusCode(500, "Internal server error while updating token");
            }
        }

        // POST: api/rfid-tokens/verify
        [HttpPost("verify")]
        public async Task<ActionResult<object>> VerifyAccess([FromBody] VerifyAccessRequest request)
        {
            try
            {
                var token = await _context.RfidTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.RfidCode == request.RfidCode);

                bool isGranted = false;
                string? deniedReason = null;

                if (token == null)
                {
                    deniedReason = "Invalid RFID token";
                }
                else if (!token.IsActive)
                {
                    deniedReason = "RFID token is inactive";
                }
                else if (!token.User.IsActive)
                {
                    deniedReason = "User is inactive";
                }
                else
                {
                    isGranted = true;
                }

                // Log the access attempt
                var accessLog = new AccessLog
                {
                    RfidTokenId = token?.Id ?? 0,
                    AccessTime = DateTime.UtcNow,
                    AccessStatus = isGranted ? AccessStatus.AUTHORIZED : AccessStatus.UNAUTHORIZED,
                    IsGranted = isGranted,
                    DeniedReason = deniedReason
                };

                _context.AccessLogs.Add(accessLog);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    IsGranted = isGranted,
                    DeniedReason = deniedReason,
                    AccessTime = accessLog.AccessTime,
                    User = token?.User == null ? null : new
                    {
                        token.User.Id,
                        token.User.Name
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying RFID access");
                return StatusCode(500, "Internal server error while verifying access");
            }
        }
    }

    public class CreateTokenRequest
    {
        public int UserId { get; set; }
        public string RfidCode { get; set; } = string.Empty;
    }

    public class UpdateTokenRequest
    {
        public string RfidCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class VerifyAccessRequest
    {
        public string RfidCode { get; set; } = string.Empty;
    }
}