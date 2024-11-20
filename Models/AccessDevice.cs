namespace ArduinoGymAccess.Models
{
    public class AccessDevice
    {
        public AccessDevice()
        {
            DeviceLogs = new HashSet<DeviceLog>();
        }

        public int Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string? Location { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual ICollection<DeviceLog> DeviceLogs { get; set; }
    }
}