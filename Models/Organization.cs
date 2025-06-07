using OnlineTourGuide.Models;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = null!; // NOT NULL в базе, но используем null! для EF
    public string INN { get; set; } = null!; // NOT NULL в базе
    public string? Email { get; set; } // Может быть NULL
    public string? Phone { get; set; }
    public string? WorkingHours { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Directions { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int? Rating { get; set; }
    public string? Categories { get; set; }
    public List<Review> Reviews { get; set; } = new List<Review>();
    public List<User> Users { get; set; } = new List<User>();
    public List<Excursion> Excursions { get; set; } = new List<Excursion>();
}