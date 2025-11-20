using Microsoft.AspNetCore.Identity;

namespace CarRentalSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        public string? FirstName { get; set; }

        [PersonalData]
        public string? LastName { get; set; }

        [PersonalData]
        public DateTime? DateOfBirth { get; set; }

        [PersonalData]
        public string? Address { get; set; }

        [PersonalData]
        public string? City { get; set; }

        [PersonalData]
        public string? State { get; set; }

        [PersonalData]
        public string? ZipCode { get; set; }

        [PersonalData]
        public string? LicenseNumber { get; set; }

        [PersonalData]
        public string? ProfilePictureUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}

