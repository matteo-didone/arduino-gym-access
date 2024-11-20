namespace ArduinoGymAccess.Models
{
    public class DeviceLog
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public int? RfidTokenId { get; set; }
        public DateTime LogTime { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual AccessDevice Device { get; set; } = null!;
        public virtual RfidToken? RfidToken { get; set; }
    }
}