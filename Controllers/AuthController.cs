using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApplicationDbContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    return BadRequest(new { message = "Пользователь с таким email уже существует" });
                }

                var user = new User
                {
                    Email = model.Email,
                    Name = model.Name,
                    Role = Role.User,
                    Status = "approved",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    CreatedAt = DateTime.UtcNow,
                    ContactInfo = null,
                    Description = null,
                    Preferences = null,
                    FullName = null,
                    Experience = null,
                    Residence = null,
                    Cities = null,
                    Ideas = null,
                    PhotosDescription = null,
                    OtherInfo = null,
                    OrganizationId = null // Явно указываем
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Регистрация успешна" });
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
                var user = await _context.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Email,
                        u.PasswordHash,
                        u.Role,
                        u.Status,
                        u.FullName,
                        u.Experience,
                        u.Residence,
                        u.Cities,
                        u.Ideas,
                        u.PhotosDescription,
                        u.OtherInfo
                    })
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    return BadRequest(new { message = "Неверный email или пароль" });
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes("MySuperSecretKey1234567890!@#$%^&*()"); // Должно совпадать с appsettings.json
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                new Claim(ClaimTypes.Name, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString().ToLower())
            }),
                    Expires = DateTime.UtcNow.AddHours(1),
                    Issuer = "OnlineTourGuide",
                    Audience = "OnlineTourGuideUsers",
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature
                    )
                };
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

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
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id.ToString() == User.Identity.Name);

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