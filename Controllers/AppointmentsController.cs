using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using System.Collections.Generic;

namespace CarRentalSystem.Controllers
{
    public class AppointmentsController : Controller
    {
        private readonly CarRentalDbContext _context;
        private readonly IAppointmentService _appointmentService;
        private readonly IPromotionService _promotionService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AppointmentsController> _logger;

        public AppointmentsController(
            CarRentalDbContext context,
            IAppointmentService appointmentService,
            IPromotionService promotionService,
            ISubscriptionService subscriptionService,
            UserManager<ApplicationUser> userManager,
            ILogger<AppointmentsController> logger)
        {
            _context = context;
            _appointmentService = appointmentService;
            _promotionService = promotionService;
            _subscriptionService = subscriptionService;
            _userManager = userManager;
            _logger = logger;
        }

        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Index(string view = "Active")
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                var query = _context.Appointments
                    .Include(a => a.Car)
                    .ThenInclude(c => c.Category)
                    .Include(a => a.Promotion)
                    .Include(a => a.Subscription)
                    .AsNoTracking() // Avoid tracking to prevent navigation property issues
                    .Where(a => a.UserId == user.Id);

            // Filter based on view
            if (view == "Active")
            {
                query = query.Where(a => a.Status != "Cancelled");
            }
            else if (view == "History")
            {
                query = query.Where(a => a.Status == "Completed" || a.Status == "Cancelled");
            }
            else if (view == "All")
            {
                // Show all
            }

                var userAppointments = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
                
                // Clear circular references to prevent serialization issues
                foreach (var appointment in userAppointments)
                {
                    if (appointment.Car != null)
                    {
                        appointment.Car.Appointments = new List<Appointment>();
                    }
                }

                // Get payments for all appointments
                var appointmentIds = userAppointments.Select(a => a.Id).ToList();
                var payments = await _context.Payments
                    .Where(p => appointmentIds.Contains(p.AppointmentId))
                    .ToListAsync();
                
                var paymentDict = payments.ToDictionary(p => p.AppointmentId, p => p);
                ViewBag.Payments = paymentDict;
                ViewBag.View = view;

