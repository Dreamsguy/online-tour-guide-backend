using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OnlineTourGuide.Models
{
    public class UserAction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? ExcursionId { get; set; }

        [Required]
        public string ActionType { get; set; } // 'view' или 'book'

        [Required]
        public DateTime Timestamp { get; set; }
    }
}
