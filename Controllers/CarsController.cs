using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using System.Collections.Generic;

namespace CarRentalSystem.Controllers
{
    public class CarsController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAppointmentService _appointmentService;

        public CarsController(CarRentalDbContext context, UserManager<ApplicationUser> userManager, IAppointmentService appointmentService)
        {
            _context = context;
            _userManager = userManager;
            _appointmentService = appointmentService;
        }

        public async Task<IActionResult> Index(string fuelType = "All", string? state = null, int? categoryId = null, 
            string? searchQuery = null, decimal? minPrice = null, decimal? maxPrice = null, string? sortBy = "price_asc",
            DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
            var query = _context.Cars
                .Include(c => c.Category)
                .AsQueryable();

            // Enhanced search functionality - case-insensitive, searches multiple fields
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                searchQuery = searchQuery.Trim();
                var lowerSearchQuery = searchQuery.ToLower();
                query = query.Where(c => 
                    (c.Make != null && c.Make.ToLower().Contains(lowerSearchQuery)) || 
                    (c.Model != null && c.Model.ToLower().Contains(lowerSearchQuery)) || 
                    (c.Description != null && c.Description.ToLower().Contains(lowerSearchQuery)) ||
                    (c.DisplayName != null && c.DisplayName.ToLower().Contains(lowerSearchQuery)) ||
                    (c.City != null && c.City.ToLower().Contains(lowerSearchQuery)) ||
                    (c.State != null && c.State.ToLower().Contains(lowerSearchQuery)) ||
                    (c.FuelType != null && c.FuelType.ToLower().Contains(lowerSearchQuery)) ||
                    (c.Category != null && c.Category.Name != null && c.Category.Name.ToLower().Contains(lowerSearchQuery)) ||
                    (c.Year.ToString().Contains(searchQuery)));
            }

            // Filter by fuel type
            if (fuelType == "Electric")
            {
                query = query.Where(c => c.IsElectric);
            }
            else if (fuelType == "Gas")
            {
                query = query.Where(c => !c.IsElectric);
            }

            // Filter by state
            if (!string.IsNullOrEmpty(state))
            {
                query = query.Where(c => c.State == state);
            }

            // Filter by category
            if (categoryId.HasValue)
            {
                query = query.Where(c => c.CategoryId == categoryId);
            }

            // Filter by price range
            if (minPrice.HasValue)
            {
                query = query.Where(c => c.DailyRate >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(c => c.DailyRate <= maxPrice.Value);
            }

            // Apply availability filter
            query = query.Where(c => c.IsAvailable);

            // Filter by available time slots if dates provided
            if (startDate.HasValue && endDate.HasValue && startDate.Value < endDate.Value)
            {
                var carsForAvailabilityCheck = await query.ToListAsync();
                var availableCarIds = new List<int>();
                
                foreach (var car in carsForAvailabilityCheck)
                {
                    var conflictingAppointments = await _context.Appointments
                        .Where(a => a.CarId == car.Id
                            && a.Status != "Cancelled"
                            && a.StartDate < endDate.Value
                            && a.EndDate > startDate.Value)
                        .AnyAsync();
                    
                    if (!conflictingAppointments)
                    {
                        availableCarIds.Add(car.Id);
                    }
                }
                
                query = query.Where(c => availableCarIds.Contains(c.Id));
            }

            // Sort functionality
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(c => c.DailyRate),
                "price_desc" => query.OrderByDescending(c => c.DailyRate),
                "name_asc" => query.OrderBy(c => c.Make).ThenBy(c => c.Model),
                "name_desc" => query.OrderByDescending(c => c.Make).ThenByDescending(c => c.Model),
                "year_desc" => query.OrderByDescending(c => c.Year),
                "rating_desc" => query.OrderByDescending(c => c.Id), // Will be sorted by ratings after loading
                _ => query.OrderBy(c => c.DailyRate)
            };

            var cars = await query.ToListAsync();

            // Calculate average ratings for each car
            var averageRatings = new Dictionary<int, double>();
            foreach (var car in cars)
            {
                var avgRating = await _context.Reviews
                    .Where(r => r.CarId == car.Id && r.IsApproved)
                    .AverageAsync(r => (double?)r.Rating) ?? 0;
                averageRatings[car.Id] = avgRating;
            }

            // Sort by rating if needed
            if (sortBy == "rating_desc")
            {
                cars = cars.OrderByDescending(c => averageRatings.GetValueOrDefault(c.Id, 0)).ToList();
            }

