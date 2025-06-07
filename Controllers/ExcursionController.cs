using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System.Collections.Generic;
using System.Linq;
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
        public async Task<ActionResult<IEnumerable<Excursion>>> GetExcursions()
        {
            try
            {
                _logger.LogDebug("Начало загрузки экскурсий");
                var excursions = await _context.Excursions
                    .Include(e => e.Availability)
                    .ToListAsync();

                _logger.LogDebug($"Загружено {excursions.Count} экскурсий");

                foreach (var excursion in excursions)
                {
                    _logger.LogDebug($"Обработка экскурсии с Id: {excursion.Id}, GuideId: {excursion.GuideId}, ManagerId: {excursion.ManagerId}, OrganizationId: {excursion.OrganizationId}");
                    excursion.Title = excursion.Title ?? "Без названия";
                    excursion.Description = excursion.Description ?? "Без описания";
                    excursion.Image = excursion.Image ?? "default_image.jpg";
                    excursion.Status = excursion.Status ?? "pending";
                    excursion.City = excursion.City ?? "Не указан";
                    excursion.Attractions ??= new List<Attraction>();
                    excursion.Reviews ??= new List<Review>();
                    excursion.RejectionReason = excursion.RejectionReason ?? "";
                    if (!excursion.Rating.HasValue) excursion.Rating = 0;
                    excursion.AvailableTicketsByDate = excursion.Availability
                        .GroupBy(a => a.AvailableDateTime.ToString("yyyy-MM-dd HH:mm"))
                        .ToDictionary(
                            g => g.Key,
                            g => g.ToDictionary(a => a.TicketCategory, a => a.AvailableTickets)
                        );
                }

                _logger.LogDebug("Экскурсии обработаны, готов к возврату");
                return Ok(excursions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке экскурсий");
                return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Excursion>> GetExcursion(int id)
        {
            var excursion = await _context.Excursions
                .Include(e => e.Availability)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (excursion == null) return NotFound();

            excursion.Title = excursion.Title ?? "Без названия";
            excursion.Description = excursion.Description ?? "Без описания";
            excursion.Image = excursion.Image ?? "default_image.jpg";
            excursion.Status = excursion.Status ?? "pending";
            excursion.City = excursion.City ?? "Не указан";
            excursion.Attractions ??= new List<Attraction>();
            excursion.Reviews ??= new List<Review>();
            excursion.RejectionReason = excursion.RejectionReason ?? "";

            excursion.AvailableTicketsByDate = excursion.Availability
                .GroupBy(a => new { DateTime = a.AvailableDateTime.ToString("yyyy-MM-dd HH:mm"), a.TicketCategory })
                .GroupBy(g => g.Key.DateTime)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(
                        x => x.Key.TicketCategory,
                        x => x.Sum(a => a.AvailableTickets)
                    )
                );

            return excursion;
        }

        //[HttpPost]
        //[Authorize(Roles = "manager,admin")]
        //public async Task<ActionResult<Excursion>> CreateExcursion(Excursion excursion)
        //{
        //    try
        //    {
        //        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        //        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        //        if (string.IsNullOrEmpty(userRole))
        //        {
        //            return Unauthorized("Роль пользователя не определена.");
        //        }

        //        var user = await _context.Users
        //            .Select(u => new { u.Id, u.OrganizationId }) // Загружаем только нужные поля
        //            .FirstOrDefaultAsync(u => u.Id == userId);
        //        if (user == null)
        //        {
        //            return Unauthorized("Пользователь не найден.");
        //        }

        //        if (userRole == "manager" && user.OrganizationId == null)
        //        {
        //            return BadRequest("Менеджер должен быть привязан к организации.");
        //        }

        //        if (userRole != "admin" && user.OrganizationId.HasValue)
        //        {
        //            excursion.OrganizationId = user.OrganizationId;
        //        }

        //        if (excursion.OrganizationId == null)
        //        {
        //            return BadRequest("Экскурсия должна быть привязана к организации.");
        //        }

        //        excursion.Title = excursion.Title ?? "Без названия";
        //        excursion.Description = excursion.Description ?? "Без описания";
        //        excursion.Image = excursion.Image ?? "default_image.jpg";
        //        excursion.Status = excursion.Status ?? "pending";
        //        excursion.City = excursion.City ?? "Не указан";
        //        excursion.CreatedAt = DateTime.Now;
        //        excursion.Attractions ??= new List<Attraction>();
        //        excursion.Reviews ??= new List<Review>();
        //        excursion.RejectionReason = excursion.RejectionReason ?? "";
        //        excursion.IsIndividual = false;
        //        excursion.GuideId = null;
        //        excursion.ManagerId = userRole == "manager" ? userId : excursion.ManagerId;

        //        _context.Excursions.Add(excursion);
        //        await _context.SaveChangesAsync();

        //        if (excursion.AvailableTicketsByDate != null)
        //        {
        //            foreach (var dateEntry in excursion.AvailableTicketsByDate)
        //            {
        //                foreach (var ticketEntry in dateEntry.Value)
        //                {
        //                    var availability = new ExcursionAvailability
        //                    {
        //                        ExcursionId = excursion.Id,
        //                        AvailableDateTime = DateTime.Parse(dateEntry.Key),
        //                        TicketCategory = ticketEntry.Key,
        //                        AvailableTickets = ticketEntry.Value
        //                    };
        //                    _context.ExcursionAvailability.Add(availability);
        //                }
        //            }
        //            await _context.SaveChangesAsync();
        //        }

        //        return CreatedAtAction(nameof(GetExcursion), new { id = excursion.Id }, excursion);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при создании экскурсии");
        //        return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
        //    }
        //}

        //[HttpPut("{id}")]
        //[Authorize(Roles = "admin,manager")]
        //public async Task<IActionResult> UpdateExcursion(int id, Excursion excursion)
        //{
        //    try
        //    {
        //        if (id != excursion.Id) return BadRequest();

        //        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        //        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        //        if (string.IsNullOrEmpty(userRole))
        //        {
        //            return Unauthorized("Роль пользователя не определена.");
        //        }

        //        var existingExcursion = await _context.Excursions
        //            .Include(e => e.Availability)
        //            .Include(e => e.Organization)
        //            .FirstOrDefaultAsync(e => e.Id == id);
        //        if (existingExcursion == null) return NotFound();

        //        var user = await _context.Users
        //            .Select(u => new { u.Id, u.OrganizationId }) // Загружаем только нужные поля
        //            .FirstOrDefaultAsync(u => u.Id == userId);

        //        if (userRole == "manager")
        //        {
        //            var managerOrg = user.OrganizationId;

        //            if (existingExcursion.OrganizationId != managerOrg)
        //            {
        //                return Forbid("Менеджер может редактировать только экскурсии своей организации.");
        //            }
        //        }
        //        else if (userRole != "admin")
        //        {
        //            return Forbid("Редактирование экскурсий доступно только администраторам и менеджерам.");
        //        }

        //        if (excursion.OrganizationId == null)
        //        {
        //            return BadRequest("Экскурсия должна быть привязана к организации.");
        //        }

        //        existingExcursion.Title = excursion.Title ?? "Без названия";
        //        existingExcursion.Description = excursion.Description ?? "Без описания";
        //        existingExcursion.Image = excursion.Image ?? "default_image.jpg";
        //        existingExcursion.Status = excursion.Status ?? "pending";
        //        existingExcursion.City = excursion.City ?? "Не указан";
        //        existingExcursion.Price = excursion.Price;
        //        existingExcursion.IsIndividual = excursion.IsIndividual;
        //        existingExcursion.Rating = excursion.Rating;
        //        existingExcursion.OrganizationId = excursion.OrganizationId;
        //        existingExcursion.GuideId = null;
        //        existingExcursion.ManagerId = userRole == "manager" ? userId : excursion.ManagerId;
        //        existingExcursion.Attractions = excursion.Attractions ?? new List<Attraction>();
        //        existingExcursion.Reviews = excursion.Reviews ?? new List<Review>();
        //        existingExcursion.RejectionReason = excursion.RejectionReason ?? "";

        //        if (excursion.AvailableTicketsByDate != null)
        //        {
        //            var oldAvailability = existingExcursion.Availability.ToList();
        //            _context.ExcursionAvailability.RemoveRange(oldAvailability);

        //            foreach (var dateEntry in excursion.AvailableTicketsByDate)
        //            {
        //                foreach (var ticketEntry in dateEntry.Value)
        //                {
        //                    var availability = new ExcursionAvailability
        //                    {
        //                        ExcursionId = id,
        //                        AvailableDateTime = DateTime.Parse(dateEntry.Key),
        //                        TicketCategory = ticketEntry.Key,
        //                        AvailableTickets = ticketEntry.Value
        //                    };
        //                    _context.ExcursionAvailability.Add(availability);
        //                }
        //            }
        //        }

        //        await _context.SaveChangesAsync();
        //        return NoContent();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при обновлении экскурсии с id {Id}", id);
        //        return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
        //    }
        //}

        //[HttpDelete("{id}")]
        //[Authorize(Roles = "admin,manager")]
        //public async Task<IActionResult> DeleteExcursion(int id)
        //{
        //    try
        //    {
        //        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        //        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        //        if (string.IsNullOrEmpty(userRole))
        //        {
        //            return Unauthorized("Роль пользователя не определена.");
        //        }

        //        var excursion = await _context.Excursions
        //            .Include(e => e.Availability)
        //            .Include(e => e.Organization)
        //            .FirstOrDefaultAsync(e => e.Id == id);

        //        if (excursion == null) return NotFound();

        //        var managerOrg = await _context.Users
        //            .Where(u => u.Id == userId)
        //            .Select(u => u.OrganizationId)
        //            .FirstOrDefaultAsync();

        //        if (userRole == "manager")
        //        {
        //            if (excursion.OrganizationId != managerOrg)
        //            {
        //                return Forbid("Менеджер может удалять только экскурсии своей организации.");
        //            }
        //        }
        //        else if (userRole != "admin")
        //        {
        //            return Forbid("Удаление экскурсий доступно только администраторам и менеджерам.");
        //        }

        //        if (excursion.OrganizationId == null)
        //        {
        //            return BadRequest("Экскурсия должна быть привязана к организации.");
        //        }

        //        excursion.Title = excursion.Title ?? "Без названия";
        //        excursion.Description = excursion.Description ?? "Без описания";
        //        excursion.City = excursion.City ?? "Не указан";
        //        excursion.Image = excursion.Image ?? "default_image.jpg";
        //        excursion.Status = excursion.Status ?? "pending";
        //        excursion.RejectionReason = excursion.RejectionReason ?? "";

        //        var availability = excursion.Availability.ToList();
        //        _context.ExcursionAvailability.RemoveRange(availability);

        //        _context.Excursions.Remove(excursion);
        //        await _context.SaveChangesAsync();
        //        return NoContent();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при удалении экскурсии с id {Id}", id);
        //        return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
        //    }
        //}

        [HttpGet("top")]
        public async Task<ActionResult<IEnumerable<Excursion>>> GetTopExcursions()
        {
            try
            {
                var topExcursions = await _context.Excursions
                    .Include(e => e.Availability)
                    .OrderByDescending(e => e.Rating)
                    .Take(3)
                    .ToListAsync();

                foreach (var excursion in topExcursions)
                {
                    excursion.Title = excursion.Title ?? "Без названия";
                    excursion.Description = excursion.Description ?? "Без описания";
                    excursion.Image = excursion.Image ?? "default_image.jpg";
                    excursion.Status = excursion.Status ?? "pending";
                    excursion.City = excursion.City ?? "Не указан";
                    excursion.Attractions ??= new List<Attraction>();
                    excursion.Reviews ??= new List<Review>();
                    excursion.RejectionReason = excursion.RejectionReason ?? "";
                    excursion.AvailableTicketsByDate = excursion.Availability
                        .GroupBy(a => a.AvailableDateTime.ToString("yyyy-MM-dd HH:mm"))
                        .ToDictionary(
                            g => g.Key,
                            g => g.ToDictionary(a => a.TicketCategory, a => a.AvailableTickets)
                        );
                }

                return Ok(topExcursions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке топ экскурсий");
                return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
            }
        }
    }
}