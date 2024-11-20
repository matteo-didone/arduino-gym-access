using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArduinoGymAccess.Models
{
    [Table("users")]
    public class User
    {
        public User()
        {
            RfidTokens = new HashSet<RfidToken>();
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Nuovi campi necessari per i controller
        [NotMapped]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<RfidToken> RfidTokens { get; set; }
    }
}