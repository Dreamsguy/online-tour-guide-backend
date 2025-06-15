using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExcursionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExcursionsController> _logger;

        public ExcursionsController(ApplicationDbContext context, ILogger<ExcursionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetExcursions(string city = null, string category = null, string search = null)
        {
            _logger.LogInformation("GET /api/excursions, User: {UserId}, Role: {Role}, Claims: {@Claims}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                User.Claims);

            try
            {
                var query = _context.Excursions.AsQueryable();
                if (!string.IsNullOrEmpty(city))
                {
                    query = query.Where(e => e.City == city);
                }
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(e => e.Direction == category); // Используем Direction вместо Category
                }
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(e => e.Title.Contains(search) || e.City.Contains(search)); // Фильтрация по названию и городу
                }

                var excursions = await query
                    .Include(e => e.Attractions)
                    .Include(e => e.Reviews)
                    .ToListAsync();

                foreach (var excursion in excursions)
                {
                    excursion.Title = excursion.Title ?? "Без названия";
                    excursion.Description = excursion.Description ?? "Без описания";
                    excursion.City = excursion.City ?? "Не указан";
                    excursion.Attractions ??= new List<Attraction>();
                    excursion.Reviews ??= new List<Review>();
                    excursion.RejectionReason = excursion.RejectionReason ?? "";
                    if (!excursion.Rating.HasValue) excursion.Rating = 0;
                }

                var result = excursions.Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.Description,
                    e.City,
                    Images = e.Images,
                    e.Rating,
                    e.RejectionReason,
                    e.Attractions,
                    e.Reviews,
                    AvailableTicketsByDate = e.AvailableTicketsByDate.Any() ? e.AvailableTicketsByDate : new Dictionary<string, Dictionary<string, TicketAvailability>>()
                }).ToList();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке экскурсий");
                return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
            }
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetExcursion(int id)
        {
            var userId = User?.Identity?.IsAuthenticated == true ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value : "Anonymous";
            var role = User?.Identity?.IsAuthenticated == true ? User.FindFirst(ClaimTypes.Role)?.Value : "N/A";
            _logger.LogInformation("Fetching excursion with ID: {id}, User: {userId}, Role: {role}", id, userId, role);

            var excursion = await _context.Excursions
                .Include(e => e.Attractions)
                .Include(e => e.Reviews)
                .Include(e => e.Guide)
                .Include(e => e.Manager)
                .Include(e => e.Organization)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (excursion == null)
            {
                _logger.LogWarning("Excursion with ID {id} not found, User: {userId}", id, userId);
                return NotFound($"Excursion with ID {id} not found.");
            }

            var result = new
            {
                excursion.Id,
                Title = excursion.Title ?? "Без названия",
                Description = excursion.Description ?? "Без описания",
                City = excursion.City ?? "Не указан",
                Category = excursion.Direction ?? "Не указана",
                Rating = excursion.Reviews.Any() ? (decimal?)excursion.Reviews.Average(r => r.Rating) : 0m, // Безопасная проверка
                Images = excursion.Images,
                Attractions = excursion.Attractions.Select(a => new { a.Id, a.Name, a.Coordinates }),
                AvailableTicketsByDate = excursion.AvailableTicketsByDate.Any() ? excursion.AvailableTicketsByDate : new Dictionary<string, Dictionary<string, TicketAvailability>>(),
            };
            return Ok(result);
        }

        [HttpGet("top")]
        public async Task<ActionResult<IEnumerable<object>>> GetTopExcursions()
        {
            _logger.LogInformation("GET /api/excursions/top");
            try
            {
                var topExcursions = await _context.Excursions
                    .Where(e => e.Rating.HasValue && !string.IsNullOrEmpty(e.Title))
                    .OrderByDescending(e => e.Rating)
                    .Take(5)
                    .Select(e => new
                    {
                        e.Id,
                        e.Title,
                        e.Description,
                        e.City,
                        Images = e.Images,
                        e.Rating,
                        e.RejectionReason,
                        Attractions = e.Attractions,
                        Reviews = e.Reviews,
                        AvailableTicketsByDate = e.AvailableTicketsByDate
                    })
                    .ToListAsync();

                return Ok(topExcursions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке топ экскурсий");
                return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
            }
        }

        [HttpPost("{id}/images")]
        [Authorize]
        public async Task<IActionResult> UploadImages(int id, [FromBody] List<string> imageUrls)
        {
            _logger.LogInformation("POST /api/excursions/{id}/images, User: {UserId}, Role: {Role}, Claims: {@Claims}",
                id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value, User.FindFirst(ClaimTypes.Role)?.Value, User.Claims);
            try
            {
                var excursion = await _context.Excursions.FirstOrDefaultAsync(e => e.Id == id);
                if (excursion == null) return NotFound(new { message = "Экскурсия не найдена" });

                if (imageUrls == null || !imageUrls.Any())
                {
                    return BadRequest(new { message = "Список изображений не может быть пустым" });
                }

                var currentImages = excursion.Images ?? new List<string>();
                currentImages.AddRange(imageUrls.Where(url => !string.IsNullOrEmpty(url)));
                excursion.Images = currentImages.Distinct().ToList();

                await _context.SaveChangesAsync();
                return Ok(new { message = "Изображения успешно добавлены", Images = excursion.Images });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке изображений для экскурсии {Id}", id);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("manager/{managerId}")]
        public async Task<IActionResult> GetManagerExcursionsAndOrganizations(int managerId)
        {
            _logger.LogInformation("GET /api/excursions/manager/{managerId}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
                managerId, User.FindFirst(ClaimTypes.NameIdentifier)?.Value, User.FindFirst(ClaimTypes.Role)?.Value, User.Claims);
            var user = await _context.Users
                .Include(u => u.ManagedExcursions)
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Id == managerId);

            if (user == null || user.Role != Role.Manager)
            {
                return NotFound(new { message = "Менеджер не найден" });
            }

            var managedExcursions = user.ManagedExcursions.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                status = e.RejectionReason != null ? "Отклонена" : "Одобрена",
                organizationId = e.OrganizationId
            }).ToList();

            var organization = user.Organization != null ? new
            {
                id = user.Organization.Id,
                name = user.Organization.Name
            } : null;

            return Ok(new
            {
                excursions = managedExcursions,
                organization = organization
            });
        }

        [HttpGet("recommendations/{userId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetRecommendations(int userId)
        {
            try
            {
                var userActions = await _context.UserActions
                    .Where(ua => ua.UserId == userId)
                    .ToListAsync();

                var viewedExcursionIds = userActions
                    .Where(ua => ua.ActionType == "view")
                    .Select(ua => ua.ExcursionId)
                    .ToList();

                var bookedExcursionIds = userActions
                    .Where(ua => ua.ActionType == "book")
                    .Select(ua => ua.ExcursionId)
                    .ToList();

                var recommendedExcursions = await _context.Excursions
                    .Where(e => e.City == _context.Excursions
                        .Where(ee => viewedExcursionIds.Contains(ee.Id) || bookedExcursionIds.Contains(ee.Id))
                        .Select(ee => ee.City)
                        .FirstOrDefault() && !viewedExcursionIds.Contains(e.Id) && !bookedExcursionIds.Contains(e.Id))
                    .OrderByDescending(e => e.Rating)
                    .Take(5)
                    .Select(e => new { e.Id, e.Title, e.City, e.Rating, e.Images })
                    .ToListAsync();

                return Ok(recommendedExcursions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке рекомендаций");
                return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
            }
        }

        [HttpPost]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> CreateExcursion()
        {
            _logger.LogInformation("POST /api/excursions, User Role: {Role}, Claims: {@Claims}",
                User.FindFirst(ClaimTypes.Role)?.Value ?? "Not found", User.Claims);

            try
            {
                var form = await Request.ReadFormAsync();
                var excursionData = form["excursion"];
                _logger.LogInformation("Received excursion data: {ExcursionData}", excursionData);
                if (string.IsNullOrEmpty(excursionData))
                    return BadRequest(new { message = "Поле excursion отсутствует или пустое" });

                var excursionModel = JsonSerializer.Deserialize<ExcursionModel>(excursionData);
                if (excursionModel == null)
                    return BadRequest(new { message = "Некорректный формат данных excursion" });

                var excursion = new Excursion
                {
                    Title = excursionModel.Title,
                    Direction = excursionModel.Direction,
                    Description = excursionModel.Description,
                    City = excursionModel.City,
                    IsIndividual = excursionModel.IsIndividual,
                    IsForDisabled = excursionModel.IsForDisabled,
                    IsForChildren = excursionModel.IsForChildren,
                    CreatedAt = DateTime.UtcNow
                };

                if (User.IsInRole("manager"))
                {
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("name") ?? User.FindFirst("sub");
                    var userId = userIdClaim != null ? int.TryParse(userIdClaim.Value, out int id) ? id : -1 : -1;
                    var user = userId != -1 ? await _context.Users.FirstOrDefaultAsync(u => u.Id == userId) : null;
                    if (user == null || user.OrganizationId == null)
                    {
                        if (!excursionModel.OrganizationId.HasValue || excursionModel.OrganizationId <= 0)
                            return Unauthorized(new { message = "Менеджер должен быть привязан к организации или указать OrganizationId" });
                        excursion.OrganizationId = excursionModel.OrganizationId.Value;
                        if (!await _context.Organizations.AnyAsync(o => o.Id == excursion.OrganizationId))
                            return BadRequest(new { message = $"Организация с Id {excursion.OrganizationId} не найдена" });
                    }
                    else
                    {
                        excursion.OrganizationId = user.OrganizationId.Value;
                        if (excursionModel.OrganizationId.HasValue && excursionModel.OrganizationId != user.OrganizationId)
                            return BadRequest(new { message = "Менеджер может создавать экскурсии только для своей организации" });
                    }
                }
                else if (User.IsInRole("admin"))
                {
                    if (excursionModel.OrganizationId.HasValue && excursionModel.OrganizationId > 0)
                    {
                        excursion.OrganizationId = excursionModel.OrganizationId.Value;
                        if (!await _context.Organizations.AnyAsync(o => o.Id == excursion.OrganizationId))
                            return BadRequest(new { message = $"Организация с Id {excursion.OrganizationId} не найдена" });
                    }
                    else
                    {
                        return BadRequest(new { message = "OrganizationId обязателен и должен быть положительным числом" });
                    }
                }
                else
                {
                    return Unauthorized(new { message = "Только админ или менеджер могут создавать экскурсии" });
                }

                if (excursionModel.GuideId.HasValue && excursionModel.GuideId > 0)
                {
                    excursion.GuideId = excursionModel.GuideId.Value;
                    if (!await _context.Users.AnyAsync(u => u.Id == excursion.GuideId && u.Role == Role.Guide && u.OrganizationId == excursion.OrganizationId))
                        return BadRequest(new { message = $"Гид с Id {excursion.GuideId} не найден или не принадлежит организации" });
                }

                if (excursionModel.ManagerId.HasValue && excursionModel.ManagerId > 0)
                {
                    excursion.ManagerId = excursionModel.ManagerId.Value;
                    if (!await _context.Users.AnyAsync(u => u.Id == excursion.ManagerId && u.Role == Role.Manager))
                        return BadRequest(new { message = $"Менеджер с Id {excursion.ManagerId} не найден или не является менеджером" });
                }

                if (excursionModel.Tickets != null && excursionModel.Tickets.Any())
                {
                    _logger.LogInformation("Received tickets: {@Tickets}", excursionModel.Tickets);
                    excursion.Tickets = excursionModel.Tickets
                        .Where(t => !string.IsNullOrEmpty(t.Date) || !string.IsNullOrEmpty(t.Time) || t.Total > 0)
                        .Select(t => new Ticket
                        {
                            Date = t.Date,
                            Time = t.Time,
                            Total = t.Total,
                            Type = t.Type,
                            Price = t.Price,
                            Currency = t.Currency
                        }).ToList();
                    if (!excursion.Tickets.Any())
                        return BadRequest(new { message = "Необходимо указать хотя бы один валидный билет" });
                }
                else
                {
                    return BadRequest(new { message = "Необходимо указать хотя бы один билет" });
                }

                var file = form.Files.GetFile("image");
                if (file != null && file.Length > 0 && file.Length <= 10 * 1024 * 1024)
                {
                    var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                    Directory.CreateDirectory(imagesDir);
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(imagesDir, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    excursion.ImagesJson = JsonSerializer.Serialize(new List<string> { "/images/" + fileName });
                }
                else
                {
                    excursion.ImagesJson = JsonSerializer.Serialize(new List<string> { "/images/default_image.jpg" });
                }

                _context.Excursions.Add(excursion);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Экскурсия создана", id = excursion.Id });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Ошибка десериализации JSON для создания экскурсии");
                return BadRequest(new { message = "Некорректный формат JSON: " + ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Неавторизованный доступ при создании экскурсии");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании экскурсии");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("attractions")]
        public async Task<IActionResult> GetAttractions()
        {
            _logger.LogInformation("GET /api/excursions/attractions, User: {UserId}, Role: {Role}, Claims: {@Claims}",
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindFirst(ClaimTypes.Role)?.Value,
                User.Claims);
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

        [HttpGet("cities")]
        public async Task<ActionResult<IEnumerable<object>>> GetCities()
        {
            _logger.LogInformation("GET /api/excursions/cities");
            try
            {
                var cities = await _context.Excursions
                    .GroupBy(e => e.City)
                    .Select(g => new { name = g.Key, count = g.Count() })
                    .Where(c => !string.IsNullOrEmpty(c.name))
                    .ToListAsync();
                return Ok(cities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке городов");
                return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
            }
        }

        [HttpGet("Directions")]
        public async Task<IActionResult> GetDirections()
        {
            _logger.LogInformation("Fetching directions...");
            var directions = await _context.Excursions
                .Select(e => new { Name = e.Direction ?? "Не указано", Count = _context.Excursions.Count(x => x.Direction == e.Direction) })
                .Distinct()
                .Where(d => !string.IsNullOrEmpty(d.Name))
                .ToListAsync();

            if (!directions.Any())
            {
                _logger.LogWarning("No directions found in the database.");
                return Ok(new List<object>()); // Возвращаем пустой массив, если ничего нет
            }

            return Ok(directions);
        }

        [HttpGet("preferences/{userId}")]
        public async Task<IActionResult> GetPreferences(int userId)
        {
            try
            {
                var preferences = await _context.UserPreferences
                    .FirstOrDefaultAsync(up => up.UserId == userId);
                if (preferences == null)
                {
                    return NotFound(new { message = "Предпочтения не найдены" });
                }
                var response = new
                {
                    Id = preferences.Id,
                    UserId = preferences.UserId,
                    PreferredCity = preferences.PreferredCity ?? "",
                    PreferredDirection = preferences.PreferredDirection ?? "",
                    PreferredAttractions = preferences.PreferredAttractions ?? new List<int>(),
                    UpdatedAt = preferences.UpdatedAt,
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке предпочтений для пользователя {UserId}", userId);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpPost("preferences/{userId}")]
        public async Task<IActionResult> SavePreferences(int userId, [FromBody] UserPreference prefs)
        {
            try
            {
                var existingPrefs = await _context.UserPreferences.FirstOrDefaultAsync(up => up.UserId == userId);
                if (existingPrefs == null)
                {
                    prefs.Id = 0; // Автоинкремент
                    prefs.UserId = userId;
                    _context.UserPreferences.Add(prefs);
                }
                else
                {
                    existingPrefs.PreferredCity = prefs.PreferredCity;
                    existingPrefs.PreferredDirection = prefs.PreferredDirection;
                    existingPrefs.PreferredAttractions = prefs.PreferredAttractions;
                    existingPrefs.UpdatedAt = prefs.UpdatedAt;
                    _context.UserPreferences.Update(existingPrefs);
                }
                await _context.SaveChangesAsync();
                return Ok(prefs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении предпочтений для пользователя {UserId}", userId);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }
    }


    public class ExcursionModel
    {
        public string Title { get; set; }
        public string Direction { get; set; }
        public string Description { get; set; }
        public string City { get; set; }
        public int? GuideId { get; set; }
        public int? ManagerId { get; set; }
        public int? OrganizationId { get; set; }
        public bool IsIndividual { get; set; }
        public bool IsForDisabled { get; set; }
        public bool IsForChildren { get; set; }
        public List<TicketModel> Tickets { get; set; }
        public int? Id { get; set; } // Для редактирования
    }

    public class TicketModel
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public int Total { get; set; }
        public string Type { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
    }
}