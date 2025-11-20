using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace CarRentalSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _environment;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
        }

        public async Task<bool> SendEmailAsync(string email, string subject, string message)
        {
            var smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:Username"] ?? "";
            var smtpPassword = _configuration["Email:Password"] ?? "";
            var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@carrental.com";
            var fromName = _configuration["Email:FromName"] ?? "Car Rental System";

            // Check if email is configured (development mode fallback)
            var isEmailConfigured = !string.IsNullOrEmpty(smtpUsername) && 
                                   !string.IsNullOrEmpty(smtpPassword) &&
                                   smtpUsername != "your-email@gmail.com" &&
                                   smtpPassword != "your-app-password";

            if (!isEmailConfigured && _environment.IsDevelopment())
            {
                // In development mode, if email is not configured, log and save to file
                _logger.LogWarning("Email not configured. Saving email to file for testing. Check wwwroot/emails folder.");
                await SaveEmailToFileAsync(email, subject, message);
                return false; // Return false to indicate email was saved to file, not sent
            }

            if (!isEmailConfigured)
            {
                _logger.LogError("Email service not configured. Please configure SMTP settings in appsettings.json");
                throw new InvalidOperationException("Email service is not configured. Please configure SMTP settings.");
            }

            try
            {
                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Timeout = 30000; // 30 seconds timeout
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(fromEmail, fromName),
                        Subject = subject,
                        Body = message,
                        IsBodyHtml = true,
                        BodyEncoding = Encoding.UTF8,
                        SubjectEncoding = Encoding.UTF8
                    };

                    mailMessage.To.Add(email);

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation("Email sent successfully to {Email}", email);
                    return true; // Email was successfully sent
                }
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP error sending email to {Email}: {Error}", email, ex.Message);
                if (_environment.IsDevelopment())
                {
                    // Fallback to file in development
                    await SaveEmailToFileAsync(email, subject, message);
                    _logger.LogWarning("Email saved to file due to SMTP error in development mode");
                    return false; // Return false to indicate email was saved to file, not sent
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Email}: {Error}", email, ex.Message);
                if (_environment.IsDevelopment())
                {
                    await SaveEmailToFileAsync(email, subject, message);
                    _logger.LogWarning("Email saved to file due to error in development mode");
                    return false; // Return false to indicate email was saved to file, not sent
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task SaveEmailToFileAsync(string email, string subject, string message)
        {
            try
            {
                var emailsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "emails");
                if (!Directory.Exists(emailsDirectory))
                {
                    Directory.CreateDirectory(emailsDirectory);
                }

                var fileName = $"email_{DateTime.UtcNow:yyyyMMddHHmmss}_{email.Replace("@", "_at_").Replace(".", "_")}.html";
                var filePath = Path.Combine(emailsDirectory, fileName);
                
                _logger.LogInformation("Email saved to file: {FilePath}", filePath);

                var emailContent = $@"
<!DOCTYPE html>
<html>
<head>
    <title>{subject}</title>
</head>
<body>
    <h2>To: {email}</h2>
    <h3>Subject: {subject}</h3>
    <hr>
    {message}
</body>
</html>";

                await File.WriteAllTextAsync(filePath, emailContent);
                _logger.LogInformation("Email saved to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving email to file");
            }
        }

        public async Task<bool> SendEmailConfirmationAsync(string email, string callbackUrl)
        {
            var subject = "Confirm Your Email - Car Rental System";
            var message = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Welcome to Car Rental System</h1>
        </div>
        <div class=""content"">
            <p>Thank you for registering! Please click the button below to confirm your email address:</p>
            <div style=""text-align: center;"">
                <a href=""{callbackUrl}"" class=""button"">Confirm Email</a>
            </div>
            <p>If the button doesn't work, copy and paste the following link into your browser:</p>
            <p style=""word-break: break-all; color: #667eea;"">{callbackUrl}</p>
            <p>This link will be valid for 24 hours.</p>
            <p>If you didn't register this account, please ignore this email.</p>
        </div>
        <div class=""footer"">
            <p>&copy; 2024 Car Rental System. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(email, subject, message);
        }

        public async Task<bool> SendPasswordResetAsync(string email, string callbackUrl)
        {
            var subject = "Password Reset - Car Rental System";
            var message = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Password Reset Request</h1>
        </div>
        <div class=""content"">
            <p>We received a request to reset your password. Please click the button below to reset your password:</p>
            <div style=""text-align: center;"">
                <a href=""{callbackUrl}"" class=""button"">Reset Password</a>
            </div>
            <div class=""warning"">
                <strong>Note:</strong> If you didn't request a password reset, please ignore this email. Your password will not be changed.
            </div>
            <p>If the button doesn't work, copy and paste the following link into your browser:</p>
            <p style=""word-break: break-all; color: #667eea;"">{callbackUrl}</p>
            <p>This link will be valid for 1 hour.</p>
        </div>
        <div class=""footer"">
            <p>&copy; 2024 Car Rental System. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(email, subject, message);
        }
    }
}

