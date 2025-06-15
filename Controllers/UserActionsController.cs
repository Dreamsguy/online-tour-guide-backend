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
    public class UserActionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserActionsController> _logger;

        public UserActionsController(ApplicationDbContext context, ILogger<UserActionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateUserAction([FromBody] UserAction action)
        {
            if (action == null || action.UserId <= 0 || string.IsNullOrEmpty(action.ActionType) || action.ExcursionId <= 0)
            {
                _logger.LogWarning("Invalid user action data: UserId={UserId}, ActionType={ActionType}, ExcursionId={ExcursionId}",
                    action?.UserId, action?.ActionType, action?.ExcursionId);
                return BadRequest(new { message = "Некорректные данные действия. UserId, ActionType и ExcursionId должны быть валидными." });
            }

            _logger.LogInformation("Creating user action: UserId={UserId}, ActionType={ActionType}, ExcursionId={ExcursionId}",
                action.UserId, action.ActionType, action.ExcursionId);

            var userAction = new UserAction
            {
                UserId = action.UserId,
                ActionType = action.ActionType,
                ExcursionId = action.ExcursionId,
                Timestamp = DateTime.UtcNow
            };

            _context.UserActions.Add(userAction);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Действие успешно сохранено", id = userAction.Id });
        }
    }
}