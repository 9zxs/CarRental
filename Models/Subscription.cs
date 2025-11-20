using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class Subscription
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal MonthlyPrice { get; set; }

        [Required]
        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int MaxRentalsPerMonth { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int MaxDaysPerRental { get; set; }

        public bool IncludesEVPriority { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}

