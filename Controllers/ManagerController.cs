using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly CarRentalDbContext _context;

        public ManagerController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            CarRentalDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public IActionResult Dashboard()
        {
            try
            {
                var stats = new
                {
                    TotalUsers = _userManager.Users.Count(),
                    TotalStaff = _userManager.GetUsersInRoleAsync("Staff").Result.Count,
                    TotalCustomers = _userManager.GetUsersInRoleAsync("Customer").Result.Count,
                    TotalAppointments = _context.Appointments?.Count() ?? 0,
                    TotalRevenue = _context.Appointments?
                        .Where(a => a.Status == "Confirmed" || a.Status == "Completed")
                        .Sum(a => a.TotalPrice) ?? 0,
                    TotalCars = _context.Cars?.Count() ?? 0
                };

                ViewBag.Stats = stats;
                return View();
            }
            catch (Exception)
            {
                ViewBag.Stats = new
                {
                    TotalUsers = 0,
                    TotalStaff = 0,
                    TotalCustomers = 0,
                    TotalAppointments = 0,
                    TotalRevenue = 0,
                    TotalCars = 0
                };
                ViewBag.ErrorMessage = "Unable to load dashboard statistics. Please try again later.";
                return View();
            }
        }

        public async Task<IActionResult> StaffManagement()
        {
            try
            {
                var staffRole = await _roleManager.FindByNameAsync("Staff");
                if (staffRole == null)
                {
                    return View(new List<ApplicationUser>());
                }

                var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");
                return View(staffUsers);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load staff management data. Please try again later.";
                return View(new List<ApplicationUser>());
            }
        }

        [HttpGet]
        public IActionResult CreateStaff()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(CreateStaffViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Ensure Staff role exists
                    if (!await _roleManager.RoleExistsAsync("Staff"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Staff"));
                    }

                    await _userManager.AddToRoleAsync(user, "Staff");
                    return RedirectToAction(nameof(StaffManagement));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStaffStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(StaffManagement));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaff(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            await _userManager.DeleteAsync(user);
            return RedirectToAction(nameof(StaffManagement));
        }

        public async Task<IActionResult> AllUsers(string searchTerm = "", string? roleFilter = null, string? statusFilter = null)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u =>
                    u.Email!.Contains(searchTerm) ||
                    u.FirstName!.Contains(searchTerm) ||
                    u.LastName!.Contains(searchTerm) ||
                    u.PhoneNumber!.Contains(searchTerm));
            }

            // Filter by role
            if (!string.IsNullOrEmpty(roleFilter) && roleFilter != "All")
            {
                var role = await _roleManager.FindByNameAsync(roleFilter);
                if (role != null)
                {
                    var userIdsInRole = _context.UserRoles
                        .Where(ur => ur.RoleId == role.Id)
                        .Select(ur => ur.UserId);
                    query = query.Where(u => userIdsInRole.Contains(u.Id));
                }
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
                    TotalSpent = totalSpent
                };
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.UserStats = userStats;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.Roles = await _roleManager.Roles.ToListAsync();
            
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllCustomers()
        {
            try
            {
                // Get all users with Customer role only (not Staff or Manager)
                var customerRole = await _roleManager.FindByNameAsync("Customer");
                if (customerRole == null)
                {
                    TempData["ErrorMessage"] = "Customer role not found.";
                    return RedirectToAction(nameof(AllUsers));
                }

                var customerUserIds = _context.UserRoles
                    .Where(ur => ur.RoleId == customerRole.Id)
                    .Select(ur => ur.UserId)
                    .ToList();

                var customers = await _userManager.Users
                    .Where(u => customerUserIds.Contains(u.Id))
                    .ToListAsync();

                int deletedCount = 0;
                foreach (var customer in customers)
                {
                    // Check if user has other roles (Staff or Manager)
                    var roles = await _userManager.GetRolesAsync(customer);
                    if (roles.Contains("Customer") && !roles.Any(r => r == "Staff" || r == "Manager"))
                    {
                        // Delete related data first to avoid foreign key constraints
                        var appointments = await _context.Appointments.Where(a => a.UserId == customer.Id).ToListAsync();
                        _context.Appointments.RemoveRange(appointments);
                        
                        var favorites = await _context.Favorites.Where(f => f.UserId == customer.Id).ToListAsync();
                        _context.Favorites.RemoveRange(favorites);
                        
                        var reviews = await _context.Reviews.Where(r => r.UserId == customer.Id).ToListAsync();
                        _context.Reviews.RemoveRange(reviews);
                        
                        // Delete payments through appointments
                        var appointmentIds = await _context.Appointments.Where(a => a.UserId == customer.Id).Select(a => a.Id).ToListAsync();
                        var payments = await _context.Payments.Where(p => appointmentIds.Contains(p.AppointmentId)).ToListAsync();
                        _context.Payments.RemoveRange(payments);
                        
                        var notifications = await _context.Notifications.Where(n => n.UserId == customer.Id).ToListAsync();
                        _context.Notifications.RemoveRange(notifications);
                        
                        await _context.SaveChangesAsync();
                        
                        // Now delete the user
                        var result = await _userManager.DeleteAsync(customer);
                        if (result.Succeeded)
                        {
                            deletedCount++;
                        }
                    }
                }

                TempData["SuccessMessage"] = $"Successfully deleted {deletedCount} customer account(s). Staff and Manager accounts were preserved.";
                return RedirectToAction(nameof(AllUsers));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting customers: {ex.Message}";
                return RedirectToAction(nameof(AllUsers));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = $"User account {(user.IsActive ? "activated" : "deactivated")} successfully.";
            return RedirectToAction(nameof(AllUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Prevent deleting yourself
            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account!";
                return RedirectToAction(nameof(AllUsers));
            }

            await _userManager.DeleteAsync(user);

            TempData["SuccessMessage"] = "User deleted successfully.";
            return RedirectToAction(nameof(AllUsers));
        }

        // System Statistics
        public async Task<IActionResult> SystemStatistics()
        {
            var stats = new
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalCustomers = (await _userManager.GetUsersInRoleAsync("Customer")).Count,
                TotalStaff = (await _userManager.GetUsersInRoleAsync("Staff")).Count,
                TotalManagers = (await _userManager.GetUsersInRoleAsync("Manager")).Count,
                TotalCars = await _context.Cars.CountAsync(),
                AvailableCars = await _context.Cars.CountAsync(c => c.IsAvailable),
                TotalBookings = await _context.Appointments.CountAsync(),
                TotalRevenue = await _context.Appointments
                    .Where(a => a.Status == "Confirmed" || a.Status == "Completed")
                    .SumAsync(a => (decimal?)a.TotalPrice) ?? 0,
                ThisMonthRevenue = await _context.Appointments
                    .Where(a => (a.Status == "Confirmed" || a.Status == "Completed") &&
                               a.CreatedAt.Month == DateTime.UtcNow.Month &&
                               a.CreatedAt.Year == DateTime.UtcNow.Year)
                    .SumAsync(a => (decimal?)a.TotalPrice) ?? 0,
                TotalReviews = await _context.Reviews.CountAsync(),
                ApprovedReviews = await _context.Reviews.CountAsync(r => r.IsApproved),
                TotalPromotions = await _context.Promotions.CountAsync(),
                ActivePromotions = await _context.Promotions.CountAsync(p => p.IsActive),
                TotalSubscriptions = await _context.Subscriptions.CountAsync(),
                ActiveSubscriptions = await _context.Subscriptions.CountAsync(s => s.IsActive)
            };

            ViewBag.Stats = stats;
            return View();
        }
    }

    public class CreateStaffViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }
    }
}

