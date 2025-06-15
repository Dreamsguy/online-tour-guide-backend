using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using OnlineTourGuide.Models;

public class Excursion
{
    public Excursion()
    {
        TicketsJson = JsonSerializer.Serialize(new List<Ticket>());
        ImagesJson = JsonSerializer.Serialize(new List<string>());
        RouteJson = JsonSerializer.Serialize(new List<double[]>());
    }

    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Direction { get; set; }
    public string? Description { get; set; }
    public string? City { get; set; }
    public bool IsIndividual { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? Rating { get; set; }
    public string? RejectionReason { get; set; }

    public int? GuideId { get; set; } // Сделали nullable, чтобы не требовалась организация
    public User? Guide { get; set; }

    public int? ManagerId { get; set; } // Сделали nullable, чтобы не требовалась организация
    public User? Manager { get; set; }

    public int OrganizationId { get; set; } // Остаётся обязательным
    public Organization? Organization { get; set; }

    public List<Attraction> Attractions { get; set; } = new List<Attraction>();
    public List<Review> Reviews { get; set; } = new List<Review>();

    [Column("Tickets")]
    public string TicketsJson { get; set; }
    [NotMapped]
    [JsonIgnore]
    public List<Ticket> Tickets
    {
        get => string.IsNullOrEmpty(TicketsJson) ? new List<Ticket>() : JsonSerializer.Deserialize<List<Ticket>>(TicketsJson) ?? new List<Ticket>();
        set => TicketsJson = JsonSerializer.Serialize(value);
    }

    [Column("Images")]
    public string ImagesJson { get; set; }
    [NotMapped]
    [JsonIgnore]
    public List<string> Images
    {
        get => string.IsNullOrEmpty(ImagesJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(ImagesJson) ?? new List<string>();
        set => ImagesJson = JsonSerializer.Serialize(value);
    }

    [Column("Route")]
    public string RouteJson { get; set; }
    [NotMapped]
    [JsonIgnore]
    public List<double[]> Route
    {
        get => string.IsNullOrEmpty(RouteJson) ? new List<double[]>() : JsonSerializer.Deserialize<List<double[]>>(RouteJson) ?? new List<double[]>();
        set => RouteJson = JsonSerializer.Serialize(value);
    }

    public bool IsForDisabled { get; set; }
    public bool IsForChildren { get; set; }

    [NotMapped]
    public Dictionary<string, Dictionary<string, TicketAvailability>> AvailableTicketsByDate
    {
        get
        {
            if (Tickets == null || !Tickets.Any())
                return new Dictionary<string, Dictionary<string, TicketAvailability>>();

            return Tickets
                .GroupBy(t => $"{t.Date} {t.Time}")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select((t, index) => new { Ticket = t, Index = index })
                        .ToDictionary(
                            x => string.IsNullOrEmpty(x.Ticket.Type) ? $"default_{x.Index}" : x.Ticket.Type,
                            x => new TicketAvailability
                            {
                                Count = x.Ticket.Total - (x.Ticket.Sold ?? 0),
                                Price = x.Ticket.Price,
                                Currency = x.Ticket.Currency
                            },
                            StringComparer.OrdinalIgnoreCase
                        )
                );
        }
        set { }
    }
}