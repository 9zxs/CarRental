using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Data;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IFileUploadService _fileUploadService;
        private readonly ICaptchaService _captchaService;
        private readonly CarRentalDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailService emailService,
            IFileUploadService fileUploadService,
            ICaptchaService captchaService,
            CarRentalDbContext context,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _fileUploadService = fileUploadService;
            _captchaService = captchaService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register()
        {
            // Clear any previous registration attempt
            HttpContext.Session.Remove("CaptchaCode");
            
            // Generate new CAPTCHA for each registration page load
            try
            {
                var (imageBytes, code) = _captchaService.GenerateCaptcha();
                HttpContext.Session.SetString("CaptchaCode", code);
                ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating CAPTCHA: {Error}", ex.Message);
                // Set a default value to prevent view errors
                ViewBag.CaptchaImage = string.Empty;
            }
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmailExists(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { exists = false });
            }

            var user = await _userManager.FindByEmailAsync(email);
            return Json(new { exists = user != null });
        }

        [HttpGet]
        public async Task<IActionResult> CheckPhoneExists(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return Json(new { exists = false });
            }

            // Normalize phone number: remove all non-digit characters for comparison
            var normalizedInput = new string(phoneNumber.Where(char.IsDigit).ToArray());
            
            // Check against all users, normalizing their phone numbers too
            var allUsers = await _userManager.Users.ToListAsync();
            var exists = allUsers.Any(u => 
                !string.IsNullOrWhiteSpace(u.PhoneNumber) && 
                new string(u.PhoneNumber.Where(char.IsDigit).ToArray()) == normalizedInput);
            
            return Json(new { exists = exists });
        }

        [HttpGet]
        public async Task<IActionResult> CheckNameExists(string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                return Json(new { exists = false });
            }

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.FirstName == firstName && u.LastName == lastName);
            return Json(new { exists = user != null });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Validate CAPTCHA
            var sessionCode = HttpContext.Session.GetString("CaptchaCode");
            if (string.IsNullOrEmpty(sessionCode) || !_captchaService.ValidateCaptcha(model.CaptchaCode ?? "", sessionCode))
            {
                ModelState.AddModelError("CaptchaCode", "Invalid CAPTCHA code. Please try again.");
                // Regenerate CAPTCHA
                var (imageBytes, code) = _captchaService.GenerateCaptcha();
                HttpContext.Session.SetString("CaptchaCode", code);
                ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
                return View(model);
            }

            // Custom validation: Check for duplicates before creating user
            if (ModelState.IsValid)
            {
                // Check if email already exists
                var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existingUserByEmail != null)
                {
                    ModelState.AddModelError("Email", "This email is already registered. Please use a different email or try logging in.");
                }

                // Check if phone number already exists (if provided)
                // Normalize phone number for comparison (remove all non-digit characters)
                if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
                {
                    var normalizedInput = new string(model.PhoneNumber.Where(char.IsDigit).ToArray());
                    
                    // Check against all users, normalizing their phone numbers too
                    var allUsers = await _userManager.Users.ToListAsync();
                    var existingUserByPhone = allUsers.FirstOrDefault(u => 
                        !string.IsNullOrWhiteSpace(u.PhoneNumber) && 
                        new string(u.PhoneNumber.Where(char.IsDigit).ToArray()) == normalizedInput);
                    
                    if (existingUserByPhone != null)
                    {
                        ModelState.AddModelError("PhoneNumber", "This phone number is already registered. Please use a different phone number.");
                    }
                }

                // Check if first name + last name combination already exists
                if (!string.IsNullOrWhiteSpace(model.FirstName) && !string.IsNullOrWhiteSpace(model.LastName))
                {
                    var existingUserByName = await _userManager.Users
                        .FirstOrDefaultAsync(u => u.FirstName == model.FirstName && u.LastName == model.LastName);
                    if (existingUserByName != null)
                    {
                        ModelState.AddModelError("FirstName", "A user with this first name and last name combination already exists. Please use a different name.");
                        ModelState.AddModelError("LastName", "");
                    }
                }

                // If validation failed, regenerate CAPTCHA and return
                if (!ModelState.IsValid)
                {
                    try
                    {
                        var (imageBytes, code) = _captchaService.GenerateCaptcha();
                        HttpContext.Session.SetString("CaptchaCode", code);
                        ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error regenerating CAPTCHA: {Error}", ex.Message);
                        ViewBag.CaptchaImage = string.Empty;
                    }
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    DateOfBirth = model.DateOfBirth,
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    ZipCode = model.ZipCode,
                    LicenseNumber = model.LicenseNumber,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Assign Customer role
                    if (!await _roleManager.RoleExistsAsync("Customer"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Customer"));
                    }
                    await _userManager.AddToRoleAsync(user, "Customer");

                    // Auto-confirm email if email confirmation is not required
                    if (!_signInManager.Options.SignIn.RequireConfirmedEmail)
                    {
                        user.EmailConfirmed = true;
                        await _userManager.UpdateAsync(user);
                        TempData["SuccessMessage"] = "Registration successful! Welcome to DriveLuxe.";
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        // Generate email confirmation token
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var callbackUrl = Url.Action(
                            nameof(ConfirmEmail),
                            "Account",
                            new { userId = user.Id, token = token },
                            protocol: Request.Scheme)!;

                        try
                        {
                            var emailSent = await _emailService.SendEmailConfirmationAsync(user.Email!, callbackUrl);
                            var isDev = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
                            
                            // Always show link if email was not sent OR in development mode
                            // This ensures users can always confirm their email even if SMTP is not configured
                            if (!emailSent || isDev)
                            {
                                TempData["EmailConfirmationUrl"] = callbackUrl;
                                TempData["DevMode"] = true;
                                if (!emailSent)
                                {
                                    TempData["ErrorMessage"] = "Email service not configured. Email saved to file. Use the link below to confirm your email.";
                                }
                                else if (isDev)
                                {
                                    TempData["InfoMessage"] = "Development mode: Email link is shown below for testing.";
                                }
                            }
                            
                            return RedirectToAction(nameof(EmailConfirmationInfo));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending confirmation email: {Error}", ex.Message);
                            
                            // Always show the link if email fails - this helps in both dev and production
                            // Users should not be blocked from confirming email if SMTP fails
                            TempData["EmailConfirmationUrl"] = callbackUrl;
                            TempData["DevMode"] = true;
                            var isDev = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
                            if (isDev)
                            {
                                TempData["ErrorMessage"] = "Email service not configured. Use the link below to confirm your email.";
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Email sending failed. Please use the link below to confirm your email, or contact administrator.";
                            }
                            return RedirectToAction(nameof(EmailConfirmationInfo));
                        }
                    }
                }

                foreach (var error in result.Errors)
                {
                    // Customize error messages for better UX
                    if (error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail")
                    {
                        ModelState.AddModelError("Email", "This email is already registered. Please use a different email or try logging in.");
                    }
                    else if (error.Code == "PasswordTooShort")
                    {
                        ModelState.AddModelError("Password", "Password must be at least 6 characters long.");
                    }
                    else if (error.Code == "PasswordRequiresDigit")
                    {
                        ModelState.AddModelError("Password", "Password must contain at least one digit.");
                    }
                    else if (error.Code == "PasswordRequiresUpper")
                    {
                        ModelState.AddModelError("Password", "Password must contain at least one uppercase letter.");
                    }
                    else if (error.Code == "PasswordRequiresLower")
                    {
                        ModelState.AddModelError("Password", "Password must contain at least one lowercase letter.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                
                // Regenerate CAPTCHA on failed registration
                try
                {
                    var (imageBytes, code) = _captchaService.GenerateCaptcha();
                    HttpContext.Session.SetString("CaptchaCode", code);
                    ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error regenerating CAPTCHA: {Error}", ex.Message);
                    ViewBag.CaptchaImage = string.Empty;
                }
            }
            
            // Ensure CAPTCHA is available even if there were no errors
            if (string.IsNullOrEmpty(ViewBag.CaptchaImage as string))
            {
                try
                {
                    var (imageBytes, code) = _captchaService.GenerateCaptcha();
                    HttpContext.Session.SetString("CaptchaCode", code);
                    ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating CAPTCHA: {Error}", ex.Message);
                    ViewBag.CaptchaImage = string.Empty;
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Login));
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Email confirmed successfully! You can now login.";
                return RedirectToAction(nameof(Login));
            }

            TempData["ErrorMessage"] = "Email confirmation failed. The link may have expired.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult EmailConfirmationInfo()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            // Clear any previous login attempt
            HttpContext.Session.Remove("LoginCaptchaCode");
            
            // Generate new CAPTCHA for each login page load
            try
            {
                var (imageBytes, code) = _captchaService.GenerateCaptcha();
                HttpContext.Session.SetString("LoginCaptchaCode", code);
                ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating CAPTCHA: {Error}", ex.Message);
                // Set a default value to prevent view errors
                ViewBag.CaptchaImage = string.Empty;
            }
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Validate CAPTCHA
            var sessionCode = HttpContext.Session.GetString("LoginCaptchaCode");
            if (string.IsNullOrEmpty(sessionCode) || !_captchaService.ValidateCaptcha(model.CaptchaCode ?? "", sessionCode))
            {
                ModelState.AddModelError("CaptchaCode", "Invalid CAPTCHA code. Please try again.");
                // Regenerate CAPTCHA
                var (imageBytes, code) = _captchaService.GenerateCaptcha();
                HttpContext.Session.SetString("LoginCaptchaCode", code);
                ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
                
                // Get lockout info
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && await _userManager.IsLockedOutAsync(user))
                {
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    if (lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow)
                    {
                        var remainingTime = lockoutEnd.Value - DateTimeOffset.UtcNow;
                        ModelState.AddModelError(string.Empty, 
                            $"Account is locked. Please try again after {remainingTime.Minutes} minutes.");
                    }
                }
                
                return View(model);
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                // Only check email confirmation if it's required
                if (user != null && !user.EmailConfirmed && _signInManager.Options.SignIn.RequireConfirmedEmail)
                {
                    ModelState.AddModelError(string.Empty, "Please confirm your email address first. Check your email for the confirmation link.");
                    return View(model);
                }

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // Clear CAPTCHA on successful login
                    HttpContext.Session.Remove("LoginCaptchaCode");
                    
                    // Log successful login
                    _logger.LogInformation("User {Email} logged in successfully.", model.Email);
                    
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    
                    // Redirect based on user role
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Staff") || roles.Contains("Manager"))
                    {
                        return RedirectToAction("Dashboard", "Staff");
                    }
                    
                    return RedirectToAction("Index", "Home");
                }
                else if (result.IsLockedOut)
                {
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user!);
                    if (lockoutEnd.HasValue)
                    {
                        var remainingTime = lockoutEnd.Value - DateTimeOffset.UtcNow;
                        ModelState.AddModelError(string.Empty, 
                            $"Account has been locked due to too many failed login attempts. Please try again after {remainingTime.Minutes} minutes and {remainingTime.Seconds} seconds.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Account has been locked. Please try again later.");
                    }
                }
                else if (result.IsNotAllowed)
                {
                    ModelState.AddModelError(string.Empty, "Login is not allowed. Please confirm your email address first.");
                }
                else if (result.RequiresTwoFactor)
                {
                    // Handle two-factor authentication if needed in the future
                    ModelState.AddModelError(string.Empty, "Two-factor authentication is required.");
                }
                else
                {
                    // Get remaining attempts
                    if (user != null)
                    {
                        var accessFailedCount = await _userManager.GetAccessFailedCountAsync(user);
                        var maxAttempts = _signInManager.Options.Lockout.MaxFailedAccessAttempts;
                        var remainingAttempts = maxAttempts - accessFailedCount - 1;
                        
                        if (remainingAttempts > 0)
                        {
                            ModelState.AddModelError(string.Empty, 
                                $"Invalid email or password. You have {remainingAttempts} attempt(s) remaining before your account is locked.");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, 
                                "Invalid email or password. Your account has been locked due to too many failed attempts.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Invalid email or password. Please check your credentials and try again.");
                    }
                }
                
                // Regenerate CAPTCHA on failed login
                try
                {
                    var (imageBytes, code) = _captchaService.GenerateCaptcha();
                    HttpContext.Session.SetString("LoginCaptchaCode", code);
                    ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error regenerating CAPTCHA: {Error}", ex.Message);
                    ViewBag.CaptchaImage = string.Empty;
                }
            }

            // Ensure CAPTCHA is available even if there were no errors
            if (string.IsNullOrEmpty(ViewBag.CaptchaImage as string))
            {
                try
                {
                    var (imageBytes, code) = _captchaService.GenerateCaptcha();
                    HttpContext.Session.SetString("LoginCaptchaCode", code);
                    ViewBag.CaptchaImage = Convert.ToBase64String(imageBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating CAPTCHA: {Error}", ex.Message);
                    ViewBag.CaptchaImage = string.Empty;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    TempData["SuccessMessage"] = "If the email is registered and confirmed, we have sent a password reset link.";
                    return RedirectToAction(nameof(ForgotPasswordConfirmation));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action(
                    nameof(ResetPassword),
                    "Account",
                    new { token = Uri.EscapeDataString(token), email = user.Email },
                    protocol: Request.Scheme)!;

                try
                {
                    var emailSent = await _emailService.SendPasswordResetAsync(user.Email!, callbackUrl);
                    var isDev = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
                    
                    // Always show link if email was not sent OR in development mode
                    // This ensures users can always reset password even if SMTP is not configured
                    if (!emailSent || isDev)
                    {
                        TempData["PasswordResetUrl"] = callbackUrl;
                        TempData["DevMode"] = true;
                        if (!emailSent)
                        {
                            TempData["ErrorMessage"] = "Email service not configured. Email saved to file. Use the link below to reset your password.";
                        }
                        else if (isDev)
                        {
                            TempData["InfoMessage"] = "Development mode: Password reset link is shown below for testing.";
                            TempData["SuccessMessage"] = "If the email is registered, we have sent a password reset link.";
                        }
                        else
                        {
                            TempData["SuccessMessage"] = "If the email is registered, we have sent a password reset link.";
                        }
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "If the email is registered, we have sent a password reset link.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending password reset email: {Error}", ex.Message);
                    
                    // Always show the link if email fails - this helps in both dev and production
                    // Users should not be blocked from resetting password if SMTP fails
                    TempData["PasswordResetUrl"] = callbackUrl;
                    TempData["DevMode"] = true;
                    var isDev = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
                    if (isDev)
                    {
                        TempData["ErrorMessage"] = "Email service not configured. Use the link below to reset your password.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Email sending failed. Please use the link below to reset your password, or contact administrator.";
                    }
                }

                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? token, string? email)
        {
            if (token == null || email == null)
            {
                TempData["ErrorMessage"] = "Invalid password reset link.";
                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload model with token and email to preserve them
                model.Token = Request.Form["Token"].ToString();
                model.Email = Request.Form["Email"].ToString();
                return View(model);
            }

            // Validate token and email are present
            if (string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.Email))
            {
                TempData["ErrorMessage"] = "Invalid password reset link. Please request a new password reset.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that user doesn't exist for security reasons
                TempData["SuccessMessage"] = "If an account exists with that email, the password has been reset. You can now login with your new password.";
                return RedirectToAction(nameof(Login));
            }

            // Validate password and confirm password match
            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                return View(model);
            }

            // Reset password
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                // Log the user out if they're logged in (they might have clicked reset link while logged in)
                await _signInManager.SignOutAsync();
                
                TempData["SuccessMessage"] = "Password reset successfully! You can now login with your new password.";
                return RedirectToAction(nameof(Login));
            }

            // Handle errors
            foreach (var error in result.Errors)
            {
                if (error.Code == "InvalidToken")
                {
                    ModelState.AddModelError(string.Empty, "The password reset link is invalid or has expired. Please request a new password reset.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Address = user.Address,
                City = user.City,
                State = user.State,
                ZipCode = user.ZipCode,
                LicenseNumber = user.LicenseNumber,
                ProfilePictureUrl = user.ProfilePictureUrl
            };

            ViewBag.MalaysianStates = GetMalaysianStates();
            
            // Add user statistics for dashboard
            if (!User.IsInRole("Staff") && !User.IsInRole("Manager"))
            {
                var totalBookings = _context.Appointments.Count(a => a.UserId == user.Id && a.Status != "Cancelled");
                var completedBookings = _context.Appointments.Count(a => a.UserId == user.Id && a.Status == "Completed");
                var totalSpent = _context.Appointments
                    .Where(a => a.UserId == user.Id && (a.Status == "Confirmed" || a.Status == "Completed"))
                    .Sum(a => (decimal?)a.TotalPrice) ?? 0;
                
                ViewBag.TotalBookings = totalBookings;
                ViewBag.CompletedBookings = completedBookings;
                ViewBag.TotalSpent = totalSpent;
            }
            
            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model, IFormFile? profilePicture)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.MalaysianStates = GetMalaysianStates();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Handle profile picture upload
            if (profilePicture != null && profilePicture.Length > 0)
            {
                try
                {
                    // Delete old picture if exists
                    if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                    {
                        await _fileUploadService.DeleteFileAsync(user.ProfilePictureUrl);
                    }

                    var pictureUrl = await _fileUploadService.UploadProfilePictureAsync(profilePicture, user.Id);
                    user.ProfilePictureUrl = pictureUrl;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("ProfilePicture", ex.Message);
                    ViewBag.MalaysianStates = GetMalaysianStates();
                    return View(model);
                }
            }

            // Update user properties
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.DateOfBirth = model.DateOfBirth;
            user.Address = model.Address;
            user.City = model.City;
            user.State = model.State;
            user.ZipCode = model.ZipCode;
            user.LicenseNumber = model.LicenseNumber;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.MalaysianStates = GetMalaysianStates();
            return View(model);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private List<string> GetMalaysianStates()
        {
            return new List<string>
            {
                "Johor", "Kedah", "Kelantan", "Kuala Lumpur", "Labuan", "Malacca",
                "Negeri Sembilan", "Pahang", "Penang", "Perak", "Perlis", "Putrajaya",
                "Sabah", "Sarawak", "Selangor", "Terengganu"
            };
        }
    }

    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "City")]
        public string? City { get; set; }

        [Display(Name = "State")]
        public string? State { get; set; }

        [Display(Name = "Zip Code")]
        public string? ZipCode { get; set; }

        [Display(Name = "Driver's License Number")]
        public string? LicenseNumber { get; set; }

        [Required(ErrorMessage = "CAPTCHA code is required")]
        [Display(Name = "CAPTCHA Code")]
        public string? CaptchaCode { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        [Required(ErrorMessage = "CAPTCHA code is required")]
        [Display(Name = "CAPTCHA Code")]
        public string? CaptchaCode { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "The password must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "City")]
        public string? City { get; set; }

        [Display(Name = "State")]
        public string? State { get; set; }

        [Display(Name = "Zip Code")]
        public string? ZipCode { get; set; }

        [Display(Name = "License Number")]
        public string? LicenseNumber { get; set; }

        [Display(Name = "Profile Picture")]
        public string? ProfilePictureUrl { get; set; }

        [Display(Name = "Upload Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }
    }
}

