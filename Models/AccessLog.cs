using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArduinoGymAccess.Models
{
    [Table("access_logs")]
    public class AccessLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("rfid_token_id")]
        public int RfidTokenId { get; set; }

        [Required]
        [Column("access_status")]
        public AccessStatus AccessStatus { get; set; }

        [Column("access_time")]
        public DateTime AccessTime { get; set; } = DateTime.UtcNow;

        // Nuovi campi necessari per i controller
        [NotMapped]
        public bool IsGranted { get; set; }

        [NotMapped]
        public string? DeniedReason { get; set; }

        [ForeignKey("RfidTokenId")]
        public virtual RfidToken RfidToken { get; set; } = null!;
    }

    public enum AccessStatus
    {
        AUTHORIZED,
        UNAUTHORIZED
    }
}