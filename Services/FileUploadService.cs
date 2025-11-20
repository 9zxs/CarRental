using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace CarRentalSystem.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private const int VehicleImageWidth = 800;
        private const int VehicleImageHeight = 600;

        public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> UploadProfilePictureAsync(IFormFile file, string userId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("文件为空");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("不支持的文件格式。仅支持: " + string.Join(", ", allowedExtensions));

            // Validate file size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
                throw new ArgumentException("文件大小不能超过 5MB");

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return relative path
                return $"/uploads/profiles/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture for user {UserId}", userId);
                throw;
            }
        }

        public Task<bool> DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return Task.FromResult(false);

            try
            {
                // Remove leading slash if present
                if (filePath.StartsWith("/"))
                    filePath = filePath.Substring(1);

                var fullPath = Path.Combine(_environment.WebRootPath, filePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FilePath}", filePath);
                return Task.FromResult(false);
            }
        }

        public async Task<string> UploadVehicleImageAsync(IFormFile file, int vehicleId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("文件为空");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("不支持的文件格式。仅支持: " + string.Join(", ", allowedExtensions));

            // Validate file size (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
                throw new ArgumentException("文件大小不能超过 10MB");

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "vehicles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"vehicle_{vehicleId}_{DateTime.UtcNow:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Check if System.Drawing is available
                try
                {
                    // Resize and save image
                    using (var inputStream = file.OpenReadStream())
                    using (var outputStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ResizeAndSaveImageAsync(inputStream, outputStream, VehicleImageWidth, VehicleImageHeight);
                    }
                }
                catch (TypeInitializationException ex) when (ex.InnerException?.GetType().Name == "DllNotFoundException")
                {
                    _logger.LogWarning("System.Drawing not available, saving image without resizing: {Message}", ex.Message);
                    // Fallback: save without resizing if System.Drawing is not available
                    using (var inputStream = file.OpenReadStream())
                    using (var outputStream = new FileStream(filePath, FileMode.Create))
                    {
                        await inputStream.CopyToAsync(outputStream);
                    }
                }
                catch (PlatformNotSupportedException ex)
                {
                    _logger.LogWarning("Image resizing not supported on this platform, saving without resize: {Message}", ex.Message);
                    // Fallback: save without resizing
                    using (var inputStream = file.OpenReadStream())
                    using (var outputStream = new FileStream(filePath, FileMode.Create))
                    {
                        await inputStream.CopyToAsync(outputStream);
                    }
                }

                // Return relative path
                return $"/uploads/vehicles/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading vehicle image for vehicle {VehicleId}", vehicleId);
                throw;
            }
        }

        private async Task ResizeAndSaveImageAsync(Stream inputStream, Stream outputStream, int targetWidth, int targetHeight)
        {
            try
            {
                using (var originalImage = Image.FromStream(inputStream))
                {
                    // Calculate aspect ratio and new dimensions
                    var aspectRatio = (double)originalImage.Width / originalImage.Height;
                    int newWidth, newHeight;

                    if (aspectRatio > (double)targetWidth / targetHeight)
                    {
                        // Image is wider - fit to width
                        newWidth = targetWidth;
                        newHeight = (int)(targetWidth / aspectRatio);
                    }
                    else
                    {
                        // Image is taller - fit to height
                        newHeight = targetHeight;
                        newWidth = (int)(targetHeight * aspectRatio);
                    }

                    // Create new image with target dimensions and white background
                    using (var resizedImage = new Bitmap(targetWidth, targetHeight))
                    using (var graphics = Graphics.FromImage(resizedImage))
                    {
                        // Set high quality settings
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;

                        // Fill with white background
                        graphics.Clear(Color.White);

                        // Calculate position to center the image
                        var x = (targetWidth - newWidth) / 2;
                        var y = (targetHeight - newHeight) / 2;

                        // Draw resized image centered
                        graphics.DrawImage(originalImage, x, y, newWidth, newHeight);

                        // Save as JPEG with high quality
                        var jpegCodec = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

                        if (jpegCodec != null)
                        {
                            var encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
                            resizedImage.Save(outputStream, jpegCodec, encoderParams);
                        }
                        else
                        {
                            resizedImage.Save(outputStream, ImageFormat.Jpeg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resizing image: {Message}", ex.Message);
                throw new InvalidOperationException("Failed to process image. Please ensure the image format is supported.", ex);
            }
            await Task.CompletedTask;
        }
    }
}

