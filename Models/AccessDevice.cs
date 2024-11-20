using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArduinoGymAccess.Models
{
    [Table("access_devices")]
    public class AccessDevice
    {
        public AccessDevice()
        {
            DeviceLogs = new HashSet<DeviceLog>();
        }

        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("device_name")]
        public string DeviceName { get; set; } = string.Empty;

        [Column("location")]
        public string? Location { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<DeviceLog> DeviceLogs { get; set; }
    }
}