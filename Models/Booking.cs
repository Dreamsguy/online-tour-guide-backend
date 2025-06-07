using OnlineTourGuide.Models;

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ExcursionId { get; set; }
    public Excursion? Excursion { get; set; }
    public string? Image { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime Timestamp { get; set; }
    public int? Slots { get; set; }
    public decimal? Total { get; set; }
    public string? TicketCategory { get; set; } // Добавлено
    public DateTime? DateTime { get; set; }     // Добавлено
    public int? Quantity { get; set; }          // Добавлено
}