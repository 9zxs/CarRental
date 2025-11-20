using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using System.ComponentModel.DataAnnotations;

namespace CarRentalSystem.Controllers
{
    [Authorize(Roles = "Manager")]
    public class SettingsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(IConfiguration configuration, ILogger<SettingsController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var settings = new SystemSettings
            {
                SiteName = _configuration["SiteSettings:SiteName"] ?? "Car Rental System",
                SiteEmail = _configuration["SiteSettings:SiteEmail"] ?? "",
                SupportPhone = _configuration["SiteSettings:SupportPhone"] ?? "",
                SupportEmail = _configuration["SiteSettings:SupportEmail"] ?? "",
                MaintenanceMode = _configuration.GetValue<bool>("SiteSettings:MaintenanceMode", false),
                AllowRegistration = _configuration.GetValue<bool>("SiteSettings:AllowRegistration", true),
                RequireEmailVerification = _configuration.GetValue<bool>("SiteSettings:RequireEmailVerification", true),
                // Email settings
                SmtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com",
                SmtpPort = _configuration["Email:SmtpPort"] ?? "587",
                SmtpUsername = _configuration["Email:Username"] ?? "",
                SmtpPassword = _configuration["Email:Password"] ?? "",
                FromEmail = _configuration["Email:FromEmail"] ?? "noreply@carrental.com",
                FromName = _configuration["Email:FromName"] ?? "Car Rental System"
            };

            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SystemSettings settings)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Read current appsettings.json
                    var appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                    var jsonContent = await System.IO.File.ReadAllTextAsync(appsettingsPath);
                    
                    // Parse JSON manually to preserve structure
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;
                    var writer = new System.Text.Json.Utf8JsonWriter(
                        new System.IO.FileStream(appsettingsPath, FileMode.Create, FileAccess.Write),
                        new System.Text.Json.JsonWriterOptions { Indented = true });
                    
                    writer.WriteStartObject();
                    
                    // Write all existing properties
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == "Email")
                        {
                            // Write updated Email section
                            writer.WritePropertyName("Email");
                            writer.WriteStartObject();
                            writer.WriteString("SmtpServer", settings.SmtpServer ?? "smtp.gmail.com");
                            writer.WriteString("SmtpPort", settings.SmtpPort ?? "587");
                            writer.WriteString("Username", settings.SmtpUsername ?? "");
                            writer.WriteString("Password", settings.SmtpPassword ?? "");
                            writer.WriteString("FromEmail", settings.FromEmail ?? "noreply@carrental.com");
                            writer.WriteString("FromName", settings.FromName ?? "Car Rental System");
                            writer.WriteEndObject();
                        }
                        else
                        {
                            // Write other properties as-is
                            prop.WriteTo(writer);
                        }
                    }
                    
                    writer.WriteEndObject();
                    writer.Flush();
                    writer.Dispose();
                    
                    TempData["SuccessMessage"] = "Email settings saved successfully! Please restart the application for changes to take effect.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving email settings: {Error}", ex.Message);
                    ModelState.AddModelError("", $"Error saving email settings: {ex.Message}. Please check appsettings.json file permissions.");
                    return View(settings);
                }
                
                return RedirectToAction(nameof(Index));
            }

            return View(settings);
        }
        
        [HttpPost]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TestEmail))
            {
                return Json(new { success = false, message = "Please provide an email address to test." });
            }
            
            var testEmail = request.TestEmail;

            try
            {
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                var testSubject = "Email Configuration Test - Car Rental System";
                var testMessage = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Email Test Successful!</h1>
        </div>
        <div class=""content"">
            <p>Congratulations! Your email configuration is working correctly.</p>
            <p>This is a test email sent from the Car Rental System.</p>
            <p>If you received this email, it means your SMTP settings are properly configured.</p>
        </div>
    </div>
</body>
</html>";

                var emailSent = await emailService.SendEmailAsync(testEmail, testSubject, testMessage);
                
                if (emailSent)
                {
                    return Json(new { success = true, message = $"Test email sent successfully to {testEmail}. Please check your inbox." });
                }
                else
                {
                    return Json(new { success = false, message = "Email service is not configured or failed to send. Please check your SMTP settings and logs." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test email: {Error}", ex.Message);
                return Json(new { success = false, message = $"Error sending test email: {ex.Message}" });
            }
        }
    }

    public class SystemSettings
    {
        [Required]
        [Display(Name = "Site Name")]
        public string SiteName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Site Email")]
        public string SiteEmail { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Support Phone")]
        public string SupportPhone { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Support Email")]
        public string SupportEmail { get; set; } = string.Empty;

        [Display(Name = "Maintenance Mode")]
        public bool MaintenanceMode { get; set; }

        [Display(Name = "Allow Registration")]
        public bool AllowRegistration { get; set; }

        [Display(Name = "Require Email Verification")]
        public bool RequireEmailVerification { get; set; }

        // Email Settings
        [Display(Name = "SMTP Server")]
        public string SmtpServer { get; set; } = "smtp.gmail.com";

        [Display(Name = "SMTP Port")]
        public string SmtpPort { get; set; } = "587";

        [Display(Name = "SMTP Username (Email)")]
        public string SmtpUsername { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "SMTP Password (App Password)")]
        public string SmtpPassword { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "From Email Address")]
        public string FromEmail { get; set; } = "noreply@carrental.com";

        [Display(Name = "From Name")]
        public string FromName { get; set; } = "Car Rental System";
    }

    public class TestEmailRequest
    {
        public string TestEmail { get; set; } = string.Empty;
    }
}

