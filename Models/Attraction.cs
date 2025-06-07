using NetTopologySuite.Geometries;
using OnlineTourGuide.Models;

public class Attraction
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Image { get; set; }
    public Point Coordinates { get; set; } // Используем тип Point для MySQL
    public string History { get; set; }
    public string VisitingHours { get; set; }
    public string City { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? Rating { get; set; }
    public List<Excursion> Excursions { get; set; } = new List<Excursion>();
    public List<Review> Reviews { get; set; } = new List<Review>();
}