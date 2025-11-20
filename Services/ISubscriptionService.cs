using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public interface ISubscriptionService
    {
        Task<Subscription?> GetSubscriptionByIdAsync(int id);
        Task<IEnumerable<Subscription>> GetAllSubscriptionsAsync();
        Task<IEnumerable<Subscription>> GetActiveSubscriptionsAsync();
        Task<Subscription> CreateSubscriptionAsync(Subscription subscription);
        Task<Subscription> UpdateSubscriptionAsync(Subscription subscription);
        Task<bool> DeleteSubscriptionAsync(int id);
    }
}

