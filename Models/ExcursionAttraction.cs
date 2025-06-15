namespace OnlineTourGuide.Models
{
    public class ExcursionAttraction
    {
        public int Id { get; set; } // Первичный ключ
        public int ExcursionId { get; set; }
        public Excursion Excursion { get; set; }
        public int AttractionId { get; set; }
        public Attraction Attraction { get; set; }
    }
}