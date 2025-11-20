using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Services;

namespace CarRentalSystem.Controllers
{
    public class CaptchaController : Controller
    {
        private readonly ICaptchaService _captchaService;

        public CaptchaController(ICaptchaService captchaService)
        {
            _captchaService = captchaService;
        }

        [HttpGet]
        public IActionResult Generate(string type = "login")
        {
            var (imageBytes, code) = _captchaService.GenerateCaptcha();
            
            var sessionKey = type == "register" ? "CaptchaCode" : "LoginCaptchaCode";
            HttpContext.Session.SetString(sessionKey, code);
            
            return File(imageBytes, "image/png");
        }

        [HttpGet]
        public IActionResult Refresh(string type = "login")
        {
            return RedirectToAction("Generate", new { type });
        }
    }
}

