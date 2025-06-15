using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineTourGuide.Data;
using OnlineTourGuide.Models;

[Route("api/[controller]")]
[ApiController]
public class OrganizationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrganizationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Organization>>> GetOrganizations()
    {
        return await _context.Organizations.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrgById(int id)
    {
        try
        {
            var organization = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == id);
            if (organization == null)
                return NotFound(new { message = "Организация не найдена" });

            return Ok(organization);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка: " + ex.Message });
        }
    }

    [HttpGet("{id}/excursions")]
    public async Task<IActionResult> GetExcursionsByOrganization(int id)
    {
        try
        {
            var organization = await _context.Organizations
                .Include(o => o.Excursions)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (organization == null)
                return NotFound(new { message = "Организация не найдена" });

            var excursions = organization.Excursions.Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                e.City,
                // Исправляем тип для Prices
                Prices = e.Tickets.Any() ?
                    e.Tickets.Select(t => new { t.Type, t.Price }).Cast<object>().ToList() :
                    new List<object> { new { Type = "N/A", Price = 0m } },
                e.Rating
            }).ToList();

            return Ok(excursions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка: " + ex.Message });
        }
    }

    [HttpGet("{id}/guides")]
    public async Task<IActionResult> GetGuidesByOrganization(int id)
    {
        try
        {
            var organization = await _context.Organizations
                .Include(o => o.Users) // Предполагаем, что гиды хранятся в Users с ролью 'guide'
                .FirstOrDefaultAsync(o => o.Id == id);

            if (organization == null)
                return NotFound(new { message = "Организация не найдена" });

            var guides = organization.Users
                .Where(u => u.Role == Role.Guide)
                .Select(u => new
                {
                    u.Id,
                    u.Name, // Или другое поле, например, u.FullName
                    u.Email
                })
                .ToList();

            return Ok(guides);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка: " + ex.Message });
        }
    }

    [HttpGet("{id}/managers")]
    public async Task<IActionResult> GetManagersByOrganization(int id)
    {
        try
        {
            var organization = await _context.Organizations
                .Include(o => o.Users)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (organization == null)
                return NotFound(new { message = "Организация не найдена" });

            var managers = organization.Users
                .Where(u => u.Role == Role.Manager)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email
                })
                .ToList();

            return Ok(managers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка: " + ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<Organization>> CreateOrganization(Organization organization)
    {
        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOrgById), new { id = organization.Id }, organization);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateOrganization(int id, Organization organization)
    {
        if (id != organization.Id)
        {
            return BadRequest();
        }

        _context.Entry(organization).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrganization(int id)
    {
        try
        {
            var organization = await _context.Organizations.FindAsync(id);
            if (organization == null) return NotFound();

            _context.Organizations.Remove(organization);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка при удалении организации: {ex.Message}");
        }
    }
}