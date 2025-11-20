using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRentalSystem.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public int CarId { get; set; }

        // User relationship
        public string? UserId { get; set; }

        [StringLength(100)]
        public string? CustomerName { get; set; }

        [EmailAddress]
        [StringLength(100)]
        public string? CustomerEmail { get; set; }

        [Phone]
        [StringLength(20)]
        public string? CustomerPhone { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Pickup Date & Time")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Return Date & Time")]
        [CustomValidation(typeof(Appointment), "ValidateEndDate")]
        public DateTime EndDate { get; set; }

        public static ValidationResult? ValidateEndDate(DateTime endDate, ValidationContext context)
        {
            var appointment = context.ObjectInstance as Appointment;
            if (appointment != null && endDate <= appointment.StartDate)
            {
                return new ValidationResult("Return date must be after pickup date.");
            }
            return ValidationResult.Success;
        }

        [StringLength(500)]
        public string? SpecialRequests { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled

        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal TotalPrice { get; set; }

        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal? DiscountAmount { get; set; }

        public int? PromotionId { get; set; }
        public int? SubscriptionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("CarId")]
        public virtual Car? Car { get; set; }

        [ForeignKey("PromotionId")]
        public virtual Promotion? Promotion { get; set; }

        [ForeignKey("SubscriptionId")]
        public virtual Subscription? Subscription { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}

