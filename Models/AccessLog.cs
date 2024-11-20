namespace ArduinoGymAccess.Models
{
    public enum AccessStatus
    {
        AUTHORIZED,
        UNAUTHORIZED
    }

    public class AccessLog
    {
        public int Id { get; set; }
        public int RfidTokenId { get; set; }
        public AccessStatus AccessStatus { get; set; }
        public DateTime AccessTime { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual RfidToken RfidToken { get; set; } = null!;
    }
}