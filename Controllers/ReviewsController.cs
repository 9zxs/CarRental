using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(CarRentalDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? carId)
        {
            try
            {
                var query = _context.Reviews
                    .Include(r => r.Car)
                    .Include(r => r.User)
                    .Where(r => r.IsApproved)
                    .AsQueryable();

            if (carId.HasValue)
            {
                query = query.Where(r => r.CarId == carId.Value);
            }

                var reviews = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                if (carId.HasValue)
                {
                    var car = await _context.Cars.FindAsync(carId.Value);
                    ViewBag.Car = car;
                    ViewBag.AverageRating = await _context.Reviews
                        .Where(r => r.CarId == carId.Value && r.IsApproved)
                        .AverageAsync(r => (double?)r.Rating) ?? 0;
                    ViewBag.TotalReviews = await _context.Reviews
                        .CountAsync(r => r.CarId == carId.Value && r.IsApproved);
                }

                return View(reviews);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load reviews. Please try again later.";
                return View(new List<Review>());
            }
        }

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create(int carId)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Check if user has completed a booking for this car
            var hasCompletedBooking = await _context.Appointments
                .AnyAsync(a => a.CarId == carId && 
                              a.UserId == user.Id && 
                              a.Status == "Completed");

            if (!hasCompletedBooking)
            {
                TempData["ErrorMessage"] = "You can only review cars you have completed bookings for.";
                return RedirectToAction("Details", "Cars", new { id = carId });
            }

            // Check if user already reviewed this car
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.CarId == carId && r.UserId == user.Id);

            if (existingReview != null)
            {
                return RedirectToAction(nameof(Edit), new { id = existingReview.Id });
            }

            ViewBag.Car = car;
            return View(new Review { CarId = carId });
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Review review)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Validate user has completed booking for this car
            var hasCompletedBooking = await _context.Appointments
                .AnyAsync(a => a.CarId == review.CarId && 
                              a.UserId == user.Id && 
                              a.Status == "Completed");

            if (!hasCompletedBooking)
            {
                TempData["ErrorMessage"] = "You can only review cars you have completed bookings for.";
                var car = await _context.Cars.FindAsync(review.CarId);
                ViewBag.Car = car;
                return View(review);
            }

            // Check if user already reviewed this car
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.CarId == review.CarId && r.UserId == user.Id);

            if (existingReview != null)
            {
                TempData["InfoMessage"] = "You have already reviewed this car. Redirecting to edit page.";
                return RedirectToAction(nameof(Edit), new { id = existingReview.Id });
            }

            if (ModelState.IsValid)
            {
                review.UserId = user.Id;
                review.CreatedAt = DateTime.UtcNow;
                review.IsApproved = false; // Require approval

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                // Create notification for staff
                try
                {
                    var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");
                    var managers = await _userManager.GetUsersInRoleAsync("Manager");
                    var allStaff = staffUsers.Union(managers).Distinct().ToList();
                    
                    var carForNotification = await _context.Cars.FindAsync(review.CarId);
                    foreach (var staff in allStaff)
                    {
                        var notification = new Notification
                        {
                            UserId = staff.Id,
                            Title = "New Review Pending Approval",
                            Message = $"A new review for {carForNotification?.DisplayName ?? "a car"} is waiting for approval.",
                            Type = "Info"
                        };
                        _context.Notifications.Add(notification);
                    }
                }
                catch
                {
                    // Ignore notification errors
                }
                
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Review submitted successfully! It will be reviewed before publication.";
                return RedirectToAction(nameof(Index), new { carId = review.CarId });
            }

            var carForView = await _context.Cars.FindAsync(review.CarId);
            ViewBag.Car = carForView;
            return View(review);
        }

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var review = await _context.Reviews
                .Include(r => r.Car)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (review == null)
            {
                return NotFound();
            }

            ViewBag.Car = review.Car;
            return View(review);
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Review review)
        {
            if (id != review.Id)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (existingReview == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                existingReview.Rating = review.Rating;
                existingReview.Comment = review.Comment;
                existingReview.UpdatedAt = DateTime.UtcNow;
                existingReview.IsApproved = false; // Require re-approval

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Review updated successfully!";
                return RedirectToAction(nameof(Index), new { carId = review.CarId });
            }

            var car = await _context.Cars.FindAsync(review.CarId);
            ViewBag.Car = car;
            return View(review);
        }

        [HttpPost]
        [Authorize(Roles = "Staff,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = true;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Customer,Staff,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && (r.UserId == user.Id || User.IsInRole("Staff") || User.IsInRole("Manager")));

            if (review == null)
            {
                return NotFound();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review deleted successfully!";
            return RedirectToAction(nameof(Index), new { carId = review.CarId });
        }
    }
}

