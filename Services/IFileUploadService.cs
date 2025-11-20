namespace CarRentalSystem.Services
{
    public interface IFileUploadService
    {
        Task<string> UploadProfilePictureAsync(IFormFile file, string userId);
        Task<string> UploadVehicleImageAsync(IFormFile file, int vehicleId);
        Task<bool> DeleteFileAsync(string filePath);
    }
}

