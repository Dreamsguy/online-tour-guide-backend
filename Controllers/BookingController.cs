using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(ApplicationDbContext context, ILogger<BookingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Booking>>> GetBookings()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован." });
                }

                var userId = int.Parse(userIdClaim);
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = "Пользователь не найден." });
                }

                if (user.Role == Role.Guide)
                {
                    var bookings = await _context.Bookings
                        .Include(b => b.Excursion)
                        .Include(b => b.User)
                        .Where(b => b.Excursion.GuideId == userId)
                        .ToListAsync();
                    return Ok(bookings);
                }
                else if (user.Role == Role.User)
                {
                    var bookings = await _context.Bookings
                        .Include(b => b.Excursion)
                        .Where(b => b.UserId == userId)
                        .ToListAsync();
                    return Ok(bookings);
                }
                else if (user.Role == Role.Admin || user.Role == Role.Manager)
                {
                    var bookings = await _context.Bookings
                        .Include(b => b.Excursion)
                        .Include(b => b.User)
                        .ToListAsync();
                    return Ok(bookings);
                }

                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке бронирований");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Booking>>> GetBookingsForUser(int userId)
        {
            try
            {
                var requestingUserIdClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(requestingUserIdClaim))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован." });
                }

                var requestingUserId = int.Parse(requestingUserIdClaim);
                if (requestingUserId != userId)
                {
                    return Unauthorized(new { message = "Вы можете запрашивать только свои бронирования." });
                }

                var bookings = await _context.Bookings
                    .Include(b => b.Excursion)
                    .Where(b => b.UserId == userId)
                    .ToListAsync();

                foreach (var booking in bookings)
                {
                    if (booking.DateTime.HasValue &&
                        booking.Status != null &&
                        booking.Status.ToLower() == "pending" &&
                        booking.DateTime.Value < DateTime.UtcNow)
                    {
                        booking.Status = "Completed";
                    }
                }
                await _context.SaveChangesAsync();

                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке бронирований для пользователя с id {UserId}", userId);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Booking>> CreateBooking([FromBody] BookingModel bookingModel)
        {
            try
            {
                if (bookingModel == null)
                    return BadRequest(new { message = "Тело запроса не может быть пустым." });

                var userIdClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized(new { message = "Пользователь не авторизован." });

                int userId = int.Parse(userIdClaim); // Простое приведение, ошибка обработается ниже
                if (userId != bookingModel.UserId)
                    return Unauthorized(new { message = "Вы можете бронировать только от своего имени." });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null || user.Role != Role.User)
                    return Unauthorized(new { message = "Только пользователи могут бронировать экскурсии." });

                var excursion = await _context.Excursions
                    .Include(e => e.Tickets)
                    .FirstOrDefaultAsync(e => e.Id == bookingModel.ExcursionId);
                if (excursion == null)
                    return NotFound(new { message = $"Экскурсия с Id {bookingModel.ExcursionId} не найдена" });

                if (!excursion.GuideId.HasValue)
                    return BadRequest(new { message = "У экскурсии нет назначенного гида" });

                if (string.IsNullOrEmpty(bookingModel.TicketCategory) || string.IsNullOrEmpty(bookingModel.DateTime))
                    return BadRequest(new { message = "Категория и дата/время бронирования обязательны." });

                if (!DateTime.TryParseExact(bookingModel.DateTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDateTime))
                    return BadRequest(new { message = "Неверный формат даты. Ожидается: yyyy-MM-dd HH:mm" });

                var ticket = excursion.Tickets.FirstOrDefault(t =>
                    t.Date == parsedDateTime.ToString("yyyy-MM-dd") &&
                    t.Time == parsedDateTime.ToString("HH:mm") &&
                    t.Type == bookingModel.TicketCategory);

                if (ticket == null || ticket.Total - ticket.Sold < (bookingModel.Quantity > 0 ? bookingModel.Quantity : 1))
                    return BadRequest(new { message = $"Недостаточно билетов в категории {bookingModel.TicketCategory} на {bookingModel.DateTime}." });

                int slots = bookingModel.Quantity > 0 ? bookingModel.Quantity : 1;
                ticket.Sold += slots;
                _context.Excursions.Update(excursion);

                var booking = new Booking
                {
                    UserId = bookingModel.UserId,
                    ExcursionId = bookingModel.ExcursionId,
                    TicketCategory = bookingModel.TicketCategory,
                    DateTime = parsedDateTime,
                    Quantity = slots,
                    Status = bookingModel.Status ?? "Pending",
                    Image = bookingModel.Image ?? "default_image.jpg",
                    PaymentMethod = bookingModel.PaymentMethod ?? "NotSpecified",
                    Total = bookingModel.Total ?? ticket.Price * slots,
                    Timestamp = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                var guideNotification = new Notification
                {
                    UserId = excursion.GuideId.Value,
                    Message = $"Забронировано: {excursion.Title ?? "Экскурсия"}",
                    Timestamp = DateTime.UtcNow
                };
                _context.Notifications.Add(guideNotification);

                var userNotification = new Notification
                {
                    UserId = booking.UserId,
                    Message = $"Вы забронировали: {excursion.Title ?? "Экскурсия"}",
                    Timestamp = DateTime.UtcNow
                };
                _context.Notifications.Add(userNotification);

                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Неверный формат идентификатора пользователя." });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = "Ошибка при сохранении в базу данных", error = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Произошла ошибка на сервере", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Booking>> GetBooking(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Excursion)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound();
            }

            return booking;
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateBooking(int id, [FromBody] BookingModel updatedBooking)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован." });
                }

                var userId = int.Parse(userIdClaim);
                var booking = await _context.Bookings
                    .Include(b => b.Excursion)
                    .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
                if (booking == null)
                {
                    return NotFound(new { message = "Бронирование не найдено." });
                }

                if (!booking.DateTime.HasValue ||
                    (booking.Status != null && booking.Status.ToLower() != "pending") ||
                    booking.DateTime.Value <= DateTime.UtcNow)
                {
                    return BadRequest(new { message = "Нельзя редактировать завершенные или истёкшие бронирования." });
                }

                var excursion = booking.Excursion;
                var oldTicket = excursion.Tickets.FirstOrDefault(t =>
                    t.Date == booking.DateTime.Value.ToString("yyyy-MM-dd") &&
                    t.Time == booking.DateTime.Value.ToString("HH:mm") &&
                    t.Type == booking.TicketCategory);

                if (oldTicket != null)
                {
                    oldTicket.Sold -= (int)booking.Quantity;
                }

                if (string.IsNullOrEmpty(updatedBooking.DateTime))
                {
                    return BadRequest(new { message = "Дата и время бронирования обязательны." });
                }

                if (!DateTime.TryParseExact(updatedBooking.DateTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDateTime))
                {
                    return BadRequest(new { message = "Неверный формат даты. Ожидается: yyyy-MM-dd HH:mm" });
                }

                var newTicket = excursion.Tickets.FirstOrDefault(t =>
                    t.Date == parsedDateTime.ToString("yyyy-MM-dd") &&
                    t.Time == parsedDateTime.ToString("HH:mm") &&
                    t.Type == updatedBooking.TicketCategory);

                if (newTicket == null || newTicket.Total - newTicket.Sold < (updatedBooking.Quantity > 0 ? updatedBooking.Quantity : 1))
                {
                    return BadRequest(new { message = $"Недостаточно билетов в категории {updatedBooking.TicketCategory} на {updatedBooking.DateTime}." });
                }

                int newSlots = updatedBooking.Quantity > 0 ? updatedBooking.Quantity : 1;
                newTicket.Sold += newSlots;
                _context.Excursions.Update(excursion);

                booking.TicketCategory = updatedBooking.TicketCategory ?? booking.TicketCategory ?? "Стандарт";
                booking.DateTime = parsedDateTime;
                booking.Quantity = newSlots;
                booking.Total = updatedBooking.Total ?? newTicket.Price * newSlots;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении бронирования с id {Id}", id);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("guide/{guideId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Booking>>> GetGuideBookings(int guideId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || int.Parse(userIdClaim) != guideId)
                {
                    return Unauthorized(new { message = "Вы можете видеть только свои бронирования." });
                }

                var bookings = await _context.Bookings
                    .Include(b => b.Excursion)
                    .Include(b => b.User)
                    .Where(b => b.Excursion.GuideId == guideId)
                    .ToListAsync();
                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке бронирований для гида: {GuideId}", guideId);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован." });
                }

                var userId = int.Parse(userIdClaim);
                var booking = await _context.Bookings
                    .Include(b => b.Excursion)
                    .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
                if (booking == null)
                {
                    return NotFound(new { message = "Бронирование не найдено." });
                }

                var excursion = booking.Excursion;
                var ticket = excursion.Tickets.FirstOrDefault(t =>
                    t.Date == booking.DateTime.Value.ToString("yyyy-MM-dd") &&
                    t.Time == booking.DateTime.Value.ToString("HH:mm") &&
                    t.Type == booking.TicketCategory);

                if (ticket != null)
                {
                    ticket.Sold -= (int)booking.Quantity;
                    _context.Excursions.Update(excursion);
                }

                booking.Status = "Cancelled";
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении бронирования с id {Id}", id);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера: " + ex.Message });
            }
        }
    }

    public class BookingModel
    {
        public int UserId { get; set; }
        public int ExcursionId { get; set; }
        public string TicketCategory { get; set; }
        public string DateTime { get; set; } // Изменили с DateTime? на string
        public int Quantity { get; set; }
        public string Status { get; set; }
        public string Image { get; set; }
        public string PaymentMethod { get; set; }
        public decimal? Total { get; set; }
    }
}