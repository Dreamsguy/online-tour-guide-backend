using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Экскурсии
    [HttpGet("excursions")]
    public async Task<IActionResult> GetExcursions()
    {
        try
        {
            var excursions = await _context.Excursions
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.City,
                    e.Price
                })
                .ToListAsync();
            return Ok(excursions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке экскурсий");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPost("excursions")]
    public async Task<IActionResult> CreateExcursion([FromBody] Excursion excursion)
    {
        try
        {
            excursion.CreatedAt = DateTime.UtcNow;
            excursion.Status = "approved";
            _context.Excursions.Add(excursion);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Экскурсия создана", id = excursion.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании экскурсии");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPut("excursions/{id}")]
    public async Task<IActionResult> UpdateExcursion(int id, [FromBody] Excursion excursion)
    {
        try
        {
            var existing = await _context.Excursions.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Экскурсия не найдена" });

            existing.Title = excursion.Title ?? existing.Title;
            existing.City = excursion.City ?? existing.City;
            existing.Price = excursion.Price != 0 ? excursion.Price : existing.Price;
            existing.Description = excursion.Description ?? existing.Description;
            existing.Image = excursion.Image ?? existing.Image;
            existing.IsIndividual = excursion.IsIndividual;
            existing.GuideId = excursion.GuideId ?? existing.GuideId;
            existing.ManagerId = excursion.ManagerId ?? existing.ManagerId;
            existing.OrganizationId = excursion.OrganizationId ?? existing.OrganizationId;
            existing.Status = excursion.Status ?? existing.Status;
            existing.RejectionReason = excursion.RejectionReason ?? existing.RejectionReason;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Экскурсия обновлена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении экскурсии");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpDelete("excursions/{id}")]
    public async Task<IActionResult> DeleteExcursion(int id)
    {
        try
        {
            var excursion = await _context.Excursions.FindAsync(id);
            if (excursion == null)
                return NotFound(new { message = "Экскурсия не найдена" });

            var relatedBookings = await _context.Bookings
                .Where(b => b.ExcursionId == id)
                .ToListAsync();
            if (relatedBookings.Any())
            {
                _logger.LogInformation($"Удаляем {relatedBookings.Count} записей из bookings для экскурсии {id}");
                _context.Bookings.RemoveRange(relatedBookings);
                await _context.SaveChangesAsync();
            }

            var relatedAvailability = await _context.ExcursionAvailability
                .Where(ea => ea.ExcursionId == id)
                .ToListAsync();
            if (relatedAvailability.Any())
            {
                _logger.LogInformation($"Удаляем {relatedAvailability.Count} записей из excursionavailability для экскурсии {id}");
                _context.ExcursionAvailability.RemoveRange(relatedAvailability);
                await _context.SaveChangesAsync();
            }

            var relatedReviews = await _context.Reviews
                .Where(r => r.ExcursionId == id)
                .ToListAsync();
            if (relatedReviews.Any())
            {
                _logger.LogInformation($"Удаляем {relatedReviews.Count} записей из reviews для экскурсии {id}");
                _context.Reviews.RemoveRange(relatedReviews);
                await _context.SaveChangesAsync();
            }

            // Удаляем саму экскурсию
            _context.Excursions.Remove(excursion);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Экскурсия удалена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении экскурсии");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpGet("attractions")]
    public async Task<IActionResult> GetAttractions()
    {
        try
        {
            var attractions = await _context.Attractions
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.City,
                    Rating = a.Rating.HasValue ? a.Rating.Value.ToString() : "Нет рейтинга",
                    Coordinates = a.Coordinates
                })
                .ToListAsync();
            return Ok(attractions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке достопримечательностей");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPost("attractions")]
    public async Task<IActionResult> CreateAttraction([FromBody] Attraction attraction)
    {
        try
        {
            attraction.CreatedAt = DateTime.UtcNow;
            attraction.Rating = null; // Устанавливаем NULL
            _context.Attractions.Add(attraction);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Достопримечательность создана", id = attraction.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании достопримечательности");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPut("attractions/{id}")]
    public async Task<IActionResult> UpdateAttraction(int id, [FromBody] Attraction attraction)
    {
        try
        {
            var existing = await _context.Attractions.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Достопримечательность не найдена" });

            existing.Name = attraction.Name ?? existing.Name;
            existing.City = attraction.City ?? existing.City;
            existing.Rating = attraction.Rating != 0 ? attraction.Rating : existing.Rating;
            existing.Description = attraction.Description ?? existing.Description;
            existing.History = attraction.History ?? existing.History;
            existing.Image = attraction.Image ?? existing.Image;
            existing.VisitingHours = attraction.VisitingHours ?? existing.VisitingHours;
            existing.Coordinates = attraction.Coordinates ?? existing.Coordinates;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Достопримечательность обновлена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении достопримечательности");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpDelete("attractions/{id}")]
    public async Task<IActionResult> DeleteAttraction(int id)
    {
        try
        {
            var attraction = await _context.Attractions.FindAsync(id);
            if (attraction == null)
                return NotFound(new { message = "Достопримечательность не найдена" });

            _context.Attractions.Remove(attraction);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Достопримечательность удалена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении достопримечательности");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    // Организации
    [HttpGet("organizations")]
    public async Task<IActionResult> GetOrganizations()
    {
        try
        {
            var organizations = await _context.Organizations
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.Rating
                })
                .ToListAsync();
            return Ok(organizations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке организаций");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPost("organizations")]
    public async Task<IActionResult> CreateOrganization([FromForm] OrganizationCreateDto organizationDto)
    {
        try
        {
            // Проверяем обязательные поля
            if (string.IsNullOrEmpty(organizationDto.Name) || string.IsNullOrEmpty(organizationDto.INN))
                return BadRequest(new { message = "Название и ИНН обязательны для заполнения" });

            // Обрабатываем загрузку изображения
            string imagePath = null;
            if (organizationDto.Image != null)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(organizationDto.Image.FileName);
                var filePath = Path.Combine("wwwroot/images", fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await organizationDto.Image.CopyToAsync(stream);
                }
                imagePath = $"/images/{fileName}";
            }

            var organization = new Organization
            {
                Name = organizationDto.Name,
                INN = organizationDto.INN,
                Email = organizationDto.Email,
                Phone = organizationDto.Phone,
                WorkingHours = organizationDto.WorkingHours,
                PostalCode = organizationDto.PostalCode,
                City = organizationDto.City,
                Directions = organizationDto.Directions != null ? System.Text.Json.JsonSerializer.Serialize(organizationDto.Directions) : null,
                Description = organizationDto.Description,
                Image = imagePath,
                Rating = null // Рейтинг не устанавливаем при создании
            };

            _context.Organizations.Add(organization);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Организация создана", id = organization.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании организации");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    // DTO для создания организации
    public class OrganizationCreateDto
    {
        public string Name { get; set; }
        public string INN { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string WorkingHours { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public List<string> Directions { get; set; } // Список направлений
        public string Description { get; set; }
        public IFormFile Image { get; set; } // Для загрузки файла
    }

    [HttpPut("organizations/{id}")]
    public async Task<IActionResult> UpdateOrganization(int id, [FromBody] Organization organization)
    {
        try
        {
            var existing = await _context.Organizations.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Организация не найдена" });

            existing.Name = organization.Name ?? existing.Name;
            existing.Rating = organization.Rating != 0 ? organization.Rating : existing.Rating;
            existing.Description = organization.Description ?? existing.Description;
            existing.Image = organization.Image ?? existing.Image;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Организация обновлена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении организации");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpDelete("organizations/{id}")]
    public async Task<IActionResult> DeleteOrganization(int id)
    {
        try
        {
            var organization = await _context.Organizations.FindAsync(id);
            if (organization == null)
                return NotFound(new { message = "Организация не найдена" });

            // Удаляем все связанные отзывы
            var relatedReviews = await _context.Reviews
                .Where(r => r.OrganizationId == id)
                .ToListAsync();
            if (relatedReviews.Any())
            {
                _logger.LogInformation($"Удаляем {relatedReviews.Count} записей из reviews для организации {id}");
                _context.Reviews.RemoveRange(relatedReviews);
                await _context.SaveChangesAsync();
            }

            // Удаляем саму организацию
            _context.Organizations.Remove(organization);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Организация удалена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении организации");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    // Пользователи
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Role
                })
                .ToListAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке пользователей");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] RegisterModel model)
    {
        try
        {
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                return BadRequest(new { message = "Пользователь с таким email уже существует" });

            var user = new User
            {
                Email = model.Email,
                Name = model.Name,
                Role = Role.User,
                Status = "approved",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Пользователь создан", id = user.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании пользователя");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] User user)
    {
        try
        {
            var existing = await _context.Users.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Пользователь не найден" });

            existing.Name = user.Name ?? existing.Name;
            existing.Email = user.Email ?? existing.Email;
            existing.Role = user.Role != null ? user.Role : existing.Role;
            existing.Status = user.Status ?? existing.Status;
            existing.FullName = user.FullName ?? existing.FullName;
            existing.Experience = user.Experience ?? existing.Experience;
            existing.Residence = user.Residence ?? existing.Residence;
            existing.Cities = user.Cities ?? existing.Cities;
            existing.Ideas = user.Ideas ?? existing.Ideas;
            existing.PhotosDescription = user.PhotosDescription ?? existing.PhotosDescription;
            existing.OtherInfo = user.OtherInfo ?? existing.OtherInfo;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Пользователь обновлен" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пользователя");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "Пользователь не найден" });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Пользователь удален" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении пользователя");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }
}