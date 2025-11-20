using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using CarRentalSystem.Models;

namespace CarRentalSystem.Data
{
    public class CarRentalDbContext : IdentityDbContext<ApplicationUser>
    {
        public CarRentalDbContext(DbContextOptions<CarRentalDbContext> options)
            : base(options)
        {
        }

        public DbSet<Car> Cars { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Favorite> Favorites { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Car configuration
            modelBuilder.Entity<Car>(entity =>
            {
                entity.HasIndex(e => e.LicensePlate).IsUnique();
                entity.Property(e => e.DailyRate).HasPrecision(18, 2);
            });

            // Appointment configuration
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasOne(a => a.Car)
                    .WithMany(c => c.Appointments)
                    .HasForeignKey(a => a.CarId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Promotion)
                    .WithMany(p => p.Appointments)
                    .HasForeignKey(a => a.PromotionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(a => a.Subscription)
                    .WithMany(s => s.Appointments)
                    .HasForeignKey(a => a.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
                entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            });

            // Subscription configuration
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.Property(e => e.MonthlyPrice).HasPrecision(18, 2);
                entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            });

            // Promotion configuration
            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(e => e.MaxDiscountAmount).HasPrecision(18, 2);
            });

            // Category configuration
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // Car-Category relationship
            modelBuilder.Entity<Car>(entity =>
            {
                entity.HasOne(c => c.Category)
                    .WithMany(cat => cat.Cars)
                    .HasForeignKey(c => c.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Appointment-User relationship
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasOne(a => a.User)
                    .WithMany(u => u.Appointments)
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Review configuration
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasOne(r => r.Car)
                    .WithMany()
                    .HasForeignKey(r => r.CarId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Payment configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasOne(p => p.Appointment)
                    .WithMany()
                    .HasForeignKey(p => p.AppointmentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Amount).HasPrecision(18, 2);
            });

            // Notification configuration
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Favorite configuration - ensure unique user-car pairs
            modelBuilder.Entity<Favorite>(entity =>
            {
                entity.HasIndex(f => new { f.UserId, f.CarId }).IsUnique();
                
                entity.HasOne(f => f.User)
                    .WithMany()
                    .HasForeignKey(f => f.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Car)
                    .WithMany()
                    .HasForeignKey(f => f.CarId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}