            List<Category> categories;
            try
            {
                categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
            {
                // Table doesn't exist - ensure it's created
                await _context.Database.EnsureCreatedAsync();
                categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            }
            var states = await _context.Cars
                .Where(c => c.IsAvailable)
                .Select(c => c.State)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            // Get price range for filter
            var allCars = await _context.Cars.Where(c => c.IsAvailable).ToListAsync();
            var minDailyRate = allCars.Any() ? allCars.Min(c => c.DailyRate) : 0;
            var maxDailyRate = allCars.Any() ? allCars.Max(c => c.DailyRate) : 1000;

            ViewBag.FuelType = fuelType;
            ViewBag.Categories = categories;
            ViewBag.States = states;
            ViewBag.SelectedState = state;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SearchQuery = searchQuery;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.MinDailyRate = minDailyRate;
            ViewBag.MaxDailyRate = maxDailyRate;
            ViewBag.SortBy = sortBy;
            ViewBag.AverageRatings = averageRatings;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            
            // Get favorited car IDs for current user
            var favoritedCarIds = new HashSet<int>();
            if (User.Identity!.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var favoriteIds = await _context.Favorites
                        .Where(f => f.UserId == user.Id && cars.Select(c => c.Id).Contains(f.CarId))
                        .Select(f => f.CarId)
                        .ToListAsync();
                    favoritedCarIds = favoriteIds.ToHashSet();
                }
            }
                ViewBag.FavoritedCarIds = favoritedCarIds;
                
                return View(cars);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load vehicles. Please try again later.";
                ViewBag.Categories = new List<Category>();
                ViewBag.States = new List<string>();
                ViewBag.FuelTypes = new List<string>();
                ViewBag.FavoritedCarIds = new HashSet<int>();
                return View(new List<Car>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var car = await _context.Cars
                    .Include(c => c.Category)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (car == null)
                {
                    return NotFound();
                }

                // Get reviews for this car
                List<Review> reviews;
                double averageRating = 0;
                int totalReviews = 0;
                try
                {
                    reviews = await _context.Reviews
                        .Include(r => r.User)
                        .Where(r => r.CarId == id && r.IsApproved)
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                    averageRating = await _context.Reviews
                        .Where(r => r.CarId == id && r.IsApproved)
                        .AverageAsync(r => (double?)r.Rating) ?? 0;

                    totalReviews = await _context.Reviews
                        .CountAsync(r => r.CarId == id && r.IsApproved);
                }
                catch
                {
                    reviews = new List<Review>();
                }

                ViewBag.Reviews = reviews;
                ViewBag.AverageRating = averageRating;
                ViewBag.TotalReviews = totalReviews;

                // Check if current user has completed booking for this car
                bool canReview = false;
                bool isFavorited = false;
                if (User.Identity!.IsAuthenticated)
                {
                    try
                    {
                        var user = await _userManager.GetUserAsync(User);
                        if (user != null)
                        {
                            canReview = await _context.Appointments
                                .AnyAsync(a => a.CarId == id && 
                                             a.UserId == user.Id && 
                                             a.Status == "Completed");
                            
                            // Check if favorited
                            isFavorited = await _context.Favorites
                                .AnyAsync(f => f.UserId == user.Id && f.CarId == id);
                        }
                    }
                    catch
                    {
                        // Ignore errors for user-specific checks
                    }
                }
                ViewBag.CanReview = canReview;
                ViewBag.IsFavorited = isFavorited;
                
                // Get available time slots for the next 30 days
                var defaultStartDate = DateTime.UtcNow.Date;
                var defaultEndDate = defaultStartDate.AddDays(30);
                var availableSlots = await _appointmentService.GetAvailableTimeSlotsAsync(car.Id, defaultStartDate, defaultEndDate);
                ViewBag.AvailableSlots = availableSlots;
                ViewBag.DefaultStartDate = defaultStartDate;
                ViewBag.DefaultEndDate = defaultEndDate;

                return View(car);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load vehicle details. Please try again later.";
                return View(new Car());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCarDetails(int id)
        {
            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.Id == id);

            if (car == null)
            {
                return Json(new { error = "Car not found" });
            }

            return Json(new
            {
                id = car.Id,
                make = car.Make,
                model = car.Model,
                year = car.Year,
                dailyRate = car.DailyRate,
                isElectric = car.IsElectric,
                range = car.Range,
                batteryCapacity = car.BatteryCapacity,
                city = car.City,
                state = car.State,
                imageUrl = car.ImageUrl,
                displayName = car.DisplayName
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecommendations(string? userId = null)
        {
            var cars = await _context.Cars
                .Include(c => c.Category)
                .Where(c => c.IsAvailable)
                .ToListAsync();

            var recommendations = cars.Select(car => new
            {
                id = car.Id,
                displayName = car.DisplayName,
                categoryName = car.Category?.Name ?? "Sedan",
                dailyRate = car.DailyRate,
                city = car.City,
                state = car.State,
                imageUrl = car.ImageUrl,
                isElectric = car.IsElectric,
                categoryId = car.CategoryId,
                recommendationScore = new Random().Next(75, 99) // Simulated score
            })
            .OrderByDescending(c => c.recommendationScore)
            .Take(6)
            .ToList();

            return Json(recommendations);
        }
    }
}

