using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using System.Collections.Generic;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class FavoritesController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FavoritesController(CarRentalDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // View user's favorites
        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                // Check if Favorites table exists, if not create it
                try
                {
                    var favorites = await _context.Favorites
                        .Include(f => f.Car)
                            .ThenInclude(c => c != null ? c.Category : null!)
                        .Where(f => f.UserId == user.Id && f.Car != null)
                        .OrderByDescending(f => f.CreatedAt)
                        .ToListAsync();

                    return View(favorites);
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name") && ex.Message.Contains("Favorites"))
                {
                    // Table doesn't exist - ensure it's created
                    await _context.Database.EnsureCreatedAsync();
                    
                    // Return empty list for now
                    return View(new List<Favorite>());
                }
            }
            catch (Exception)
            {
                // Return empty list on any error
                return View(new List<Favorite>());
            }
        }

        // Add to favorites
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int carId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Check if already favorited
            var existingFavorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == user.Id && f.CarId == carId);

            if (existingFavorite != null)
            {
                return Json(new { success = false, message = "Already in favorites" });
            }

            var favorite = new Favorite
            {
                UserId = user.Id,
                CarId = carId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Favorites.Add(favorite);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Added to favorites" });
        }

        // Remove from favorites
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int carId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == user.Id && f.CarId == carId);

            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Removed from favorites" });
            }

            return Json(new { success = false, message = "Favorite not found" });
        }

        // Check if car is favorited
        [HttpGet]
        public async Task<IActionResult> IsFavorited(int carId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { favorited = false });

            var favorited = await _context.Favorites
                .AnyAsync(f => f.UserId == user.Id && f.CarId == carId);

            return Json(new { favorited });
        }
    }
}

