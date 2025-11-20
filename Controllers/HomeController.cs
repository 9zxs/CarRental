using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Services;
using CarRentalSystem.Models;
using System.Collections.Generic;
using System.Linq;

namespace CarRentalSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly IPromotionService _promotionService;

        public HomeController(CarRentalDbContext context, IPromotionService promotionService)
        {
            _context = context;
            _promotionService = promotionService;
        }

        public async Task<IActionResult> Index(string? state = null, int? categoryId = null, string? searchQuery = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.Cars
                    .Include(c => c.Category)
                    .Where(c => c.IsAvailable)
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

                // Filter by available time slots if dates provided
                List<Car> cars;
                if (startDate.HasValue && endDate.HasValue && startDate.Value < endDate.Value)
                {
                    var allCars = await query.ToListAsync();
                    cars = new List<Car>();
                    
                    foreach (var car in allCars)
                    {
                        var conflictingAppointments = await _context.Appointments
                            .Where(a => a.CarId == car.Id
                                && a.Status != "Cancelled"
                                && a.StartDate < endDate.Value
                                && a.EndDate > startDate.Value)
                            .AnyAsync();
                        
                        if (!conflictingAppointments)
                        {
                            cars.Add(car);
                        }
                    }
                    
                    cars = cars.Take(12).ToList();
                }
                else
                {
                    cars = await query.Take(12).ToListAsync();
                }
                var evCars = await _context.Cars
                    .Include(c => c.Category)
                    .Where(c => c.IsElectric && c.IsAvailable)
                    .Take(6)
                    .ToListAsync();

                List<Promotion> promotions;
                try
                {
                    var promotionsEnumerable = await _promotionService.GetActivePromotionsAsync();
                    promotions = promotionsEnumerable?.ToList() ?? new List<Promotion>();
                }
                catch
                {
                    promotions = new List<Promotion>();
                }

                List<Category> categories;
                try
                {
                    categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
                {
                    await _context.Database.EnsureCreatedAsync();
                    categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
                }

                List<string> states;
                try
                {
                    states = await _context.Cars
                        .Where(c => c.IsAvailable)
                        .Select(c => c.State)
                        .Distinct()
                        .OrderBy(s => s)
                        .ToListAsync();
                }
                catch
                {
                    states = new List<string>();
                }

                ViewBag.Cars = cars;
                ViewBag.EVCars = evCars;
                ViewBag.Promotions = promotions;
                ViewBag.Categories = categories;
                ViewBag.States = states;
                ViewBag.SelectedState = state;
                ViewBag.SelectedCategoryId = categoryId;
                ViewBag.SearchQuery = searchQuery;

                return View();
            }
            catch (Exception)
            {
                // Log error and return empty view instead of crashing
                ViewBag.Cars = new List<Car>();
                ViewBag.EVCars = new List<Car>();
                ViewBag.Promotions = new List<Promotion>();
                ViewBag.Categories = new List<Category>();
                ViewBag.States = new List<string>();
                ViewBag.SelectedState = state;
                ViewBag.SelectedCategoryId = categoryId;
                ViewBag.SearchQuery = searchQuery;
                ViewBag.ErrorMessage = "Unable to load vehicle data. Please try again later.";
                return View();
            }
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Error()
        {
            var exceptionHandlerPathFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;
            
            ViewBag.ErrorMessage = exception?.Message ?? "An error occurred while processing your request.";
            ViewBag.ErrorDetails = exception?.ToString();
            ViewBag.RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            
            return View();
        }
    }
}

