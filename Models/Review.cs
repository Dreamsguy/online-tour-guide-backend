namespace OnlineTourGuide.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int? ExcursionId { get; set; }
        public int? OrganizationId { get; set; }
        public int UserId { get; set; }
        public int? AttractionId { get; set; }
        public string Text { get; set; }
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public Excursion Excursion { get; set; }
        public Organization Organization { get; set; }
        public User User { get; set; }
        public Attraction  Attraction { get; set; }
    }
}