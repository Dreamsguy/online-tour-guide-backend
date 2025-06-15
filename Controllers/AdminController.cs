using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

[Route("api/[controller]")]
[ApiController]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminController(ApplicationDbContext context, ILogger<AdminController> logger, IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("excursions")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetExcursions()
    {
        _logger.LogInformation("GET /api/admin/excursions, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var excursions = await _context.Excursions
                .Select(e => new
                {
                    e.Id,
                    Title = e.Title ?? "Без названия",
                    City = e.City ?? "Не указан",
                    TicketsJson = e.TicketsJson,
                    ImagesJson = e.ImagesJson,
                    RouteJson = e.RouteJson
                })
                .ToListAsync();

            var result = excursions.Select(e => new
            {
                e.Id,
                e.Title,
                e.City,
                Tickets = e.TicketsJson != null ? e.TicketsJson : "[]",
                Images = e.ImagesJson != null ? e.ImagesJson : "[]",
                Route = e.RouteJson != null && !string.IsNullOrEmpty(e.RouteJson)
                    ? JsonSerializer.Serialize(
                        JsonSerializer.Deserialize<List<Dictionary<string, double>>>(e.RouteJson)
                            ?.Select(coord => new double[] { coord["lat"], coord["lng"] }) ?? new List<double[]>()
                    )
                    : "[]"
            }).Select(e => new
            {
                e.Id,
                e.Title,
                e.City,
                Tickets = JsonSerializer.Deserialize<List<Ticket>>(e.Tickets) ?? new List<Ticket>(),
                Images = JsonSerializer.Deserialize<List<string>>(e.Images) ?? new List<string>(),
                Route = JsonSerializer.Deserialize<List<double[]>>(e.Route) ?? new List<double[]>()
            }).ToList();

            _logger.LogInformation("Загружено {Count} экскурсий", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке экскурсий");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
        }
    }

    [HttpGet("organizations")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetOrganizations()
    {
        _logger.LogInformation("GET /api/admin/organizations, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var organizations = await _context.Organizations
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.INN,
                    o.City,
                    o.PostalCode,
                    o.Rating,
                    o.Phone,
                    o.Email,
                    o.WorkingHours,
                    o.Description,
                    Image = o.Image ?? "/images/default_organization.jpg"
                })
                .ToListAsync();

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var result = new List<object>();

                foreach (var o in organizations)
                {
                    var excursions = await dbContext.Excursions
                        .Where(e => e.OrganizationId == o.Id)
                        .Select(e => new { e.Id, e.Title })
                        .ToListAsync();

                    var guides = await dbContext.Users
                        .Where(u => u.Role == Role.Guide && u.OrganizationId == o.Id)
                        .Select(u => new { u.Id, u.Name })
                        .ToListAsync();

                    var managers = await dbContext.Users
                        .Where(u => u.Role == Role.Manager && u.OrganizationId == o.Id)
                        .Select(u => new { u.Id, u.Name })
                        .ToListAsync();

                    result.Add(new
                    {
                        o.Id,
                        o.Name,
                        o.INN,
                        o.City,
                        o.PostalCode,
                        o.Rating,
                        o.Phone,
                        o.Email,
                        o.WorkingHours,
                        o.Description,
                        o.Image,
                        Excursions = excursions,
                        Guides = guides,
                        Managers = managers
                    });
                }

                return Ok(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке организаций");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpGet("organizations/{id}/excursions")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetOrganizationExcursions(int id)
    {
        _logger.LogInformation("GET /api/admin/organizations/{id}/excursions, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var excursions = await _context.Excursions
                .Where(e => e.OrganizationId == id)
                .Select(e => new { e.Id, e.Title })
                .ToListAsync();

            return Ok(excursions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке экскурсий для организации {Id}", id);
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpGet("organizations/{id}/guides")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetOrganizationGuides(int id)
    {
        _logger.LogInformation("GET /api/admin/organizations/{id}/guides, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var guides = await _context.Users
                .Where(u => u.Role == Role.Guide && u.OrganizationId == id)
                .Select(u => new { u.Id, u.Name })
                .ToListAsync();

            return Ok(guides);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке гидов для организации {Id}", id);
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpGet("organizations/{id}/managers")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetOrganizationManagers(int id)
    {
        _logger.LogInformation("GET /api/admin/organizations/{id}/managers, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var managers = await _context.Users
                .Where(u => u.Role == Role.Manager && u.OrganizationId == id)
                .Select(u => new { u.Id, u.Name })
                .ToListAsync();

            return Ok(managers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке менеджеров для организации {Id}", id);
            return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
        }
    }

    [HttpPut("excursions/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateExcursion(int id, [FromBody] Excursion excursion)
    {
        _logger.LogInformation("PUT /api/admin/excursions/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var existing = await _context.Excursions.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Экскурсия не найдена" });

            existing.Title = excursion.Title ?? existing.Title;
            existing.City = excursion.City ?? existing.City;
            existing.Description = excursion.Description ?? existing.Description;
            existing.IsIndividual = excursion.IsIndividual;
            existing.GuideId = excursion.GuideId;
            existing.ManagerId = excursion.ManagerId;
            existing.OrganizationId = excursion.OrganizationId;
            existing.RejectionReason = excursion.RejectionReason ?? existing.RejectionReason;
            existing.Tickets = excursion.Tickets;
            existing.Images = excursion.Images;
            existing.Route = excursion.Route;

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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteExcursion(int id)
    {
        _logger.LogInformation("DELETE /api/admin/excursions/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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
                _context.Bookings.RemoveRange(relatedBookings);
                await _context.SaveChangesAsync();
            }

            var relatedReviews = await _context.Reviews
                .Where(r => r.ExcursionId == id)
                .ToListAsync();
            if (relatedReviews.Any())
            {
                _context.Reviews.RemoveRange(relatedReviews);
                await _context.SaveChangesAsync();
            }

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
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetAttractions()
    {
        _logger.LogInformation("GET /api/admin/attractions, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var attractions = await _context.Attractions
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.City,
                    Rating = a.Rating.HasValue ? a.Rating.Value.ToString() : "Нет рейтинга",
                    Coordinates = a.Coordinates != null ? new { lat = a.Coordinates.Y, lng = a.Coordinates.X } : null
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateAttraction([FromBody] Attraction attraction)
    {
        _logger.LogInformation("POST /api/admin/attractions, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            attraction.CreatedAt = DateTime.UtcNow;
            attraction.Rating = null;
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateAttraction(int id, [FromBody] Attraction attraction)
    {
        _logger.LogInformation("PUT /api/admin/attractions/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteAttraction(int id)
    {
        _logger.LogInformation("DELETE /api/admin/attractions/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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

    [HttpPost("organizations")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateOrganization([FromForm] OrganizationCreateDto organizationDto)
    {
        _logger.LogInformation("POST /api/admin/organizations, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            if (string.IsNullOrEmpty(organizationDto.Name) || string.IsNullOrEmpty(organizationDto.INN))
                return BadRequest(new { message = "Название и ИНН обязательны для заполнения" });

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
                Rating = null
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

    [HttpPut("organizations/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateOrganization(int id, [FromBody] Organization organization)
    {
        _logger.LogInformation("PUT /api/admin/organizations/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteOrganization(int id)
    {
        _logger.LogInformation("DELETE /api/admin/organizations/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
        try
        {
            var organization = await _context.Organizations.FindAsync(id);
            if (organization == null)
                return NotFound(new { message = "Организация не найдена" });

            var relatedReviews = await _context.Reviews
                .Where(r => r.OrganizationId == id)
                .ToListAsync();
            if (relatedReviews.Any())
            {
                _context.Reviews.RemoveRange(relatedReviews);
                await _context.SaveChangesAsync();
            }

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

    [HttpGet("users")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUsers()
    {
        _logger.LogInformation("GET /api/admin/users, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateUser([FromBody] RegisterModel model)
    {
        _logger.LogInformation("POST /api/admin/users, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] User user)
    {
        _logger.LogInformation("PUT /api/admin/users/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        _logger.LogInformation("DELETE /api/admin/users/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            id, User.FindFirst("sub")?.Value, User.FindFirst("role")?.Value, User.Claims);
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

    public class OrganizationCreateDto
    {
        public string Name { get; set; }
        public string INN { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string WorkingHours { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public List<string> Directions { get; set; }
        public string Description { get; set; }
        public IFormFile Image { get; set; }
    }
}