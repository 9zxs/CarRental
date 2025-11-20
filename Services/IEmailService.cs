namespace CarRentalSystem.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string email, string subject, string message);
        Task<bool> SendEmailConfirmationAsync(string email, string callbackUrl);
        Task<bool> SendPasswordResetAsync(string email, string callbackUrl);
    }
}

