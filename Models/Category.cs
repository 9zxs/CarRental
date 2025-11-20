using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty; // SUV, Sedan, Hatchback, Coupe, Convertible, Truck, Van

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? IconUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Car> Cars { get; set; } = new List<Car>();
    }
}

