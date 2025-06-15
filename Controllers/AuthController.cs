using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;
        private readonly JwtSettings _jwtSettings;

        public AuthController(
            ApplicationDbContext context,
            ILogger<AuthController> logger,
            IOptions<JwtSettings> jwtSettings)
        {
            _context = context;
            _logger = logger;
            _jwtSettings = jwtSettings.Value ?? throw new ArgumentNullException(nameof(jwtSettings), "JWT settings are not configured.");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    return BadRequest(new { message = "Пользователь с таким email уже существует" });
                }

                var user = new User
                {
                    Email = model.Email,
                    Name = model.Email, // Используем email как UserName
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Status = "approved",
                    CreatedAt = DateTime.UtcNow,
                    OrganizationId = model.OrganizationId // Делаем необязательным, может быть null
                };

                if (!string.IsNullOrEmpty(model.Role))
                {
                    if (Enum.TryParse<Role>(model.Role, true, out var role))
                    {
                        user.Role = role;
                    }
                    else
                    {
                        user.Role = Role.User;
                    }
                }
                else
                {
                    user.Role = Role.User;
                }

                _context.Users.Add(user);
                await _context.SaveChangesAsync(); // Строка 79

                return Ok(new { message = "Пользователь зарегистрирован", id = user.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при регистрации пользователя");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", model.Email);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid login attempt for email: {Email}", model.Email);
                    return BadRequest(new { message = "Неверный email или пароль" });
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtSettings.Key ?? throw new ArgumentNullException(nameof(_jwtSettings.Key), "JWT Key is not configured."));
                var now = DateTime.UtcNow;
                var expires = now.AddHours(1); // Устанавливаем срок действия на 1 час вперёд
                _logger.LogInformation("Generating token with nbf: {Now}, exp: {Expires}", now, expires);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                new Claim(ClaimTypes.Name, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString().ToLower())
            }),
                    NotBefore = now,
                    Expires = expires,
                    Issuer = _jwtSettings.Issuer,
                    Audience = _jwtSettings.Audience,
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature
                    )
                };
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);
                _logger.LogInformation("Token generated: {Token}", tokenString);

                return Ok(new
                {
                    token = tokenString,
                    user = new
                    {
                        id = user.Id,
                        name = user.Name,
                        email = user.Email,
                        role = user.Role.ToString().ToLower(),
                        status = user.Status,
                        fullName = user.FullName,
                        experience = user.Experience,
                        residence = user.Residence,
                        cities = user.Cities,
                        ideas = user.Ideas,
                        photosDescription = user.PhotosDescription,
                        otherInfo = user.OtherInfo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе пользователя");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("users")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users = await _context.Users
                    .Select(u => new
                    {
                        id = u.Id,
                        name = u.Name,
                        email = u.Email,
                        role = u.Role.ToString().ToLower(),
                        status = u.Status,
                        fullName = u.FullName,
                        experience = u.Experience,
                        residence = u.Residence,
                        cities = u.Cities,
                        ideas = u.Ideas,
                        photosDescription = u.PhotosDescription,
                        otherInfo = u.OtherInfo
                    })
                    .ToListAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке списка пользователей");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value; // Извлекаем Id из токена
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int id))
                {
                    return Unauthorized(new { message = "Недействительный токен" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound(new { message = "Пользователь не найден" });
                }

                return Ok(new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    role = user.Role.ToString().ToLower(),
                    status = user.Status,
                    fullName = user.FullName,
                    experience = user.Experience,
                    residence = user.Residence,
                    cities = user.Cities,
                    ideas = user.Ideas,
                    photosDescription = user.PhotosDescription,
                    otherInfo = user.OtherInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке профиля пользователя");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }
    }

    public class RoleApprovalModel
    {
        public string Role { get; set; }
    }

    public class LoginModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RoleRequestModel
    {
        public string FullName { get; set; }
        public string Experience { get; set; }
        public string Residence { get; set; }
        public string Cities { get; set; }
        public string Ideas { get; set; }
        public string PhotosDescription { get; set; }
        public string OtherInfo { get; set; }
    }
}