using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Staff,Manager")]
    public class StaffController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StaffController> _logger;

        public StaffController(CarRentalDbContext context, UserManager<ApplicationUser> userManager, ILogger<StaffController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // Helper method to safely get categories
        private async Task<List<Category>> GetCategoriesAsync()
        {
            try
            {
                return await _context.Categories.Where(c => c.IsActive).ToListAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                try
                {
                    await _context.Database.EnsureCreatedAsync();
                    return await _context.Categories.Where(c => c.IsActive).ToListAsync();
                }
                catch
                {
                    return new List<Category>();
                }
            }
        }

        public IActionResult Dashboard()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var thisMonth = DateTime.UtcNow.Month;
                var thisYear = DateTime.UtcNow.Year;
                
                // Exclude cancelled appointments from stats
                var activeAppointments = _context.Appointments?.Where(a => a.Status != "Cancelled") ?? Enumerable.Empty<Appointment>().AsQueryable();
            
            var stats = new
            {
                TotalAppointments = activeAppointments.Count(),
                TodayAppointments = activeAppointments.Count(a => a.StartDate.Date == today),
                PendingAppointments = activeAppointments.Count(a => a.Status == "Pending"),
                ConfirmedAppointments = activeAppointments.Count(a => a.Status == "Confirmed"),
                CompletedAppointments = activeAppointments.Count(a => a.Status == "Completed"),
                CancelledAppointments = _context.Appointments?.Count(a => a.Status == "Cancelled") ?? 0,
                TotalRevenue = activeAppointments.Where(a => a.Status == "Confirmed" || a.Status == "Completed")
                    .Sum(a => (decimal?)a.TotalPrice) ?? 0,
                ThisMonthRevenue = activeAppointments
                    .Where(a => (a.Status == "Confirmed" || a.Status == "Completed") && 
                               a.CreatedAt.Month == thisMonth && 
                               a.CreatedAt.Year == thisYear)
                    .Sum(a => (decimal?)a.TotalPrice) ?? 0,
                AvailableCars = _context.Cars?.Count(c => c.IsAvailable) ?? 0,
                TotalCars = _context.Cars?.Count() ?? 0,
                TotalCustomers = _context.Users.Count(u => !_userManager.IsInRoleAsync(u, "Staff").Result && !_userManager.IsInRoleAsync(u, "Manager").Result),
                RecentBookings = activeAppointments.OrderByDescending(a => a.CreatedAt).Take(5).ToList()
            };

                ViewBag.Stats = stats;
                return View();
            }
            catch (Exception)
            {
                ViewBag.Stats = new
                {
                    TotalAppointments = 0,
                    TodayAppointments = 0,
                    PendingAppointments = 0,
                    ConfirmedAppointments = 0,
                    CompletedAppointments = 0,
                    CancelledAppointments = 0,
                    TotalRevenue = 0,
                    ThisMonthRevenue = 0,
                    AvailableCars = 0,
                    TotalCars = 0,
                    TotalCustomers = 0,
                    RecentBookings = new List<Appointment>()
                };
                ViewBag.ErrorMessage = "Unable to load dashboard statistics. Please try again later.";
                return View();
            }
        }

        public async Task<IActionResult> Orders(string status = "All", string searchTerm = "")
        {
            try
            {
                var query = _context.Appointments
                    .Include(a => a.Car)
                    .Include(a => a.User)
                    .Include(a => a.Promotion)
                    .AsQueryable();

                // Exclude cancelled orders by default unless explicitly viewing cancelled
                if (status == "All")
                {
                    query = query.Where(a => a.Status != "Cancelled");
                }

                if (status != "All")
                {
                    query = query.Where(a => a.Status == status);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(a =>
                        (a.CustomerName != null && a.CustomerName.Contains(searchTerm)) ||
                        (a.CustomerEmail != null && a.CustomerEmail.Contains(searchTerm)) ||
                        (a.Car != null && a.Car.Make.Contains(searchTerm)) ||
                        (a.Car != null && a.Car.Model.Contains(searchTerm)));
                }

                var orders = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
                
                // Get payment status for each order
                var orderIds = orders.Select(o => o.Id).ToList();
                var payments = new List<Payment>();
                if (orderIds.Any())
                {
                    payments = await _context.Payments
                        .Where(p => orderIds.Contains(p.AppointmentId))
                        .ToListAsync();
                }
                
                var paymentDict = payments.ToDictionary(p => p.AppointmentId, p => p);
                ViewBag.Payments = paymentDict;
                
                ViewBag.Status = status;
                ViewBag.SearchTerm = searchTerm;
                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading orders");
                ViewBag.ErrorMessage = "Unable to load orders. Please try again later.";
                ViewBag.Status = status;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.Payments = new Dictionary<int, Payment>();
                return View(new List<Appointment>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (appointment == null)
            {
                return NotFound();
            }

            var oldStatus = appointment.Status;
            appointment.Status = status;
            appointment.UpdatedAt = DateTime.UtcNow;
            
            // If cancelled, update car availability and handle payment refund
            if (status == "Cancelled" && oldStatus != "Cancelled")
            {
                // Make car available again if it was reserved
                if (appointment.CarId > 0)
                {
                    var car = await _context.Cars.FindAsync(appointment.CarId);
                    if (car != null && !car.IsAvailable)
                    {
                        // Check if there are no other active bookings for this car
                        var hasActiveBookings = await _context.Appointments
                            .AnyAsync(a => a.CarId == appointment.CarId && 
                                         a.Id != appointment.Id && 
                                         a.Status != "Cancelled" && 
                                         a.Status != "Completed" &&
                                         ((a.StartDate <= appointment.EndDate && a.EndDate >= appointment.StartDate)));
                        if (!hasActiveBookings)
                        {
                            car.IsAvailable = true;
                        }
                    }
                }

                // Handle payment refund if payment was completed
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.AppointmentId == id && p.Status == "Completed");
                if (payment != null)
                {
                    // Create refund record (in real app, process actual refund)
                    payment.Status = "Refunded";
                    payment.UpdatedAt = DateTime.UtcNow;
                    
                    var refundNotification = new Notification
                    {
                        UserId = appointment.UserId!,
                        Title = "Refund Processed",
                        Message = $"A refund of RM {payment.Amount:N2} has been processed for your cancelled booking.",
                        Type = "Success"
                    };
                    _context.Notifications.Add(refundNotification);
                }
            }
            
            // Create notification for status change
            if (appointment.UserId != null)
            {
                var notification = new Notification
                {
                    UserId = appointment.UserId,
                    Title = $"Booking {status}",
                    Message = status == "Completed" 
                        ? $"Your booking for {appointment.Car?.DisplayName} has been completed. Share your experience by leaving a review!" 
                        : $"Your booking for {appointment.Car?.DisplayName} has been {status.ToLower()}.",
                    Type = status == "Confirmed" || status == "Completed" ? "Success" : status == "Cancelled" ? "Danger" : "Info"
                };
                _context.Notifications.Add(notification);
            }
            
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Order #{id} status updated to {status} successfully!";
            return RedirectToAction(nameof(Orders));
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .Include(a => a.Promotion)
                .Include(a => a.Subscription)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            // Get payment for this order
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.AppointmentId == id);
            
            ViewBag.Payment = payment;
            ViewBag.HasPayment = payment != null;
            ViewBag.IsPaymentCompleted = payment != null && payment.Status == "Completed";

            return View(appointment);
        }

        public async Task<IActionResult> Reports()
        {
            var startDate = DateTime.UtcNow.AddMonths(-1);
            var endDate = DateTime.UtcNow;

            var report = new
            {
                TotalBookings = await _context.Appointments
                    .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                    .CountAsync(),
                TotalRevenue = await _context.Appointments
                    .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate && 
                                (a.Status == "Confirmed" || a.Status == "Completed"))
                    .SumAsync(a => a.TotalPrice),
                RevenueByStatus = await _context.Appointments
                    .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Revenue = g.Sum(a => a.TotalPrice) })
                    .ToListAsync(),
                TopCars = await _context.Appointments
                    .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                    .GroupBy(a => a.Car!.Make + " " + a.Car!.Model)
                    .Select(g => new { Car = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync(),
                BookingsByDay = await _context.Appointments
                    .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                    .GroupBy(a => a.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToListAsync()
            };

            ViewBag.Report = report;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;

            var report = new
            {
                TotalBookings = await _context.Appointments
                    .Where(a => a.CreatedAt >= start && a.CreatedAt <= end)
                    .CountAsync(),
                TotalRevenue = await _context.Appointments
                    .Where(a => a.CreatedAt >= start && a.CreatedAt <= end && 
                                (a.Status == "Confirmed" || a.Status == "Completed"))
                    .SumAsync(a => a.TotalPrice),
                RevenueByStatus = await _context.Appointments
                    .Where(a => a.CreatedAt >= start && a.CreatedAt <= end)
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Revenue = g.Sum(a => a.TotalPrice) })
                    .ToListAsync(),
                TopCars = await _context.Appointments
                    .Where(a => a.CreatedAt >= start && a.CreatedAt <= end)
                    .GroupBy(a => a.Car!.Make + " " + a.Car!.Model)
                    .Select(g => new { Car = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync(),
                BookingsByDay = await _context.Appointments
                    .Where(a => a.CreatedAt >= start && a.CreatedAt <= end)
                    .GroupBy(a => a.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToListAsync()
            };

            ViewBag.Report = report;
            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            return View();
        }

        public async Task<IActionResult> UserManagement(string searchTerm = "", string statusFilter = "All", string roleFilter = "All")
        {
            var query = _userManager.Users.AsQueryable();

            // Staff and Manager can both see all users and filter by role
            if (roleFilter != "All" && !string.IsNullOrEmpty(roleFilter))
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleFilter);
                if (role != null)
                {
                    var userIdsInRole = await _context.UserRoles
                        .Where(ur => ur.RoleId == role.Id)
                        .Select(ur => ur.UserId)
                        .ToListAsync();
                    query = query.Where(u => userIdsInRole.Contains(u.Id));
                }
            }

            // Enhanced search - case-insensitive and searches more fields
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(u =>
                    (u.Email != null && u.Email.ToLower().Contains(lowerSearchTerm)) ||
                    (u.FirstName != null && u.FirstName.ToLower().Contains(lowerSearchTerm)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(lowerSearchTerm)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(searchTerm)) ||
                    ((u.FirstName != null ? u.FirstName : "") + " " + (u.LastName != null ? u.LastName : "")).ToLower().Contains(lowerSearchTerm) ||
                    (u.City != null && u.City.ToLower().Contains(lowerSearchTerm)) ||
                    (u.State != null && u.State.ToLower().Contains(lowerSearchTerm)));
            }

            // Filter by status
            if (statusFilter == "Active")
            {
                query = query.Where(u => u.IsActive);
            }
            else if (statusFilter == "Inactive")
            {
                query = query.Where(u => !u.IsActive);
            }

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

            var userRoles = new Dictionary<string, IList<string>>();
            var userStats = new Dictionary<string, object>();
            foreach (var user in users)
            {
                userRoles[user.Id] = await _userManager.GetRolesAsync(user);
                
                // Get user statistics
                var appointmentCount = await _context.Appointments.CountAsync(a => a.UserId == user.Id);
                var totalSpent = await _context.Appointments
                    .Where(a => a.UserId == user.Id && (a.Status == "Confirmed" || a.Status == "Completed"))
                    .SumAsync(a => (decimal?)a.TotalPrice) ?? 0;
                
                userStats[user.Id] = new
                {
                    AppointmentCount = appointmentCount,
                    TotalSpent = totalSpent,
                    LastBookingDate = await _context.Appointments
                        .Where(a => a.UserId == user.Id)
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => (DateTime?)a.CreatedAt)
                        .FirstOrDefaultAsync()
                };
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.UserStats = userStats;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.RoleFilter = roleFilter;
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCustomerStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return NotFound();
            
            // Prevent modifying your own account
            if (user.Id == currentUser.Id)
            {
                TempData["ErrorMessage"] = "You cannot modify your own account status.";
                return RedirectToAction(nameof(UserManagement));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            var isCurrentUserManager = currentUserRoles.Contains("Manager");

            // Staff cannot modify Manager accounts, but can modify Staff and Customer accounts
            if (roles.Contains("Manager") && !isCurrentUserManager)
            {
                TempData["ErrorMessage"] = "You don't have permission to modify Manager account status.";
                return RedirectToAction(nameof(UserManagement));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            var userRole = roles.FirstOrDefault() ?? "User";
            TempData["SuccessMessage"] = $"{userRole} account {(user.IsActive ? "activated" : "deactivated")} successfully.";
            return RedirectToAction(nameof(UserManagement));
        }

        public async Task<IActionResult> UserDetails(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            
            // Get all customer data
            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.Promotion)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var payments = await _context.Payments
                .Include(p => p.Appointment)
                    .ThenInclude(a => a!.Car)
                .Where(p => p.Appointment!.UserId == userId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var reviews = await _context.Reviews
                .Include(r => r.Car)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Calculate statistics
            var totalSpent = appointments
                .Where(a => a.Status == "Confirmed" || a.Status == "Completed")
                .Sum(a => a.TotalPrice);

            var totalBookings = appointments.Count;
            var completedBookings = appointments.Count(a => a.Status == "Completed");
            var pendingBookings = appointments.Count(a => a.Status == "Pending");
            var cancelledBookings = appointments.Count(a => a.Status == "Cancelled");

            ViewBag.Roles = roles;
            ViewBag.Appointments = appointments;
            ViewBag.Payments = payments;
            ViewBag.Reviews = reviews;
            ViewBag.TotalSpent = totalSpent;
            ViewBag.TotalBookings = totalBookings;
            ViewBag.CompletedBookings = completedBookings;
            ViewBag.PendingBookings = pendingBookings;
            ViewBag.CancelledBookings = cancelledBookings;
            ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
            
            return View(user);
        }

        // Vehicle Management (Staff and Manager can manage vehicles)
        public async Task<IActionResult> VehicleManagement(string searchTerm = "", string? status = null, string? state = null)
        {
            var query = _context.Cars
                .Include(c => c.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c =>
                    c.Make.Contains(searchTerm) ||
                    c.Model.Contains(searchTerm) ||
                    c.LicensePlate.Contains(searchTerm));
            }

            if (status == "Available")
            {
                query = query.Where(c => c.IsAvailable);
            }
            else if (status == "Unavailable")
            {
                query = query.Where(c => !c.IsAvailable);
            }

            if (!string.IsNullOrEmpty(state))
            {
                query = query.Where(c => c.State == state);
            }

            var vehicles = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

            // Get statistics
            var stats = new
            {
                TotalVehicles = await _context.Cars.CountAsync(),
                AvailableVehicles = await _context.Cars.CountAsync(c => c.IsAvailable),
                UnavailableVehicles = await _context.Cars.CountAsync(c => !c.IsAvailable),
                ElectricVehicles = await _context.Cars.CountAsync(c => c.IsElectric),
                GasVehicles = await _context.Cars.CountAsync(c => !c.IsElectric)
            };

            ViewBag.Stats = stats;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            ViewBag.State = state;
            ViewBag.States = await _context.Cars.Select(c => c.State).Distinct().OrderBy(s => s).ToListAsync();
            List<Category> categories;
            try
            {
                categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                try
                {
                    await _context.Database.EnsureCreatedAsync();
                    categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
                }
                catch
                {
                    categories = new List<Category>();
                }
            }
            ViewBag.Categories = categories;

            return View(vehicles);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleVehicleStatus(int carId)
        {
            var car = await _context.Cars.FindAsync(carId);
            if (car == null)
            {
                return NotFound();
            }

            car.IsAvailable = !car.IsAvailable;
            car.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Vehicle {car.DisplayName} has been {(car.IsAvailable ? "made available" : "made unavailable")}.";
            return RedirectToAction(nameof(VehicleManagement));
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await GetCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Car car, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                car.CreatedAt = DateTime.UtcNow;
                car.IsAvailable = true;
                
                // Handle image upload if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        // Save car first to get ID
                        _context.Cars.Add(car);
                        await _context.SaveChangesAsync();
                        
                        // Upload and resize image
                        var fileUploadService = HttpContext.RequestServices.GetRequiredService<IFileUploadService>();
                        var imageUrl = await fileUploadService.UploadVehicleImageAsync(imageFile, car.Id);
                        car.ImageUrl = imageUrl;
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading vehicle image");
                        ModelState.AddModelError("ImageFile", "Failed to upload image: " + ex.Message);
                        var categories = await GetCategoriesAsync();
                        ViewBag.Categories = new SelectList(categories, "Id", "Name", car.CategoryId);
                        return View(car);
                    }
                }
                else
                {
                    // No image file, just save the car
                    _context.Cars.Add(car);
                    await _context.SaveChangesAsync();
                }
                
                TempData["SuccessMessage"] = $"Vehicle {car.DisplayName} has been added successfully!";
                return RedirectToAction(nameof(VehicleManagement));
            }
            
            var categories2 = await GetCategoriesAsync();
            ViewBag.Categories = new SelectList(categories2, "Id", "Name", car.CategoryId);
            return View(car);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var car = await _context.Cars.FindAsync(id);
            if (car == null)
            {
                return NotFound();
            }

            var categories = await GetCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", car.CategoryId);
            return View(car);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Car car)
        {
            if (id != car.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCar = await _context.Cars.FindAsync(id);
                    if (existingCar == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingCar.Make = car.Make;
                    existingCar.Model = car.Model;
                    existingCar.Year = car.Year;
                    existingCar.LicensePlate = car.LicensePlate;
                    existingCar.Color = car.Color;
                    existingCar.DailyRate = car.DailyRate;
                    existingCar.FuelType = car.FuelType;
                    existingCar.IsElectric = car.IsElectric;
                    existingCar.State = car.State;
                    existingCar.City = car.City;
                    existingCar.CategoryId = car.CategoryId;
                    existingCar.Description = car.Description;
                    existingCar.ImageUrl = car.ImageUrl;
                    existingCar.LocationAddress = car.LocationAddress;
                    existingCar.BatteryCapacity = car.BatteryCapacity;
                    existingCar.Range = car.Range;
                    existingCar.ChargingTime = car.ChargingTime;
                    existingCar.UpdatedAt = DateTime.UtcNow;

                    _context.Cars.Update(existingCar);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Vehicle {existingCar.DisplayName} has been updated successfully!";
                    return RedirectToAction(nameof(VehicleManagement));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Cars.AnyAsync(c => c.Id == id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }

            var categories = await GetCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", car.CategoryId);
            return View(car);
        }

        // Review Management (Staff can approve/reject reviews)
        public async Task<IActionResult> ReviewManagement(string status = "All")
        {
            var query = _context.Reviews
                .Include(r => r.Car)
                .Include(r => r.User)
                .AsQueryable();

            if (status == "Pending")
            {
                query = query.Where(r => !r.IsApproved);
            }
            else if (status == "Approved")
            {
                query = query.Where(r => r.IsApproved);
            }

            var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            var stats = new
            {
                TotalReviews = await _context.Reviews.CountAsync(),
                PendingReviews = await _context.Reviews.CountAsync(r => !r.IsApproved),
                ApprovedReviews = await _context.Reviews.CountAsync(r => r.IsApproved),
                AverageRating = await _context.Reviews.Where(r => r.IsApproved).AverageAsync(r => (double?)r.Rating) ?? 0
            };

            ViewBag.Stats = stats;
            ViewBag.Status = status;
            return View(reviews);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReview(int reviewId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = true;
            review.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review approved successfully!";
            return RedirectToAction(nameof(ReviewManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReview(int reviewId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
            {
                return NotFound();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review rejected and removed.";
            return RedirectToAction(nameof(ReviewManagement));
        }

        // Batch update order status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchUpdateOrderStatus(int[] orderIds, string status)
        {
            if (orderIds == null || orderIds.Length == 0)
            {
                TempData["ErrorMessage"] = "No orders selected.";
                return RedirectToAction(nameof(Orders));
            }

            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .Where(a => orderIds.Contains(a.Id))
                .ToListAsync();

            var updatedCount = 0;
            foreach (var appointment in appointments)
            {
                var oldStatus = appointment.Status;
                appointment.Status = status;
                appointment.UpdatedAt = DateTime.UtcNow;

                // Handle cancellation logic if needed
                if (status == "Cancelled" && oldStatus != "Cancelled")
                {
                    if (appointment.CarId > 0)
                    {
                        var car = await _context.Cars.FindAsync(appointment.CarId);
                        if (car != null)
                        {
                            var hasActiveBookings = await _context.Appointments
                                .AnyAsync(a => a.CarId == appointment.CarId &&
                                             a.Id != appointment.Id &&
                                             a.Status != "Cancelled" &&
                                             a.Status != "Completed" &&
                                             ((a.StartDate <= appointment.EndDate && a.EndDate >= appointment.StartDate)));
                            if (!hasActiveBookings)
                            {
                                car.IsAvailable = true;
                            }
                        }
                    }

                    var payment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id && p.Status == "Completed");
                    if (payment != null)
                    {
                        payment.Status = "Refunded";
                        payment.UpdatedAt = DateTime.UtcNow;

                        if (appointment.UserId != null)
                        {
                            var refundNotification = new Notification
                            {
                                UserId = appointment.UserId,
                                Title = "Refund Processed",
                                Message = $"A refund of RM {payment.Amount:N2} has been processed for your cancelled booking.",
                                Type = "Success"
                            };
                            _context.Notifications.Add(refundNotification);
                        }
                    }
                }

                // Create notification for status change
                if (appointment.UserId != null)
                {
                    var notification = new Notification
                    {
                        UserId = appointment.UserId,
                        Title = $"Booking {status}",
                        Message = $"Your booking for {appointment.Car?.DisplayName} has been {status.ToLower()}.",
                        Type = status == "Confirmed" || status == "Completed" ? "Success" : status == "Cancelled" ? "Danger" : "Info"
                    };
                    _context.Notifications.Add(notification);
                }

                updatedCount++;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{updatedCount} order(s) updated to {status} successfully!";
            return RedirectToAction(nameof(Orders));
        }

        // Export orders to CSV
        [HttpPost]
        public async Task<IActionResult> ExportOrdersToCsv(string status = "All", string searchTerm = "")
        {
            var query = _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .Include(a => a.Promotion)
                .AsQueryable();

            if (status == "All")
            {
                query = query.Where(a => a.Status != "Cancelled");
            }
            else if (status != "All")
            {
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(a =>
                    a.CustomerName!.Contains(searchTerm) ||
                    a.CustomerEmail!.Contains(searchTerm) ||
                    a.Car!.Make.Contains(searchTerm) ||
                    a.Car!.Model.Contains(searchTerm));
            }

            var orders = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Order ID,Customer Name,Email,Phone,Car,Start Date,End Date,Status,Total Price,Promotion,Created At");

            foreach (var order in orders)
            {
                csv.AppendLine($"{order.Id}," +
                    $"\"{order.CustomerName}\"," +
                    $"\"{order.CustomerEmail}\"," +
                    $"\"{order.CustomerPhone}\"," +
                    $"\"{order.Car?.DisplayName}\"," +
                    $"{order.StartDate:yyyy-MM-dd HH:mm}," +
                    $"{order.EndDate:yyyy-MM-dd HH:mm}," +
                    $"{order.Status}," +
                    $"RM {order.TotalPrice:N2}," +
                    $"\"{order.Promotion?.Name ?? "None"}\"," +
                    $"{order.CreatedAt:yyyy-MM-dd HH:mm}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"orders_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        // Calendar view for appointments
        public async Task<IActionResult> Calendar()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .Where(a => a.Status != "Cancelled")
                .ToListAsync();

            ViewBag.Appointments = appointments;
            return View();
        }

        // Get calendar events as JSON (for calendar view)
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(DateTime? start, DateTime? end)
        {
            var startDate = start ?? DateTime.UtcNow.AddMonths(-1);
            var endDate = end ?? DateTime.UtcNow.AddMonths(1);

            var appointments = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .Where(a => a.Status != "Cancelled" &&
                           ((a.StartDate >= startDate && a.StartDate <= endDate) ||
                            (a.EndDate >= startDate && a.EndDate <= endDate) ||
                            (a.StartDate <= startDate && a.EndDate >= endDate)))
                .Select(a => new
                {
                    id = a.Id,
                    title = $"{a.Car!.DisplayName} - {a.CustomerName}",
                    start = a.StartDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = a.EndDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    status = a.Status,
                    color = a.Status == "Confirmed" ? "#28a745" :
                           a.Status == "Pending" ? "#ffc107" :
                           a.Status == "Completed" ? "#17a2b8" : "#6c757d"
                })
                .ToListAsync();

            return Json(appointments);
        }
    }
}

