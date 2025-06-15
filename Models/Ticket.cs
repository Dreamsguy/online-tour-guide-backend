namespace OnlineTourGuide.Models
{
    public class Ticket
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public int Total { get; set; }
        public string Type { get; set; }
        public decimal Price { get; set; }
        public int? Sold { get; set; } = 0;
        public string Currency { get; set; } = "USD";
    }
}
