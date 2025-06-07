using System;

namespace OnlineTourGuide.Models
{
    public class RoleRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string FullName { get; set; }
        public string Experience { get; set; }
        public string Residence { get; set; }
        public string Cities { get; set; }
        public string Ideas { get; set; }
        public string PhotosDescription { get; set; }
        public string OtherInfo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } // pending, approved, rejected
    }
}