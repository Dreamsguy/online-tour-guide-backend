using System;
using System.ComponentModel.DataAnnotations;

namespace OnlineTourGuide.Models
{
    public class Promotion
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        public decimal? Discount { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public int? ExcursionId { get; set; }
        public Excursion Excursion { get; set; }

        public int? OrganizationId { get; set; }
        public Organization Organization { get; set; }
    }
}