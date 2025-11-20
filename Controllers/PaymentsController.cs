using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class PaymentsController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PaymentsController(CarRentalDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                // Only show user's own payments for customers
                var userAppointments = await _context.Appointments
                    .Where(a => a.UserId == user.Id)
                    .Select(a => a.Id)
                    .ToListAsync();

                var payments = await _context.Payments
                    .Include(p => p.Appointment)
                        .ThenInclude(a => a!.Car)
                    .Where(p => userAppointments.Contains(p.AppointmentId))
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return View(payments);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load payments. Please try again later.";
                return View(new List<Payment>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || appointment.UserId != user.Id)
            {
                return Unauthorized();
            }

            // Check if payment already exists
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId);

            if (existingPayment != null && existingPayment.Status == "Completed")
            {
                TempData["InfoMessage"] = "Payment already completed for this appointment.";
                return RedirectToAction(nameof(Details), new { id = existingPayment.Id });
            }

            ViewBag.Appointment = appointment;
            ViewBag.Amount = appointment.TotalPrice;
            return View(new Payment 
            { 
                AppointmentId = appointmentId,
                Amount = appointment.TotalPrice,
                PaymentMethod = "Credit Card"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Payment payment)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Car)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == payment.AppointmentId);

            if (appointment == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                payment.PaymentDate = DateTime.UtcNow;
                payment.Status = "Pending";
                
                // Simulate payment processing (in real app, integrate with payment gateway)
                if (!string.IsNullOrEmpty(payment.TransactionId) || payment.PaymentMethod == "Credit Card")
                {
                    // Simulate successful payment
                    payment.Status = "Completed";
                    payment.TransactionId = payment.TransactionId ?? Guid.NewGuid().ToString();
                    payment.UpdatedAt = DateTime.UtcNow;
                    
                    // Update appointment status to Confirmed if payment successful
                    if (appointment.Status == "Pending")
                    {
                        appointment.Status = "Confirmed";
                        appointment.UpdatedAt = DateTime.UtcNow;
                    }
                }

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Create notification
                var notification = new Notification
                {
                    UserId = appointment.UserId!,
                    Title = "Payment Received",
                    Message = $"Your payment of RM {payment.Amount:N2} for booking {appointment.Car!.DisplayName} has been processed successfully.",
                    Type = "Success"
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Payment processed successfully!";
                return RedirectToAction(nameof(Details), new { id = payment.Id });
            }

            ViewBag.Appointment = appointment;
            ViewBag.Amount = appointment.TotalPrice;
            return View(payment);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Appointment)
                    .ThenInclude(a => a!.Car)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a!.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                return NotFound();
            }

            // Check authorization - customers can only access their own payments, staff/manager can access all
            if (!User.IsInRole("Staff") && !User.IsInRole("Manager"))
            {
                if (payment.Appointment!.UserId != user.Id)
                {
                    return Unauthorized();
                }
            }

            return View(payment);
        }

        [HttpPost]
        [Authorize(Roles = "Staff,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var payment = await _context.Payments
                .Include(p => p.Appointment)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                return NotFound();
            }

            payment.Status = status;
            payment.UpdatedAt = DateTime.UtcNow;
            
            // Update appointment status based on payment status
            if (payment.Appointment != null)
            {
                if (status == "Completed")
                {
                    // If payment completed, update appointment status
                    if (payment.Appointment.Status == "Pending")
                    {
                        payment.Appointment.Status = "Confirmed";
                        payment.Appointment.UpdatedAt = DateTime.UtcNow;
                        
                        // Create notification for user
                        var notification = new Notification
                        {
                            UserId = payment.Appointment.UserId!,
                            Title = "Booking Confirmed",
                            Message = $"Your booking for {payment.Appointment.Car?.DisplayName} has been confirmed. Payment received.",
                            Type = "Success"
                        };
                        _context.Notifications.Add(notification);
                    }
                    // If already confirmed and rental period has ended, mark as completed
                    else if (payment.Appointment.Status == "Confirmed" && payment.Appointment.EndDate < DateTime.UtcNow)
                    {
                        payment.Appointment.Status = "Completed";
                        payment.Appointment.UpdatedAt = DateTime.UtcNow;
                        
                        // Create notification for completed booking with review prompt
                        var completedNotification = new Notification
                        {
                            UserId = payment.Appointment.UserId!,
                            Title = "Booking Completed",
                            Message = $"Your booking for {payment.Appointment.Car?.DisplayName} has been completed. Share your experience by leaving a review!",
                            Type = "Success"
                        };
                        _context.Notifications.Add(completedNotification);
                    }
                }
                else if (status == "Failed" || status == "Refunded")
                {
                    // If payment failed or refunded, revert appointment to Pending
                    if (payment.Appointment.Status == "Confirmed")
                    {
                        payment.Appointment.Status = "Pending";
                        payment.Appointment.UpdatedAt = DateTime.UtcNow;
                        
                        // Create notification for user
                        var notification = new Notification
                        {
                            UserId = payment.Appointment.UserId!,
                            Title = "Payment Issue",
                            Message = $"There was an issue with your payment for booking {payment.Appointment.Car?.DisplayName}. Please update your payment method.",
                            Type = "Warning"
                        };
                        _context.Notifications.Add(notification);
                    }
                }
            }
            
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Payment status updated to {status} successfully!";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

