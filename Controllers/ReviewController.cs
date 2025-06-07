using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System.Security.Claims;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController(ApplicationDbContext context, ILogger<ReviewsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("excursion/{excursionId}")]
        public async Task<ActionResult<IEnumerable<Review>>> GetReviewsForExcursion(int excursionId)
        {
            try
            {
                var reviews = await _context.Reviews
                    .Include(r => r.User)
                    .Where(r => r.ExcursionId == excursionId)
                    .ToListAsync();
                return Ok(reviews);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке отзывов для экскурсии с id {ExcursionId}", excursionId);
                return StatusCode(500, "Внутренняя ошибка сервера: " + ex.Message);
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Review>> CreateReview([FromBody] Review review)
        {
            try
            {
                if (review == null)
                {
                    return BadRequest(new { message = "Данные отзыва не могут быть пустыми." });
                }

                // Проверка авторизации
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован." });
                }

                var userId = int.Parse(userIdClaim);
                if (userId != review.UserId)
                {
                    return Unauthorized(new { message = "Вы можете оставлять отзывы только от своего имени." });
                }

                // Проверка существования пользователя
                var user = await _context.Users.FindAsync(review.UserId);
                if (user == null)
                {
                    return BadRequest(new { message = $"Пользователь с ID {review.UserId} не найден." });
                }

                // Проверка существования экскурсии
                if (!review.ExcursionId.HasValue)
                {
                    return BadRequest(new { message = "ExcursionId обязателен." });
                }

                var excursion = await _context.Excursions.FindAsync(review.ExcursionId.Value);
                if (excursion == null)
                {
                    return BadRequest(new { message = $"Экскурсия с ID {review.ExcursionId} не найдена." });
                }

                // Валидация рейтинга
                if (review.Rating < 1 || review.Rating > 5)
                {
                    return BadRequest(new { message = "Рейтинг должен быть от 1 до 5." });
                }

                // Установка текущей даты и обнуление ненужных полей
                review.CreatedAt = DateTime.Now;
                review.OrganizationId = null;
                review.AttractionId = null;

                // Игнорируем навигационные свойства при сохранении
                review.Excursion = null;
                review.Organization = null;
                review.User = null;
                review.Attraction = null;

                // Сохранение отзыва
                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                // Обновление рейтинга экскурсии
                var reviews = await _context.Reviews.Where(r => r.ExcursionId == review.ExcursionId).ToListAsync();
                excursion.Rating = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : 0;
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetReviewsForExcursion), new { excursionId = review.ExcursionId }, review);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании отзыва");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован." });
                }

                var userId = int.Parse(userIdClaim);
                var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
                if (review == null)
                {
                    return NotFound(new { message = "Отзыв не найден или вы не можете его удалить." });
                }

                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();

                // Обновляем рейтинг экскурсии
                if (review.ExcursionId.HasValue)
                {
                    var reviews = await _context.Reviews.Where(r => r.ExcursionId == review.ExcursionId).ToListAsync();
                    var excursion = await _context.Excursions.FindAsync(review.ExcursionId.Value);
                    if (excursion != null)
                    {
                        excursion.Rating = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : 0;
                        await _context.SaveChangesAsync();
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении отзыва с id {Id}", id);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера", error = ex.Message });
            }
        }
    }
}