using Microsoft.EntityFrameworkCore;
using ArduinoGymAccess.Models;

namespace ArduinoGymAccess.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<RfidToken> RfidTokens { get; set; }
        public DbSet<AccessLog> AccessLogs { get; set; }
        public DbSet<AccessDevice> AccessDevices { get; set; }
        public DbSet<DeviceLog> DeviceLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // RfidTokens
            modelBuilder.Entity<RfidToken>(entity =>
            {
                entity.ToTable("rfid_tokens");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.RfidCode).HasColumnName("rfid_code");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.HasIndex(e => e.RfidCode).IsUnique();
                entity.HasOne(d => d.User)
                    .WithMany(p => p.RfidTokens)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AccessLogs
            modelBuilder.Entity<AccessLog>(entity =>
            {
                entity.ToTable("access_logs");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.RfidTokenId).HasColumnName("rfid_token_id");
                entity.Property(e => e.AccessStatus).HasColumnName("access_status")
                    .HasConversion<string>();
                entity.Property(e => e.AccessTime).HasColumnName("access_time");
                entity.HasOne(d => d.RfidToken)
                    .WithMany(p => p.AccessLogs)
                    .HasForeignKey(d => d.RfidTokenId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AccessDevices
            modelBuilder.Entity<AccessDevice>(entity =>
            {
                entity.ToTable("access_devices");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DeviceName).HasColumnName("device_name");
                entity.Property(e => e.Location).HasColumnName("location");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // DeviceLogs
            modelBuilder.Entity<DeviceLog>(entity =>
            {
                entity.ToTable("device_logs");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DeviceId).HasColumnName("device_id");
                entity.Property(e => e.RfidTokenId).HasColumnName("rfid_token_id");
                entity.Property(e => e.LogTime).HasColumnName("log_time");
                entity.HasOne(d => d.Device)
                    .WithMany(p => p.DeviceLogs)
                    .HasForeignKey(d => d.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.RfidToken)
                    .WithMany(p => p.DeviceLogs)
                    .HasForeignKey(d => d.RfidTokenId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}