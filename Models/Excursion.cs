using OnlineTourGuide.Models;
using System.ComponentModel.DataAnnotations.Schema;

public class Excursion
{
    public int Id { get; set; } // Not nullable
    public string Title { get; set; }
    public string Description { get; set; }
    public string City { get; set; }
    public decimal Price { get; set; } // Not nullable
    public string Image { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; } // Not nullable
    public bool IsIndividual { get; set; } // Not nullable
    public decimal? Rating { get; set; } // Nullable

    public int? GuideId { get; set; } // Nullable
    public User Guide { get; set; }
    public int? ManagerId { get; set; } // Nullable
    public User Manager { get; set; }
    public int? OrganizationId { get; set; } // Nullable
    public Organization Organization { get; set; }

    public List<Attraction> Attractions { get; set; }
    public List<Review> Reviews { get; set; }
    public List<ExcursionAvailability> Availability { get; set; }
    [NotMapped]
    public Dictionary<string, Dictionary<string, int>> AvailableTicketsByDate { get; set; }

    public string RejectionReason { get; set; }
}