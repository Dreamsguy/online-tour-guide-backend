using System.Text.Json.Serialization;

namespace OnlineTourGuide.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? PasswordHash { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Role Role { get; set; }

        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ContactInfo { get; set; }
        public string? Description { get; set; }
        public string? Preferences { get; set; }
        public string? FullName { get; set; } // Для гида
        public string? Experience { get; set; } // Для гида
        public string? Residence { get; set; } // Для гида
        public string? Cities { get; set; } // Города работы
        public string? Ideas { get; set; } // Интересные идеи
        public string? PhotosDescription { get; set; } // Описание фотографий
        public string? OtherInfo { get; set; } // Другая информация
        public int? OrganizationId { get; set; } // Добавлено
        public Organization Organization { get; set; } // Добавлено

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