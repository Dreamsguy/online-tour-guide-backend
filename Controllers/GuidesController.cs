using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data; // Убедись, что пространство имен совпадает
using OnlineTourGuide.Models;
using System.Linq;
using System.Threading.Tasks;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GuidesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GuidesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{id}/analytics")]
        [Authorize]
        public async Task<ActionResult<object>> GetAnalytics(int id)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || int.Parse(userIdClaim) != id)
            {
                return Unauthorized(new { message = "Доступ запрещен." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role != Role.Guide)
            {
                return NotFound(new { message = "Гид не найден." });
            }

            var excursions = await _context.Excursions.Where(e => e.GuideId == id).ToListAsync();
            var excursionIds = excursions.Select(e => e.Id).ToList();
            var bookings = await _context.Bookings.Where(b => excursionIds.Contains(b.ExcursionId)).ToListAsync();
            var reviews = await _context.Reviews.Where(r => excursionIds.Contains(r.ExcursionId ?? 0)).ToListAsync();

            var analytics = new
            {
                totalBookings = bookings.Count,
                averageRating = reviews.Any() ? (double?)reviews.Average(r => r.Rating) : null 
            };

            return Ok(analytics);
        }
    }
}