using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OnlineTourGuide.Models
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class UserPreference
    {
        public int Id { get; set; } // Первичный ключ
        public int UserId { get; set; }
        public string? PreferredCity { get; set; } // Nullable string
        public string? PreferredDirection { get; set; } // Nullable string
        [Column("PreferredAttractions")]
        public string? PreferredAttractionsJson { get; set; } // Nullable string для JSON

        [NotMapped]
        public List<int> PreferredAttractions
        {
            get => string.IsNullOrEmpty(PreferredAttractionsJson)
                ? new List<int>()
                : Newtonsoft.Json.JsonConvert.DeserializeObject<List<int>>(PreferredAttractionsJson) ?? new List<int>();
            set => PreferredAttractionsJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
        }
        public DateTime UpdatedAt { get; set; }
    }
}
