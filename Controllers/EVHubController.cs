using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using System.Collections.Generic;

namespace CarRentalSystem.Controllers
{
    public class EVHubController : Controller
    {
        private readonly CarRentalDbContext _context;

        public EVHubController(CarRentalDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var evCars = await _context.Cars
                    .Where(c => c.IsElectric && c.IsAvailable)
                    .OrderBy(c => c.DailyRate)
                    .ToListAsync();

                return View(evCars);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load electric vehicles. Please try again later.";
                return View(new List<Models.Car>());
            }
        }

        public async Task<IActionResult> Compare(int? id1, int? id2, int? id3)
        {
            try
            {
                var cars = new List<Models.Car>();

                if (id1.HasValue)
            {
                var car1 = await _context.Cars.FindAsync(id1.Value);
                if (car1 != null && car1.IsElectric) cars.Add(car1);
            }

            if (id2.HasValue)
            {
                var car2 = await _context.Cars.FindAsync(id2.Value);
                if (car2 != null && car2.IsElectric) cars.Add(car2);
            }

            if (id3.HasValue)
            {
                var car3 = await _context.Cars.FindAsync(id3.Value);
                    if (car3 != null && car3.IsElectric) cars.Add(car3);
                }

                return View(cars);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to compare vehicles. Please try again later.";
                return View(new List<Models.Car>());
            }
        }
    }
}

