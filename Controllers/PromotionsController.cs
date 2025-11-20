using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Services;
using CarRentalSystem.Models;
using System.Collections.Generic;

namespace CarRentalSystem.Controllers
{
    public class PromotionsController : Controller
    {
        private readonly IPromotionService _promotionService;

        public PromotionsController(IPromotionService promotionService)
        {
            _promotionService = promotionService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var promotions = await _promotionService.GetActivePromotionsAsync();
                return View(promotions);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "Unable to load promotions. Please try again later.";
                return View(new List<Promotion>());
            }
        }

        [Authorize(Roles = "Staff,Manager")]
        public async Task<IActionResult> Manage()
        {
            var promotions = await _promotionService.GetAllPromotionsAsync();
            return View(promotions);
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
        public async Task<IActionResult> Create(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _promotionService.CreatePromotionAsync(promotion);
                    TempData["SuccessMessage"] = "Promotion created successfully!";
                    return RedirectToAction(nameof(Manage));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating promotion: {ex.Message}");
                }
            }
            return View(promotion);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var promotion = await _promotionService.GetPromotionByIdAsync(id);
            if (promotion == null)
            {
                return NotFound();
            }
            return View(promotion);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Promotion promotion)
        {
            if (id != promotion.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _promotionService.UpdatePromotionAsync(promotion);
                    TempData["SuccessMessage"] = "Promotion updated successfully!";
                    return RedirectToAction(nameof(Manage));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating promotion: {ex.Message}");
                }
            }
            return View(promotion);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _promotionService.DeletePromotionAsync(id);
                if (deleted)
                {
                    TempData["SuccessMessage"] = "Promotion deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Promotion not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting promotion: {ex.Message}";
            }
            return RedirectToAction(nameof(Manage));
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var promotion = await _promotionService.GetPromotionByIdAsync(id);
            if (promotion == null)
            {
                TempData["ErrorMessage"] = "Promotion not found.";
                return RedirectToAction(nameof(Manage));
            }

            promotion.IsActive = !promotion.IsActive;
            await _promotionService.UpdatePromotionAsync(promotion);
            TempData["SuccessMessage"] = $"Promotion {(promotion.IsActive ? "activated" : "deactivated")} successfully!";
            return RedirectToAction(nameof(Manage));
        }
    }
}

