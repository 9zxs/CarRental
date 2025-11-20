using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class Car
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Make { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Model { get; set; } = string.Empty;

        [Required]
        [Range(1900, 2100)]
        public int Year { get; set; }

        [Required]
        [StringLength(20)]
        public string LicensePlate { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Color { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal DailyRate { get; set; }

        [Required]
        [StringLength(50)]
        public string FuelType { get; set; } = string.Empty; // Gas, Electric, Hybrid

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? ImageUrl { get; set; }

        public bool IsElectric { get; set; }
        public bool IsAvailable { get; set; } = true;

        // Location/State
        [Required]
        [StringLength(50)]
        public string State { get; set; } = "Kuala Lumpur";

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(200)]
        public string? LocationAddress { get; set; }

        // Category
        public int? CategoryId { get; set; }

        // EV-specific properties
        public int? BatteryCapacity { get; set; } // kWh
        public int? Range { get; set; } // miles
        public int? ChargingTime { get; set; } // minutes for 80% charge

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public virtual Category? Category { get; set; }

        // Helper property for display
        public string DisplayName => $"{Year} {Make} {Model} - {LicensePlate}";
    }
}

