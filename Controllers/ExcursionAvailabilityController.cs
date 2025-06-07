using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System.Threading.Tasks;

namespace OnlineTourGuide.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExcursionAvailabilityController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ExcursionAvailabilityController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ExcursionAvailability>> CreateAvailability([FromBody] ExcursionAvailability availability)
        {
            _context.ExcursionAvailability.Add(availability);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAvailability), new { id = availability.Id }, availability);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ExcursionAvailability>> GetAvailability(int id)
        {
            var availability = await _context.ExcursionAvailability.FindAsync(id);
            if (availability == null)
            {
                return NotFound();
            }
            return availability;
        }
    }
}