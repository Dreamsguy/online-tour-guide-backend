using System.Text.Json.Serialization;

namespace OnlineTourGuide.Models
{
    public class User
    {
        public int Id { get; set; } // Явно добавляем Id
        public string Email { get; set; } // Явно добавляем Email
        public string PasswordHash { get; set; } // Для хранения хеша пароля

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Role Role { get; set; }

        public string? Status { get; set; }
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ContactInfo { get; set; }
        public string? Description { get; set; }
        public string? Preferences { get; set; }
        public string? FullName { get; set; }
        public string? Experience { get; set; }
        public string? Residence { get; set; }
        public string? Cities { get; set; }
        public string? Ideas { get; set; }
        public string? PhotosDescription { get; set; }
        public string? OtherInfo { get; set; }

        public int? OrganizationId { get; set; }
        public virtual Organization? Organization { get; set; }

        public List<Excursion> GuidedExcursions { get; set; }
        public List<Excursion> ManagedExcursions { get; set; }
        public List<Review> Reviews { get; set; }
        public List<Notification> Notifications { get; set; }
        public List<Booking> Bookings { get; set; }
    }

    public enum Role
    {
        User,
        Guide,
        Manager,
        Admin
    }
}