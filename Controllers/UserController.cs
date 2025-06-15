using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserController> _logger;

    public UserController(ApplicationDbContext context, ILogger<UserController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        _logger.LogInformation("GET /api/users, User: {UserId}, Role: {Role}, Claims: {@Claims}",
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            User.FindFirst(ClaimTypes.Role)?.Value,
            User.Claims);
        return await _context.Users.ToListAsync();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            _logger.LogInformation("DELETE /api/users/{id}, User: {UserId}, Role: {Role}, Claims: {@Claims}",
                id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindFirst(ClaimTypes.Role)?.Value, User.Claims);
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении пользователя с ID: {Id}", id);
            return StatusCode(500, $"Ошибка при удалении пользователя: {ex.Message}");
        }
    }

    [HttpPost]
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("POST /api/users, User: {UserId}, Role: {Role}, Claims: {@Claims}, NewUser: {@User}",
            userIdClaim, User.FindFirst(ClaimTypes.Role)?.Value, User.Claims, user);
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized("Пользователь не авторизован.");
        }

        var currentUserId = int.Parse(userIdClaim);
        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUserId);
        if (currentUser == null)
        {
            return Unauthorized("Пользователь не найден.");
        }

        if (currentUser.Role != Role.Admin && currentUser.Role != Role.Manager)
        {
            return Unauthorized("Только администраторы и менеджеры могут создавать пользователей.");
        }

        if (user.Role == Role.Admin)
        {
            return BadRequest("Создание администраторов запрещено.");
        }

        if (user.Role == Role.Guide && user.OrganizationId == null)
        {
            return BadRequest("Гид должен быть привязан к организации.");
        }

        if (currentUser.Role == Role.Manager && user.OrganizationId != currentUser.OrganizationId)
        {
            return Unauthorized("Вы можете создавать пользователей только для своей организации.");
        }

        user.CreatedAt = DateTime.Now;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreferences(int userId, [FromBody] UserPreference prefs)
    {
        var existingPrefs = await _context.UserPreferences.FirstOrDefaultAsync(up => up.UserId == userId);
        if (existingPrefs == null)
        {
            prefs.UserId = userId;
            _context.UserPreferences.Add(prefs);
        }
        else
        {
            existingPrefs.PreferredCity = prefs.PreferredCity;
            existingPrefs.PreferredDirection = prefs.PreferredDirection; // Обновляем Direction
            existingPrefs.PreferredAttractions = prefs.PreferredAttractions; // Обновляем массив
            existingPrefs.UpdatedAt = prefs.UpdatedAt;
            _context.UserPreferences.Update(existingPrefs);
        }
        await _context.SaveChangesAsync();
        return Ok(prefs);
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(int userId)
    {
        var preference = await _context.UserPreferences.FirstOrDefaultAsync(up => up.UserId == userId);
        if (preference == null) return NotFound();
        return Ok(preference);
    }

    [HttpGet("recommendations/{userId}")]
    public async Task<IActionResult> GetRecommendations(int userId)
    {
        var preferences = await _context.UserPreferences.FirstOrDefaultAsync(up => up.UserId == userId);
        var actions = await _context.UserActions
            .Where(ua => ua.UserId == userId)
            .GroupBy(ua => ua.ExcursionId)
            .Select(g => new { ExcursionId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        var excursions = await _context.Excursions.ToListAsync(); // Загружаем все экскурсии
        var recommendedExcursions = new List<Excursion>();

        if (preferences != null)
        {
            recommendedExcursions = excursions
                .Where(e => (string.IsNullOrEmpty(preferences.PreferredCity) || e.City == preferences.PreferredCity) &&
                            (string.IsNullOrEmpty(preferences.PreferredDirection) || e.Direction == preferences.PreferredDirection))
                .ToList();
        }

        if (actions.Any())
        {
            var popularExcursionIds = actions.Take(5).Select(a => a.ExcursionId);
            var popularExcursions = excursions
                .Where(e => popularExcursionIds.Contains(e.Id))
                .ToList();
            recommendedExcursions.AddRange(popularExcursions);
        }

        return Ok(recommendedExcursions.Distinct().Take(5).ToList());
    }

    [HttpPost("useractions")]
    public async Task<IActionResult> RecordUserAction([FromBody] UserAction action)
    {
        _context.UserActions.Add(action);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("Directions")]
    public async Task<IActionResult> GetDirections()
    {
        var directions = await _context.Excursions
            .Select(e => e.Direction)
            .Distinct()
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => new { Name = c, Count = _context.Excursions.Count(e => e.Direction == c) })
            .ToListAsync();
        return Ok(directions);
    }
}