using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArduinoGymAccess.Models
{
    [Table("device_logs")]
    public class DeviceLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("device_id")]
        public int DeviceId { get; set; }

        [Column("rfid_token_id")]
        public int? RfidTokenId { get; set; }

        [Column("log_time")]
        public DateTime LogTime { get; set; } = DateTime.UtcNow;

        [ForeignKey("DeviceId")]
        public virtual AccessDevice Device { get; set; } = null!;

        [ForeignKey("RfidTokenId")]
        public virtual RfidToken? RfidToken { get; set; }
    }
}