using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace CarRentalSystem.Services
{
    public class CaptchaService : ICaptchaService
    {
        private readonly Random _random = new();

        public (byte[] imageBytes, string code) GenerateCaptcha()
        {
            try
            {
                // Generate random 5-character code
                const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluding confusing characters
                var code = new string(Enumerable.Repeat(chars, 5)
                    .Select(s => s[_random.Next(s.Length)]).ToArray());

                // Create image
                var width = 150;
                var height = 50;
                using var bitmap = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(bitmap);

                // White background
                graphics.Clear(Color.White);

                // Add noise lines
                for (int i = 0; i < 5; i++)
                {
                    var pen = new Pen(Color.FromArgb(_random.Next(150, 200), _random.Next(150, 200), _random.Next(150, 200)), 1);
                    graphics.DrawLine(pen, 
                        _random.Next(width), _random.Next(height), 
                        _random.Next(width), _random.Next(height));
                }

                // Add noise dots
                for (int i = 0; i < 30; i++)
                {
                    bitmap.SetPixel(_random.Next(width), _random.Next(height), 
                        Color.FromArgb(_random.Next(150, 255), _random.Next(150, 255), _random.Next(150, 255)));
                }

                // Draw text
                var font = new Font("Arial", 24, FontStyle.Bold);
                var brush = new SolidBrush(Color.Black);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                // Add slight rotation to each character
                for (int i = 0; i < code.Length; i++)
                {
                    var charBrush = new SolidBrush(Color.FromArgb(_random.Next(50, 100), _random.Next(50, 100), _random.Next(50, 100)));
                    var charFont = new Font("Arial", _random.Next(22, 26), FontStyle.Bold);
                    var x = (width / (code.Length + 1)) * (i + 1);
                    var y = height / 2;
                    
                    graphics.TranslateTransform(x, y);
                    graphics.RotateTransform(_random.Next(-15, 15));
                    graphics.DrawString(code[i].ToString(), charFont, charBrush, 0, 0, format);
                    graphics.ResetTransform();

                    charBrush.Dispose();
                    charFont.Dispose();
                }

                // Convert to byte array
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return (ms.ToArray(), code);
            }
            catch (TypeInitializationException ex) when (ex.InnerException?.GetType().Name == "DllNotFoundException")
            {
                // System.Drawing not available - return empty image and code
                throw new PlatformNotSupportedException("CAPTCHA generation is not supported on this platform. System.Drawing.Common is required.", ex);
            }
            catch (PlatformNotSupportedException)
            {
                // Re-throw platform not supported
                throw;
            }
            catch (Exception)
            {
                // For any other error, generate a simple text-based fallback
                // Generate random code
                const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
                var code = new string(Enumerable.Repeat(chars, 5)
                    .Select(s => s[_random.Next(s.Length)]).ToArray());
                
                // Return minimal 1x1 PNG (transparent pixel) as fallback
                var minimalPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 
                    0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 
                    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 
                    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 
                    0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 
                    0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 
                    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 
                    0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 
                    0x42, 0x60, 0x82 };
                return (minimalPng, code);
            }
        }

        public bool ValidateCaptcha(string userInput, string sessionCode)
        {
            if (string.IsNullOrWhiteSpace(userInput) || string.IsNullOrWhiteSpace(sessionCode))
                return false;

            return userInput.Trim().Equals(sessionCode.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

