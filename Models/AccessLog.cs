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
        [Column("is_granted")] // Assicurati che ci sia una colonna "is_granted" nella tabella
        public bool IsGranted { get; set; }

        [Column("denied_reason")]
        public string? DeniedReason { get; set; }

        [ForeignKey("RfidTokenId")]
        public virtual RfidToken RfidToken { get; set; } = null!;
    }

   public enum AccessStatus
    {
        GRANTED,    // Mappato dal database come 'Granted'
        DENIED,     // Mappato dal database come 'Denied'
        AUTHORIZED, // Nuovo valore
        UNAUTHORIZED // Nuovo valore
    }


}
