using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Services;
using CarRentalSystem.Models;
using System.Collections.Generic;

namespace CarRentalSystem.Controllers
{
    public class SubscriptionsController : Controller
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionsController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();
                return View(subscriptions);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load subscriptions. Please try again later.";
                return View(new List<Subscription>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
                if (subscription == null)
                {
                    return NotFound();
                }

                return View(subscription);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load subscription details. Please try again later.";
                return NotFound();
            }
        }

        [Authorize(Roles = "Staff,Manager")]
        public async Task<IActionResult> Manage()
        {
            var subscriptions = await _subscriptionService.GetAllSubscriptionsAsync();
            return View(subscriptions);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Subscription subscription)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _subscriptionService.CreateSubscriptionAsync(subscription);
                    TempData["SuccessMessage"] = "Subscription plan created successfully!";
                    return RedirectToAction(nameof(Manage));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating subscription plan: {ex.Message}");
                }
            }
            return View(subscription);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (subscription == null)
            {
                return NotFound();
            }
            return View(subscription);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Subscription subscription)
        {
            if (id != subscription.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _subscriptionService.UpdateSubscriptionAsync(subscription);
                    TempData["SuccessMessage"] = "Subscription plan updated successfully!";
                    return RedirectToAction(nameof(Manage));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating subscription plan: {ex.Message}");
                }
            }
            return View(subscription);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _subscriptionService.DeleteSubscriptionAsync(id);
                if (deleted)
                {
                    TempData["SuccessMessage"] = "Subscription plan deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Subscription plan not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting subscription plan: {ex.Message}";
            }
            return RedirectToAction(nameof(Manage));
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (subscription == null)
            {
                TempData["ErrorMessage"] = "Subscription plan not found.";
                return RedirectToAction(nameof(Manage));
            }

            subscription.IsActive = !subscription.IsActive;
            await _subscriptionService.UpdateSubscriptionAsync(subscription);
            TempData["SuccessMessage"] = $"Subscription plan {(subscription.IsActive ? "activated" : "deactivated")} successfully!";
            return RedirectToAction(nameof(Manage));
        }

        [HttpGet]
        public async Task<IActionResult> GetSubscriptionDetails(int id)
        {
            var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id);
            if (subscription == null)
            {
                return Json(new { error = "Subscription not found" });
            }

            return Json(new
            {
                id = subscription.Id,
                name = subscription.Name,
                discountPercentage = subscription.DiscountPercentage,
                monthlyPrice = subscription.MonthlyPrice,
                isActive = subscription.IsActive
            });
        }
    }
}

