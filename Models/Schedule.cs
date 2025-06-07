namespace OnlineTourGuide.Models
{
    public class Schedule
    {
        public int Id { get; set; }
        public int GuideId { get; set; }
        public User Guide { get; set; }
        public int ExcursionId { get; set; }
        public Excursion Excursion { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } // "Planned", "Completed", "Cancelled"
    }
}