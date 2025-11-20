using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Staff,Manager")]
    public class AnalyticsController : Controller
    {
        private readonly CarRentalDbContext _context;

        public AnalyticsController(CarRentalDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);

                var analytics = new
            {
                Revenue = new
                {
                    Today = await _context.Appointments
                        .Where(a => a.StartDate.Date == today && 
                                   (a.Status == "Confirmed" || a.Status == "Completed"))
                        .SumAsync(a => a.TotalPrice),
                    ThisMonth = await _context.Appointments
                        .Where(a => a.CreatedAt >= thisMonth && 
                                   (a.Status == "Confirmed" || a.Status == "Completed"))
                        .SumAsync(a => a.TotalPrice),
                    LastMonth = await _context.Appointments
                        .Where(a => a.CreatedAt >= lastMonth && a.CreatedAt < thisMonth && 
                                   (a.Status == "Confirmed" || a.Status == "Completed"))
                        .SumAsync(a => a.TotalPrice)
                },
                Bookings = new
                {
                    Today = await _context.Appointments.CountAsync(a => a.CreatedAt.Date == today),
                    ThisMonth = await _context.Appointments.CountAsync(a => a.CreatedAt >= thisMonth),
                    LastMonth = await _context.Appointments.CountAsync(a => a.CreatedAt >= lastMonth && a.CreatedAt < thisMonth)
                },
                TopCars = await _context.Appointments
                    .Where(a => a.CreatedAt >= thisMonth)
                    .GroupBy(a => new { a.Car!.Make, a.Car!.Model })
                    .Select(g => new
                    {
                        Car = g.Key.Make + " " + g.Key.Model,
                        Count = g.Count(),
                        Revenue = g.Sum(a => a.TotalPrice)
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync(),
                RevenueByDay = await _context.Appointments
                    .Where(a => a.CreatedAt >= thisMonth && 
                               (a.Status == "Confirmed" || a.Status == "Completed"))
                    .GroupBy(a => a.CreatedAt.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Revenue = g.Sum(a => a.TotalPrice),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync()
            };

                ViewBag.Analytics = analytics;
                return View();
            }
            catch (Exception)
            {
                ViewBag.Analytics = new
                {
                    Revenue = new { Today = 0, ThisMonth = 0, LastMonth = 0 },
                    Bookings = new { Today = 0, ThisMonth = 0, LastMonth = 0 },
                    TopCars = new List<object>(),
                    RevenueByDay = new List<object>()
                };
                ViewBag.ErrorMessage = "Unable to load analytics data. Please try again later.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRevenueData(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
                var end = endDate ?? DateTime.UtcNow;

                var data = await _context.Appointments
                .Where(a => a.CreatedAt >= start && a.CreatedAt <= end && 
                           (a.Status == "Confirmed" || a.Status == "Completed"))
                .GroupBy(a => a.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(a => a.TotalPrice),
                    Bookings = g.Count()
                })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                return Json(data);
            }
            catch (Exception)
            {
                return Json(new List<object>());
            }
        }
    }
}

