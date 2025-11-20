namespace CarRentalSystem.Services
{
    public interface ICaptchaService
    {
        (byte[] imageBytes, string code) GenerateCaptcha();
        bool ValidateCaptcha(string userInput, string sessionCode);
    }
}

