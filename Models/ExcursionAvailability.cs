namespace OnlineTourGuide.Models
{
    public class ExcursionAvailability
    {
        public int Id { get; set; }
        public int ExcursionId { get; set; }
        public DateTime AvailableDateTime { get; set; }
        public string TicketCategory { get; set; }
        public int AvailableTickets { get; set; }

        public virtual Excursion Excursion { get; set; }
    }
}