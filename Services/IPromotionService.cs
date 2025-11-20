using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public interface IPromotionService
    {
        Task<Promotion?> GetPromotionByIdAsync(int id);
        Task<Promotion?> GetPromotionByCodeAsync(string code);
        Task<IEnumerable<Promotion>> GetAllPromotionsAsync();
        Task<IEnumerable<Promotion>> GetActivePromotionsAsync();
        Task<Promotion> CreatePromotionAsync(Promotion promotion);
        Task<Promotion> UpdatePromotionAsync(Promotion promotion);
        Task<bool> DeletePromotionAsync(int id);
        Task<bool> ValidatePromotionCodeAsync(string code, bool isEV);
    }
}

