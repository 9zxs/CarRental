using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public class PromotionService : IPromotionService
    {
        private readonly CarRentalDbContext _context;

        public PromotionService(CarRentalDbContext context)
        {
            _context = context;
        }

        public async Task<Promotion?> GetPromotionByIdAsync(int id)
        {
            return await _context.Promotions.FindAsync(id);
        }

        public async Task<Promotion?> GetPromotionByCodeAsync(string code)
        {
            return await _context.Promotions
                .FirstOrDefaultAsync(p => p.Code.ToUpper() == code.ToUpper());
        }

        public async Task<IEnumerable<Promotion>> GetAllPromotionsAsync()
        {
            return await _context.Promotions
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Promotion>> GetActivePromotionsAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.Promotions
                .Where(p => p.IsActive
                    && p.StartDate <= now
                    && p.EndDate >= now
                    && (p.MaxUses == null || p.CurrentUses < p.MaxUses))
                .OrderByDescending(p => p.DiscountPercentage)
                .ToListAsync();
        }

        public async Task<Promotion> CreatePromotionAsync(Promotion promotion)
        {
            promotion.Code = promotion.Code.ToUpper();
            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();
            return promotion;
        }

        public async Task<Promotion> UpdatePromotionAsync(Promotion promotion)
        {
            promotion.Code = promotion.Code.ToUpper();
            promotion.UpdatedAt = DateTime.UtcNow;
            _context.Promotions.Update(promotion);
            await _context.SaveChangesAsync();
            return promotion;
        }

        public async Task<bool> DeletePromotionAsync(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null) return false;

            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidatePromotionCodeAsync(string code, bool isEV)
        {
            var promotion = await GetPromotionByCodeAsync(code);
            if (promotion == null) return false;

            var now = DateTime.UtcNow;
            if (!promotion.IsActive
                || promotion.StartDate > now
                || promotion.EndDate < now)
                return false;

            if (promotion.IsEVOnly && !isEV) return false;

            if (promotion.MaxUses.HasValue && promotion.CurrentUses >= promotion.MaxUses)
                return false;

            return true;
        }
    }
}

