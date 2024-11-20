namespace ArduinoGymAccess.Models
{
    public class User
    {
        public User()
        {
            RfidTokens = new HashSet<RfidToken>();
        }

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual ICollection<RfidToken> RfidTokens { get; set; }
    }
}