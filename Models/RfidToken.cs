namespace ArduinoGymAccess.Models
{
    public class RfidToken
    {
        public RfidToken()
        {
            AccessLogs = new HashSet<AccessLog>();
            DeviceLogs = new HashSet<DeviceLog>();
        }

        public int Id { get; set; }
        public int UserId { get; set; }
        public string RfidCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<AccessLog> AccessLogs { get; set; }
        public virtual ICollection<DeviceLog> DeviceLogs { get; set; }
    }
}