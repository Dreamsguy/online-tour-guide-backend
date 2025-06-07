using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using Microsoft.Extensions.Logging;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttractionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AttractionsController> _logger;

        public AttractionsController(ApplicationDbContext context, ILogger<AttractionsController> logger)
        {
            _context = context;
            _logger = logger;
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
                        Coordinates = new { x = a.Coordinates.X, y = a.Coordinates.Y } // Преобразование Point
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

        [HttpGet("{id}")]
        public async Task<ActionResult<Attraction>> GetAttraction(int id)
        {
            var attraction = await _context.Attractions
                .Include(a => a.Excursions)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (attraction == null) return NotFound();
            return attraction;
        }

        [HttpGet("attraction/{attractionId}")]
        public async Task<ActionResult<IEnumerable<Review>>> GetReviewsForAttraction(int attractionId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.AttractionId == attractionId)
                .ToListAsync();
            return Ok(reviews);
        }

        [HttpPost]
        public async Task<ActionResult<Attraction>> CreateAttraction(Attraction attraction)
        {
            attraction.Rating = null; // Устанавливаем NULL
            attraction.CreatedAt = DateTime.UtcNow;
            _context.Attractions.Add(attraction);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAttraction), new { id = attraction.Id }, attraction);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAttraction(int id, Attraction attraction)
        {
            if (id != attraction.Id) return BadRequest();
            _context.Entry(attraction).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttraction(int id)
        {
            var attraction = await _context.Attractions.FindAsync(id);
            if (attraction == null) return NotFound();
            _context.Attractions.Remove(attraction);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}