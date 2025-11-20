using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public interface IAppointmentService
    {
        Task<Appointment?> GetAppointmentByIdAsync(int id);
        Task<IEnumerable<Appointment>> GetAllAppointmentsAsync();
        Task<IEnumerable<Appointment>> GetAppointmentsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<Appointment> CreateAppointmentAsync(Appointment appointment);
        Task<Appointment> UpdateAppointmentAsync(Appointment appointment);
        Task<bool> DeleteAppointmentAsync(int id);
        Task<bool> IsCarAvailableAsync(int carId, DateTime startDate, DateTime endDate, int? excludeAppointmentId = null);
        Task<decimal> CalculatePriceAsync(int carId, DateTime startDate, DateTime endDate, int? promotionId = null, int? subscriptionId = null);
        Task<List<(DateTime Start, DateTime End)>> GetAvailableTimeSlotsAsync(int carId, DateTime startDate, DateTime endDate);
    }
}

