using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;
using System.Security.Claims;

[Route("api/[controller]")]
[ApiController]
public class SchedulesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(ApplicationDbContext context, ILogger<SchedulesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("guide/{guideId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Schedule>>> GetGuideSchedule(int guideId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized(new { message = "Пользователь не авторизован." });
        }

        var userId = int.Parse(userIdClaim);
        var user = await _context.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return Unauthorized(new { message = "Пользователь не найден." });
        }

        if (user.Role != Role.Guide && user.Role != Role.Manager && user.Role != Role.Admin)
        {
            return Unauthorized(new { message = "Доступно только для гидов, менеджеров и администраторов." });
        }

        if (user.Role == Role.Guide && userId != guideId)
        {
            return Unauthorized(new { message = "Вы можете видеть только своё расписание." });
        }

        if (user.Role == Role.Manager)
        {
            var guide = await _context.Users
                .FirstOrDefaultAsync(g => g.Id == guideId && g.Role == Role.Guide);
            if (guide == null || guide.OrganizationId != user.OrganizationId)
            {
                return Unauthorized(new { message = "Вы можете видеть расписание только гидов своей организации." });
            }
        }

        var schedule = await _context.Schedules
            .Include(s => s.Excursion)
            .Where(s => s.GuideId == guideId && s.Status != "Cancelled")
            .ToListAsync();

        return Ok(schedule);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Schedule>> CreateSchedule([FromBody] ScheduleModel scheduleModel)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized(new { message = "Пользователь не авторизован." });
        }

        var userId = int.Parse(userIdClaim);
        var user = await _context.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return Unauthorized(new { message = "Пользователь не найден." });
        }

        if (user.Role != Role.Manager && user.Role != Role.Admin)
        {
            return Unauthorized(new { message = "Только менеджеры и администраторы могут создавать расписание." });
        }

        var guide = await _context.Users
            .FirstOrDefaultAsync(g => g.Id == scheduleModel.GuideId && g.Role == Role.Guide);
        if (guide == null)
        {
            return BadRequest(new { message = "Гид не найден." });
        }

        if (user.Role == Role.Manager && guide.OrganizationId != user.OrganizationId)
        {
            return Unauthorized(new { message = "Вы можете создавать расписание только для гидов своей организации." });
        }

        var excursion = await _context.Excursions
            .FirstOrDefaultAsync(e => e.Id == scheduleModel.ExcursionId);
        if (excursion == null || excursion.GuideId.HasValue) // Проверяем, что экскурсия не привязана к другому гиду
        {
            return BadRequest(new { message = "Экскурсия не найдена или уже назначена другому гиду." });
        }

        // Проверка пересечений
        var existingSchedules = await _context.Schedules
            .Where(s => s.GuideId == scheduleModel.GuideId &&
                        s.Status != "Cancelled" &&
                        s.StartTime < scheduleModel.EndTime &&
                        s.EndTime > scheduleModel.StartTime)
            .ToListAsync();

        if (existingSchedules.Any())
        {
            return BadRequest(new { message = "У гида уже есть экскурсия в это время." });
        }

        var schedule = new Schedule
        {
            GuideId = scheduleModel.GuideId,
            ExcursionId = scheduleModel.ExcursionId,
            StartTime = scheduleModel.StartTime,
            EndTime = scheduleModel.EndTime,
            Status = "Planned"
        };

        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, schedule);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Schedule>> GetSchedule(int id)
    {
        var schedule = await _context.Schedules
            .Include(s => s.Guide)
            .Include(s => s.Excursion)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (schedule == null)
        {
            return NotFound();
        }

        return schedule;
    }
}

public class ScheduleModel
{
    public int GuideId { get; set; }
    public int ExcursionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}