using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArduinoGymAccess.Models
{
    [Table("rfid_tokens")]
    public class RfidToken
    {
        public RfidToken()
        {
            AccessLogs = new HashSet<AccessLog>();
            DeviceLogs = new HashSet<DeviceLog>();
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("rfid_code")]
        public string RfidCode { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Nuovo campo necessario per i controller
        [NotMapped]
        public bool IsActive { get; set; } = true;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
        public virtual ICollection<AccessLog> AccessLogs { get; set; }
        public virtual ICollection<DeviceLog> DeviceLogs { get; set; }
    }
}