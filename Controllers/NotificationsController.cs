using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using System.Collections.Generic;

namespace CarRentalSystem.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(CarRentalDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                try
                {
                    var notifications = await _context.Notifications
                        .Where(n => n.UserId == user.Id)
                        .OrderByDescending(n => n.CreatedAt)
                        .ToListAsync();

                    return View(notifications);
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name") && ex.Message.Contains("Notifications"))
                {
                    // Table doesn't exist - ensure it's created
                    await _context.Database.EnsureCreatedAsync();
                    return View(new List<Notification>());
                }
            }
            catch (Exception)
            {
                return View(new List<Notification>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found" });

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

            if (notification == null)
            {
                return Json(new { success = false, message = "Notification not found" });
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { count = 0 });

            var count = await _context.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            return Json(new { count });
        }
    }
}

