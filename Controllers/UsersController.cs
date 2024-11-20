using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Data;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(AppDbContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers([FromQuery] string? search, [FromQuery] bool? active)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.RfidTokens)
                    .AsQueryable();

                if (active.HasValue)
                {
                    query = query.Where(u => u.IsActive == active.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(u => 
                        u.Name.Contains(search) || 
                        u.Email.Contains(search) || 
                        u.Phone.Contains(search));
                }

                var users = await query
                    .Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Email,
                        u.Phone,
                        u.IsActive,
                        u.CreatedAt,
                        RfidTokens = u.RfidTokens.Select(rt => new
                        {
                            rt.Id,
                            rt.RfidCode,
                            rt.IsActive,
                            rt.CreatedAt,
                            LastAccess = rt.AccessLogs
                                .OrderByDescending(al => al.AccessTime)
                                .FirstOrDefault()
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "Internal server error while retrieving users");
            }
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.RfidTokens)
                        .ThenInclude(rt => rt.AccessLogs.OrderByDescending(al => al.AccessTime).Take(5))
                    .Where(u => u.Id == id)
                    .Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Email,
                        u.Phone,
                        u.IsActive,
                        u.CreatedAt,
                        RfidTokens = u.RfidTokens.Select(rt => new
                        {
                            rt.Id,
                            rt.RfidCode,
                            rt.IsActive,
                            rt.CreatedAt,
                            RecentAccess = rt.AccessLogs.Select(al => new
                            {
                                al.AccessTime,
                                al.AccessStatus,
                                al.IsGranted,
                                al.DeniedReason
                            }).ToList()
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound($"User with ID {id} not found");
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, "Internal server error while retrieving user details");
            }
        }

        // POST: api/users
        [HttpPost]
        public async Task<ActionResult<User>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return BadRequest("Email already registered");
                }

                var user = new User
                {
                    Name = request.Name,
                    Email = request.Email,
                    Phone = request.Phone,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(request.RfidCode))
                {
                    if (await _context.RfidTokens.AnyAsync(rt => rt.RfidCode == request.RfidCode))
                    {
                        return BadRequest("RFID code already assigned");
                    }

                    var rfidToken = new RfidToken
                    {
                        UserId = user.Id,
                        RfidCode = request.RfidCode,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.RfidTokens.Add(rfidToken);
                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, 
                    await GetUser(user.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, "Internal server error while creating user");
            }
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID {id} not found");
                }

                if (request.Email != user.Email && 
                    await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return BadRequest("Email already registered to another user");
                }

                user.Name = request.Name;
                user.Email = request.Email;
                user.Phone = request.Phone;
                user.IsActive = request.IsActive;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, "Internal server error while updating user");
            }
        }

        // GET: api/users/5/access-logs
        [HttpGet("{id}/access-logs")]
        public async Task<ActionResult<object>> GetUserAccessLogs(
            int id, 
            [FromQuery] DateTime? from, 
            [FromQuery] DateTime? to,
            [FromQuery] bool? granted)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID {id} not found");
                }

                var query = _context.AccessLogs
                    .Include(al => al.RfidToken)
                    .Where(al => al.RfidToken.UserId == id);

                if (from.HasValue)
                    query = query.Where(al => al.AccessTime >= from.Value);

                if (to.HasValue)
                    query = query.Where(al => al.AccessTime <= to.Value);

                if (granted.HasValue)
                    query = query.Where(al => al.IsGranted == granted.Value);

                var accessLogs = await query
                    .OrderByDescending(al => al.AccessTime)
                    .Select(al => new
                    {
                        al.Id,
                        al.AccessTime,
                        al.IsGranted,
                        al.AccessStatus,
                        al.DeniedReason,
                        RfidToken = new
                        {
                            al.RfidToken.Id,
                            al.RfidToken.RfidCode
                        }
                    })
                    .ToListAsync();

                return Ok(new
                {
                    UserId = id,
                    UserName = user.Name,
                    TotalAccesses = accessLogs.Count,
                    GrantedAccesses = accessLogs.Count(al => al.IsGranted),
                    DeniedAccesses = accessLogs.Count(al => !al.IsGranted),
                    AccessLogs = accessLogs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving access logs for user {UserId}", id);
                return StatusCode(500, "Internal server error while retrieving access logs");
            }
        }
    }

    public class CreateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? RfidCode { get; set; }
    }

    public class UpdateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}