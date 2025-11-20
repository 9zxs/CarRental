using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly CarRentalDbContext _context;

        public AppointmentService(CarRentalDbContext context)
        {
            _context = context;
        }

        public async Task<Appointment?> GetAppointmentByIdAsync(int id)
        {
            return await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.Promotion)
                .Include(a => a.Subscription)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<IEnumerable<Appointment>> GetAllAppointmentsAsync()
        {
            return await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.Promotion)
                .Include(a => a.Subscription)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetAppointmentsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Appointments
                .Include(a => a.Car)
                .Where(a => a.StartDate <= endDate && a.EndDate >= startDate && a.Status != "Cancelled")
                .OrderBy(a => a.StartDate)
                .ToListAsync();
        }

        public async Task<Appointment> CreateAppointmentAsync(Appointment appointment)
        {
            // Calculate base price
            var car = await _context.Cars.FindAsync(appointment.CarId);
            if (car == null) throw new ArgumentException("Car not found");

            // Calculate days - handle partial days correctly
            var timeSpan = appointment.EndDate - appointment.StartDate;
            var totalHours = timeSpan.TotalHours;
            var days = totalHours <= 0 ? 1 : (int)Math.Ceiling(totalHours / 24.0);
            var basePrice = car.DailyRate * days;

            // Calculate discounts
            var discountAmount = 0m;

            if (appointment.SubscriptionId.HasValue)
            {
                var subscription = await _context.Subscriptions.FindAsync(appointment.SubscriptionId.Value);
                if (subscription != null && subscription.IsActive)
                {
                    discountAmount += basePrice * (subscription.DiscountPercentage / 100);
                }
            }

            if (appointment.PromotionId.HasValue)
            {
                var promotion = await _context.Promotions.FindAsync(appointment.PromotionId.Value);
                if (promotion != null && promotion.IsActive
                    && promotion.StartDate <= DateTime.UtcNow
                    && promotion.EndDate >= DateTime.UtcNow
                    && (promotion.MaxUses == null || promotion.CurrentUses < promotion.MaxUses))
                {
                    var promotionDiscount = basePrice * (promotion.DiscountPercentage / 100);
                    if (promotion.MaxDiscountAmount.HasValue && promotionDiscount > promotion.MaxDiscountAmount.Value)
                    {
                        promotionDiscount = promotion.MaxDiscountAmount.Value;
                    }
                    discountAmount += promotionDiscount;
                }
            }

            appointment.TotalPrice = basePrice - discountAmount;
            appointment.DiscountAmount = discountAmount > 0 ? discountAmount : null;

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            return appointment;
        }

        public async Task<Appointment> UpdateAppointmentAsync(Appointment appointment)
        {
            // Recalculate price if dates or discounts changed
            var car = await _context.Cars.FindAsync(appointment.CarId);
            if (car == null) throw new ArgumentException("Car not found");

            // Calculate days - handle partial days correctly
            var timeSpan = appointment.EndDate - appointment.StartDate;
            var totalHours = timeSpan.TotalHours;
            var days = totalHours <= 0 ? 1 : (int)Math.Ceiling(totalHours / 24.0);
            var basePrice = car.DailyRate * days;

            // Calculate discounts
            var discountAmount = 0m;

            if (appointment.SubscriptionId.HasValue)
            {
                var subscription = await _context.Subscriptions.FindAsync(appointment.SubscriptionId.Value);
                if (subscription != null && subscription.IsActive)
                {
                    discountAmount += basePrice * (subscription.DiscountPercentage / 100);
                }
            }

            if (appointment.PromotionId.HasValue)
            {
                var promotion = await _context.Promotions.FindAsync(appointment.PromotionId.Value);
                if (promotion != null && promotion.IsActive
                    && promotion.StartDate <= DateTime.UtcNow
                    && promotion.EndDate >= DateTime.UtcNow
                    && (promotion.MaxUses == null || promotion.CurrentUses < promotion.MaxUses))
                {
                    var promotionDiscount = basePrice * (promotion.DiscountPercentage / 100);
                    if (promotion.MaxDiscountAmount.HasValue && promotionDiscount > promotion.MaxDiscountAmount.Value)
                    {
                        promotionDiscount = promotion.MaxDiscountAmount.Value;
                    }
                    discountAmount += promotionDiscount;
                }
            }

            appointment.TotalPrice = basePrice - discountAmount;
            appointment.DiscountAmount = discountAmount > 0 ? discountAmount : null;
            appointment.UpdatedAt = DateTime.UtcNow;

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
            return appointment;
        }

        public async Task<bool> DeleteAppointmentAsync(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return false;

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsCarAvailableAsync(int carId, DateTime startDate, DateTime endDate, int? excludeAppointmentId = null)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car == null || !car.IsAvailable) return false;

            var conflictingAppointments = await _context.Appointments
                .Where(a => a.CarId == carId
                    && a.Id != excludeAppointmentId
                    && a.Status != "Cancelled"
                    && a.StartDate < endDate
                    && a.EndDate > startDate)
                .AnyAsync();

            return !conflictingAppointments;
        }

        public async Task<decimal> CalculatePriceAsync(int carId, DateTime startDate, DateTime endDate, int? promotionId = null, int? subscriptionId = null)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car == null) return 0;

            // Calculate days - handle partial days correctly
            var timeSpan = endDate - startDate;
            var totalHours = timeSpan.TotalHours;
            var days = totalHours <= 0 ? 1 : (int)Math.Ceiling(totalHours / 24.0);

            var basePrice = car.DailyRate * days;
            var discountAmount = 0m;

            // Apply subscription discount
            if (subscriptionId.HasValue)
            {
                var subscription = await _context.Subscriptions.FindAsync(subscriptionId.Value);
                if (subscription != null && subscription.IsActive)
                {
                    var subscriptionDiscount = basePrice * (subscription.DiscountPercentage / 100);
                    discountAmount += subscriptionDiscount;
                }
            }

            // Apply promotion discount
            if (promotionId.HasValue)
            {
                var promotion = await _context.Promotions.FindAsync(promotionId.Value);
                if (promotion != null && promotion.IsActive
                    && promotion.StartDate <= DateTime.UtcNow
                    && promotion.EndDate >= DateTime.UtcNow
                    && (promotion.MaxUses == null || promotion.CurrentUses < promotion.MaxUses))
                {
                    var promotionDiscount = basePrice * (promotion.DiscountPercentage / 100);
                    if (promotion.MaxDiscountAmount.HasValue && promotionDiscount > promotion.MaxDiscountAmount.Value)
                    {
                        promotionDiscount = promotion.MaxDiscountAmount.Value;
                    }
                    discountAmount += promotionDiscount;
                }
            }

            return basePrice - discountAmount;
        }

        public async Task<List<(DateTime Start, DateTime End)>> GetAvailableTimeSlotsAsync(int carId, DateTime startDate, DateTime endDate)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car == null || !car.IsAvailable)
            {
                return new List<(DateTime Start, DateTime End)>();
            }

            // Get all booked appointments for this car in the date range
            var bookedAppointments = await _context.Appointments
                .Where(a => a.CarId == carId
                    && a.Status != "Cancelled"
                    && a.EndDate > startDate
                    && a.StartDate < endDate)
                .OrderBy(a => a.StartDate)
                .ToListAsync();

            var availableSlots = new List<(DateTime Start, DateTime End)>();
            var currentStart = startDate;

            foreach (var appointment in bookedAppointments)
            {
                // If there's a gap before this appointment, it's available
                if (currentStart < appointment.StartDate)
                {
                    availableSlots.Add((currentStart, appointment.StartDate));
                }
                // Move to after this appointment
                currentStart = appointment.EndDate > currentStart ? appointment.EndDate : currentStart;
            }

            // If there's time left after the last appointment, add it
            if (currentStart < endDate)
            {
                availableSlots.Add((currentStart, endDate));
            }

            // If no appointments, the entire period is available
            if (!bookedAppointments.Any())
            {
                availableSlots.Add((startDate, endDate));
            }

            return availableSlots;
        }
    }
}