                return View(userAppointments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading appointments: {Error}", ex.Message);
                ViewBag.View = view;
                ViewBag.ErrorMessage = "Unable to load appointments. Please try again later.";
                return View(new List<Appointment>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ValidatePromotionCode(string code, int carId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new { valid = false, message = "Code is required" });
            }

            try
            {
                var car = await _context.Cars.FindAsync(carId);
                if (car == null)
                {
                    return Json(new { valid = false, message = "Car not found" });
                }

                var promotion = await _promotionService.GetPromotionByCodeAsync(code);
                if (promotion == null)
                {
                    return Json(new { valid = false, message = "Invalid code" });
                }

                var isValid = await _promotionService.ValidatePromotionCodeAsync(code, car.IsElectric);
                if (isValid)
                {
                    return Json(new
                    {
                        valid = true,
                        discount = promotion.DiscountPercentage,
                        promotionId = promotion.Id,
                        message = $"Valid! {promotion.DiscountPercentage}% discount applied."
                    });
                }
                else
                {
                    return Json(new { valid = false, message = "Invalid or expired code" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating promotion code: {Error}", ex.Message);
                return Json(new { valid = false, message = "Error validating code" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(int carId)
        {
            try
            {
                if (carId <= 0)
                {
                    return Json(new List<object>());
                }
                
                var defaultStartDate = DateTime.UtcNow.Date;
                var defaultEndDate = defaultStartDate.AddDays(30);
                var availableSlots = await _appointmentService.GetAvailableTimeSlotsAsync(carId, defaultStartDate, defaultEndDate);
                
                var result = availableSlots.Select(slot => new
                {
                    start = slot.Start.ToString("yyyy-MM-ddTHH:mm"),
                    end = slot.End.ToString("yyyy-MM-ddTHH:mm")
                }).ToList();
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available slots for car {CarId}", carId);
                return Json(new List<object>());
            }
        }

        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create(int? carId, DateTime? startDate = null, DateTime? endDate = null, int? subscriptionId = null)
        {
            // Check if car exists and is available when coming from homepage or car details
            if (carId.HasValue)
            {
                var car = await _context.Cars
                    .Include(c => c.Category)
                    .AsNoTracking() // Avoid tracking to prevent navigation property loading
                    .FirstOrDefaultAsync(c => c.Id == carId.Value);
                    
                if (car == null)
                {
                    TempData["ErrorMessage"] = "The selected vehicle does not exist. Please choose another vehicle.";
                    return RedirectToAction("Index", "Cars");
                }
                
                if (!car.IsAvailable)
                {
                    TempData["ErrorMessage"] = $"The vehicle '{car.DisplayName}' is currently unavailable. Please choose another vehicle.";
                    return RedirectToAction("Details", "Cars", new { id = carId.Value });
                }
                
                // Clear navigation properties to avoid circular reference
                car.Appointments = new List<Appointment>();
                
                ViewBag.SelectedCar = car;
                
                // Get available time slots for this car
                var defaultStartDate = DateTime.UtcNow.Date;
                var defaultEndDate = defaultStartDate.AddDays(30);
                var availableSlots = await _appointmentService.GetAvailableTimeSlotsAsync(carId.Value, defaultStartDate, defaultEndDate);
                ViewBag.AvailableSlots = availableSlots;
                
                // If time slot is pre-selected from car details page
                if (startDate.HasValue && endDate.HasValue)
                {
                    ViewBag.PreSelectedStartDate = startDate.Value;
                    ViewBag.PreSelectedEndDate = endDate.Value;
                }
            }

            ViewBag.Cars = new SelectList(
                await _context.Cars.Where(c => c.IsAvailable).ToListAsync(),
                "Id",
                "DisplayName",
                carId);

            ViewBag.Promotions = new SelectList(
                await _promotionService.GetActivePromotionsAsync(),
                "Id",
                "Name");

            ViewBag.Subscriptions = new SelectList(
                await _subscriptionService.GetActiveSubscriptionsAsync(),
                "Id",
                "Name",
                subscriptionId);

            // If subscriptionId is provided (from subscription page), set it in ViewBag
            if (subscriptionId.HasValue)
            {
                ViewBag.PreSelectedSubscriptionId = subscriptionId.Value;
            }

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment appointment)
        {
            // Ensure CarId is set from form - check multiple sources
            if (appointment.CarId == 0)
            {
                // Try hidden input first
                var carIdFromHidden = Request.Form["CarId"].ToString();
                if (int.TryParse(carIdFromHidden, out int carIdHidden))
                {
                    appointment.CarId = carIdHidden;
                }
                
                // If still 0, try select dropdown (it has name="CarId")
                if (appointment.CarId == 0)
                {
                    var carIdFromSelect = Request.Form["carSelect"].ToString();
                    if (string.IsNullOrEmpty(carIdFromSelect))
                    {
                        carIdFromSelect = Request.Form["CarId"].ToString();
                    }
                    if (int.TryParse(carIdFromSelect, out int carIdSelect))
                    {
                        appointment.CarId = carIdSelect;
                    }
                }
            }
            
            // Validate CarId is set
            if (appointment.CarId == 0)
            {
                ModelState.AddModelError("CarId", "Please select a vehicle to continue with your booking.");
            }
            else
            {
                // Verify the car exists and is available
                var car = await _context.Cars.FindAsync(appointment.CarId);
                if (car == null)
                {
                    ModelState.AddModelError("CarId", "The selected vehicle does not exist. Please select another vehicle.");
                }
                else if (!car.IsAvailable)
                {
                    ModelState.AddModelError("CarId", $"The vehicle '{car.DisplayName}' is currently unavailable. Please select another vehicle.");
                }
            }
            
            // Server-side date validation - CRITICAL
            if (appointment.StartDate == default(DateTime) || appointment.EndDate == default(DateTime))
            {
                if (appointment.StartDate == default(DateTime))
                {
                    ModelState.AddModelError("StartDate", "Pickup date is required.");
                }
                if (appointment.EndDate == default(DateTime))
                {
                    ModelState.AddModelError("EndDate", "Return date is required.");
                }
            }
            else
            {
                if (appointment.EndDate <= appointment.StartDate)
                {
                    ModelState.AddModelError("EndDate", "Return date must be after pickup date.");
                }

                if (appointment.StartDate < DateTime.UtcNow.AddMinutes(-5)) // Allow 5 minute buffer for time zone differences
                {
                    ModelState.AddModelError("StartDate", "Pickup date cannot be in the past.");
                }

                if (appointment.EndDate < DateTime.UtcNow.AddMinutes(-5)) // Allow 5 minute buffer
                {
                    ModelState.AddModelError("EndDate", "Return date cannot be in the past.");
                }

                // Validate minimum rental duration (at least 1 hour)
                var rentalDuration = appointment.EndDate - appointment.StartDate;
                if (rentalDuration.TotalHours < 1)
                {
                    ModelState.AddModelError("EndDate", "Rental duration must be at least 1 hour.");
                }
            }

            if (ModelState.IsValid)
            {
                // Validate car availability
                var isAvailable = await _appointmentService.IsCarAvailableAsync(
                    appointment.CarId,
                    appointment.StartDate,
                    appointment.EndDate);

                if (!isAvailable)
                {
                    var car = await _context.Cars.FindAsync(appointment.CarId);
                    var carName = car?.DisplayName ?? "the selected car";
                    
                    // Check if car is generally available
                    if (car != null && !car.IsAvailable)
                    {
                        ModelState.AddModelError("CarId", $"{carName} is currently unavailable. Please select a different vehicle.");
                    }
                    else
                    {
                        // Check for conflicting appointments
                        var conflictingAppointments = await _context.Appointments
                            .Where(a => a.CarId == appointment.CarId
                                && a.Status != "Cancelled"
                                && a.StartDate < appointment.EndDate
                                && a.EndDate > appointment.StartDate)
                            .OrderBy(a => a.StartDate)
                            .ToListAsync();
                        
                        if (conflictingAppointments.Any())
                        {
                            var conflict = conflictingAppointments.First();
                            ModelState.AddModelError("", 
                                $"{carName} is not available for the selected dates. " +
                                $"It's already booked from {conflict.StartDate:MMM dd, yyyy HH:mm} to {conflict.EndDate:MMM dd, yyyy HH:mm}. " +
                                $"Please choose different dates or select another vehicle.");
                        }
                        else
                        {
                            ModelState.AddModelError("", $"{carName} is not available for the chosen dates. Please select different dates or choose another vehicle.");
                        }
                    }
                    
                    // After adding errors, ensure ViewBag is loaded before returning view
                    if (appointment.CarId > 0)
                    {
                        var selectedCar = await _context.Cars
                            .Include(c => c.Category)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.Id == appointment.CarId);
                        if (selectedCar != null)
                        {
                            selectedCar.Appointments = new List<Appointment>();
                            ViewBag.SelectedCar = selectedCar;
                        }
                    }
                    
                    ViewBag.Cars = new SelectList(
                        await _context.Cars.Where(c => c.IsAvailable).ToListAsync(),
                        "Id",
                        "DisplayName",
                        appointment.CarId);
                    ViewBag.Promotions = new SelectList(
                        await _promotionService.GetActivePromotionsAsync(),
                        "Id",
                        "Name",
                        appointment.PromotionId);
                    ViewBag.Subscriptions = new SelectList(
                        await _subscriptionService.GetActiveSubscriptionsAsync(),
                        "Id",
                        "Name",
                        appointment.SubscriptionId);
                    
                    // Return view with errors - don't continue processing
                    return View(appointment);
                }
                else
                {
                    // Get current user
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null)
                    {
                        ModelState.AddModelError("", "User not found. Please login again.");
                        
                        // Load selected car for ViewBag if CarId is set
                        if (appointment.CarId > 0)
                        {
                            var selectedCar = await _context.Cars
                                .Include(c => c.Category)
                                .AsNoTracking() // Avoid tracking to prevent navigation property loading
                                .FirstOrDefaultAsync(c => c.Id == appointment.CarId);
                            if (selectedCar != null)
                            {
                                // Clear navigation properties to avoid circular reference
                                selectedCar.Appointments = new List<Appointment>();
                                ViewBag.SelectedCar = selectedCar;
                            }
                        }
                        
                        ViewBag.Cars = new SelectList(
                            await _context.Cars.Where(c => c.IsAvailable).ToListAsync(),
                            "Id",
                            "DisplayName",
                            appointment.CarId);
                        ViewBag.Promotions = new SelectList(
                            await _promotionService.GetActivePromotionsAsync(),
                            "Id",
                            "Name",
                            appointment.PromotionId);
                        ViewBag.Subscriptions = new SelectList(
                            await _subscriptionService.GetActiveSubscriptionsAsync(),
                            "Id",
                            "Name",
                            appointment.SubscriptionId);
                        return View(appointment);
                    }
                    
                    appointment.UserId = user.Id;
                    appointment.CustomerName = $"{user.FirstName} {user.LastName}";
                    appointment.CustomerEmail = user.Email ?? string.Empty;
                    appointment.CustomerPhone = user.PhoneNumber ?? string.Empty;
                    appointment.Status = "Pending"; // Set initial status
                    appointment.CreatedAt = DateTime.UtcNow;

                    // Ensure SubscriptionId is set from form if provided
                    if (appointment.SubscriptionId == null || appointment.SubscriptionId == 0)
                    {
                        var subscriptionIdFromForm = Request.Form["SubscriptionId"].ToString();
                        if (!string.IsNullOrEmpty(subscriptionIdFromForm) && int.TryParse(subscriptionIdFromForm, out int subscriptionId))
                        {
                            // Verify subscription exists and is active
                            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(subscriptionId);
                            if (subscription != null && subscription.IsActive)
                            {
                                appointment.SubscriptionId = subscriptionId;
                            }
                        }
                    }
                    else
                    {
                        // Verify the subscription is still active
                        var subscription = await _subscriptionService.GetSubscriptionByIdAsync(appointment.SubscriptionId.Value);
                        if (subscription == null || !subscription.IsActive)
                        {
                            appointment.SubscriptionId = null; // Clear if invalid
                        }
                    }

                    // Apply promotion if code is provided (before service calculates price)
                    if (!string.IsNullOrEmpty(Request.Form["PromotionCode"]))
                    {
                        var promotionCode = Request.Form["PromotionCode"].ToString().Trim();
                        if (!string.IsNullOrEmpty(promotionCode))
                        {
                            var selectedCar = await _context.Cars.FindAsync(appointment.CarId);
                            if (selectedCar != null)
                            {
                                var promotion = await _promotionService.GetPromotionByCodeAsync(promotionCode);
                                if (promotion != null && await _promotionService.ValidatePromotionCodeAsync(promotion.Code, selectedCar.IsElectric))
                                {
                                    appointment.PromotionId = promotion.Id;
                                    // The service will calculate the discount
                                    promotion.CurrentUses++;
                                    _context.Promotions.Update(promotion);
                                }
                                else
                                {
                                    // Promotion code was provided but is invalid
                                    ModelState.AddModelError("", $"The promotion code '{promotionCode}' is invalid or expired. Please check and try again.");
                                }
                            }
                        }
                    }
                    // Also check if PromotionId is directly provided (from hidden input)
                    else if (!string.IsNullOrEmpty(Request.Form["PromotionId"]))
                    {
                        var promotionIdStr = Request.Form["PromotionId"].ToString();
                        if (int.TryParse(promotionIdStr, out int promotionId))
                        {
                            var promotion = await _promotionService.GetPromotionByIdAsync(promotionId);
                            var selectedCar = await _context.Cars.FindAsync(appointment.CarId);
                            if (promotion != null && selectedCar != null && await _promotionService.ValidatePromotionCodeAsync(promotion.Code, selectedCar.IsElectric))
                            {
                                appointment.PromotionId = promotionId;
                                promotion.CurrentUses++;
                                _context.Promotions.Update(promotion);
                            }
                        }
                    }

                    // Double-check ModelState is still valid after promotion validation
                    if (!ModelState.IsValid)
                    {
                        // Load ViewBag data for return
                        if (appointment.CarId > 0)
                        {
                            var selectedCar = await _context.Cars
                                .Include(c => c.Category)
                                .AsNoTracking() // Avoid tracking to prevent navigation property loading
                                .FirstOrDefaultAsync(c => c.Id == appointment.CarId);
                            if (selectedCar != null)
                            {
                                // Clear navigation properties to avoid circular reference
                                selectedCar.Appointments = new List<Appointment>();
                                ViewBag.SelectedCar = selectedCar;
                            }
                        }
                        ViewBag.Cars = new SelectList(
                            await _context.Cars.Where(c => c.IsAvailable).ToListAsync(),
                            "Id",
                            "DisplayName",
                            appointment.CarId);
                        ViewBag.Promotions = new SelectList(
                            await _promotionService.GetActivePromotionsAsync(),
                            "Id",
                            "Name",
                            appointment.PromotionId);
                        ViewBag.Subscriptions = new SelectList(
                            await _subscriptionService.GetActiveSubscriptionsAsync(),
                            "Id",
                            "Name",
                            appointment.SubscriptionId);
                        return View(appointment);
                    }

                    // Use the service to create appointment (it will calculate final price including discounts)
                    var createdAppointment = await _appointmentService.CreateAppointmentAsync(appointment);
                    
                    // Load car for notification message
                    var carForNotification = await _context.Cars.FindAsync(createdAppointment.CarId);
                    var carName = carForNotification?.DisplayName ?? "your selected vehicle";
                    
                    // Create notification for successful booking
                    var notification = new Notification
                    {
                        UserId = user.Id,
                        Title = "Booking Created",
                        Message = $"Your booking for {carName} has been created successfully. Please proceed with payment to confirm your booking.",
                        Type = "Success"
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Booking created successfully! Please proceed with payment to confirm your booking.";
                    return RedirectToAction(nameof(Details), new { id = createdAppointment.Id });
                }
            }

            // Load selected car for ViewBag if CarId is set
            if (appointment.CarId > 0)
            {
                var selectedCar = await _context.Cars
                    .Include(c => c.Category)
                    .AsNoTracking() // Avoid tracking to prevent navigation property loading
                    .FirstOrDefaultAsync(c => c.Id == appointment.CarId);
                if (selectedCar != null)
                {
                    // Clear navigation properties to avoid circular reference
                    selectedCar.Appointments = new List<Appointment>();
                    ViewBag.SelectedCar = selectedCar;
                }
            }

            ViewBag.Cars = new SelectList(
                await _context.Cars.Where(c => c.IsAvailable).ToListAsync(),
                "Id",
                "DisplayName",
                appointment.CarId);

            ViewBag.Promotions = new SelectList(
                await _promotionService.GetActivePromotionsAsync(),
                "Id",
                "Name",
                appointment.PromotionId);

            ViewBag.Subscriptions = new SelectList(
                await _subscriptionService.GetActiveSubscriptionsAsync(),
                "Id",
                "Name",
                appointment.SubscriptionId);

            return View(appointment);
        }

        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                var appointment = await _appointmentService.GetAppointmentByIdAsync(id);
                if (appointment == null)
                {
                    TempData["ErrorMessage"] = "Booking not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Verify the booking belongs to the current user
                if (appointment.UserId != user.Id)
                {
                    TempData["ErrorMessage"] = "You don't have permission to view this booking.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if payment exists
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.AppointmentId == id);
                
                ViewBag.Payment = payment;
                ViewBag.HasPayment = payment != null;

                return View(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading booking details {BookingId}: {Error}", id, ex.Message);
                TempData["ErrorMessage"] = "An error occurred while loading booking details. Please try again later.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                var appointment = await _appointmentService.GetAppointmentByIdAsync(id);
                if (appointment == null)
                {
                    TempData["ErrorMessage"] = "Booking not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Verify the booking belongs to the current user
                if (appointment.UserId != user.Id)
                {
                    TempData["ErrorMessage"] = "You don't have permission to cancel this booking.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if booking can be cancelled
                if (appointment.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "This booking is already cancelled.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (appointment.Status == "Completed")
                {
                    TempData["ErrorMessage"] = "Cannot cancel a completed booking.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Check if pickup date is within 48 hours (free cancellation policy)
                var hoursUntilPickup = (appointment.StartDate - DateTime.UtcNow).TotalHours;
                var canCancelFree = hoursUntilPickup >= 48;

                // Update booking status
                appointment.Status = "Cancelled";
                appointment.UpdatedAt = DateTime.UtcNow;
                _context.Appointments.Update(appointment);

                // Check if there's a payment and handle refund
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.AppointmentId == id && p.Status == "Completed");
                
                if (payment != null)
                {
                    if (canCancelFree)
                    {
                        // Full refund for cancellation within 48 hours
                        payment.Status = "Refunded";
                        payment.UpdatedAt = DateTime.UtcNow;
                        _context.Payments.Update(payment);
                    }
                    else
                    {
                        // Partial refund or no refund depending on policy
                        payment.Status = "PartiallyRefunded";
                        payment.UpdatedAt = DateTime.UtcNow;
                        _context.Payments.Update(payment);
                    }
                }

                // Create notification
                var carName = appointment.Car?.DisplayName ?? "your booked vehicle";
                var notification = new Notification
                {
                    UserId = user.Id,
                    Title = "Booking Cancelled",
                    Message = canCancelFree 
                        ? $"Your booking for {carName} has been cancelled. A full refund will be processed if payment was made."
                        : $"Your booking for {carName} has been cancelled. Please check refund policy for details.",
                    Type = "Info"
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = canCancelFree
                    ? "Booking cancelled successfully. A full refund will be processed if payment was made."
                    : "Booking cancelled successfully. Please check cancellation policy for refund details.";

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling booking {BookingId}: {Error}", id, ex.Message);
                TempData["ErrorMessage"] = "An error occurred while cancelling the booking. Please try again later.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // Quick rebook from previous appointment
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Rebook(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var previousAppointment = await _context.Appointments
                .Include(a => a.Car)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);

            if (previousAppointment == null)
            {
                return NotFound();
            }

            // Redirect to create page with pre-filled data
            return RedirectToAction(nameof(Create), new { carId = previousAppointment.CarId });
        }
    }
}
