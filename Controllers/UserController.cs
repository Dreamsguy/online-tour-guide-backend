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

    public UserController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users.ToListAsync();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка при удалении пользователя: {ex.Message}");
        }
    }

    [HttpPost]
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
}