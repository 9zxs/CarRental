using CarRentalSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            try
            {
                var context = serviceProvider.GetRequiredService<CarRentalDbContext>();
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Ensure database is created
                try
                {
                    await context.Database.EnsureCreatedAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Database EnsureCreated failed: {ex.Message}");
                    return; // Exit early if database can't be created
                }

                // Check if already seeded
                bool alreadySeeded = false;
                try
                {
                    alreadySeeded = context.Cars.Any() || context.Subscriptions.Any() || context.Promotions.Any();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Error checking existing data: {ex.Message}");
                    // Continue to try seeding anyway
                }

                if (alreadySeeded)
                {
                    // Check if default accounts exist, if not create them
                    try
                    {
                        await SeedDefaultAccountsAsync(userManager, roleManager);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SeedData] Error seeding default accounts: {ex.Message}");
                    }
                    return;
                }

                // Seed in order with error handling
                try
                {
                    await SeedRolesAsync(roleManager);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Error seeding roles: {ex.Message}");
                }

                try
                {
                    await SeedDefaultAccountsAsync(userManager, roleManager);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Error seeding default accounts: {ex.Message}");
                }

                try
                {
                    await SeedCategoriesAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Error seeding categories: {ex.Message}");
                }

                try
                {
                    await SeedCarsAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Error seeding cars: {ex.Message}");
                }

                try
                {
                    await SeedSubscriptionsAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Error seeding subscriptions: {ex.Message}");
                }

                try
                {
                    await SeedPromotionsAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeedData] Error seeding promotions: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SeedData] Critical error in InitializeAsync: {ex.Message}");
                Console.WriteLine($"[SeedData] Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            var roles = new[] { "Customer", "Staff", "Manager" };
            
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        private static async Task SeedDefaultAccountsAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Create Manager Account
            var managerEmail = "manager@driveluxe.com";
            var manager = await userManager.FindByEmailAsync(managerEmail);
            if (manager == null)
            {
                manager = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "Manager",
                    PhoneNumber = "+60123456789",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(manager, "Manager123!");
                if (result.Succeeded)
                {
                    if (!await roleManager.RoleExistsAsync("Manager"))
                    {
                        await roleManager.CreateAsync(new IdentityRole("Manager"));
                    }
                    await userManager.AddToRoleAsync(manager, "Manager");
                }
            }

            // Create Staff Account
            var staffEmail = "staff@driveluxe.com";
            var staff = await userManager.FindByEmailAsync(staffEmail);
            if (staff == null)
            {
                staff = new ApplicationUser
                {
                    UserName = staffEmail,
                    Email = staffEmail,
                    EmailConfirmed = true,
                    FirstName = "John",
                    LastName = "Staff",
                    PhoneNumber = "+60123456790",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(staff, "Staff123!");
                if (result.Succeeded)
                {
                    if (!await roleManager.RoleExistsAsync("Staff"))
                    {
                        await roleManager.CreateAsync(new IdentityRole("Staff"));
                    }
                    await userManager.AddToRoleAsync(staff, "Staff");
                }
            }
        }

        private static async Task SeedCategoriesAsync(CarRentalDbContext context)
        {
            var categories = new[]
            {
                new Category { Name = "SUV", Description = "Sport Utility Vehicles", IsActive = true },
                new Category { Name = "Sedan", Description = "Four-door passenger cars", IsActive = true },
                new Category { Name = "Hatchback", Description = "Compact cars with rear hatch", IsActive = true },
                new Category { Name = "Coupe", Description = "Two-door sports cars", IsActive = true },
                new Category { Name = "Convertible", Description = "Open-top vehicles", IsActive = true },
                new Category { Name = "Truck", Description = "Pickup trucks and utility vehicles", IsActive = true },
                new Category { Name = "Van", Description = "Large passenger or cargo vehicles", IsActive = true }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }

        private static async Task SeedCarsAsync(CarRentalDbContext context)
        {
            var sedanCategory = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Sedan");
            var suvCategory = await context.Categories.FirstOrDefaultAsync(c => c.Name == "SUV");
            var hatchbackCategory = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Hatchback");

            var cars = new[]
            {
                new Car
                {
                    Make = "Tesla",
                    Model = "Model 3",
                    Year = 2023,
                    LicensePlate = "EV-001",
                    Color = "Pearl White",
                    DailyRate = 89.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 75,
                    Range = 358,
                    ChargingTime = 30,
                    Description = "Premium electric sedan with autopilot features",
                    ImageUrl = "https://images.unsplash.com/photo-1560957173-2c5b8e2e0b3a?w=800",
                    IsAvailable = true,
                    State = "Kuala Lumpur",
                    City = "Kuala Lumpur",
                    CategoryId = sedanCategory?.Id ?? 1
                },
                new Car
                {
                    Make = "Tesla",
                    Model = "Model Y",
                    Year = 2024,
                    LicensePlate = "EV-002",
                    Color = "Midnight Silver",
                    DailyRate = 99.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 75,
                    Range = 330,
                    ChargingTime = 30,
                    Description = "Spacious electric SUV perfect for families",
                    ImageUrl = "https://images.unsplash.com/photo-1606664515524-ed2f786a0bd6?w=800",
                    IsAvailable = true,
                    State = "Penang",
                    City = "George Town",
                    CategoryId = suvCategory?.Id ?? 2
                },
                new Car
                {
                    Make = "Nissan",
                    Model = "Leaf",
                    Year = 2023,
                    LicensePlate = "EV-003",
                    Color = "Blue",
                    DailyRate = 69.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 60,
                    Range = 226,
                    ChargingTime = 40,
                    Description = "Affordable and reliable electric vehicle",
                    ImageUrl = "https://images.unsplash.com/photo-1617788138017-80ad40651399?w=800",
                    IsAvailable = true,
                    State = "Selangor",
                    City = "Petaling Jaya",
                    CategoryId = hatchbackCategory?.Id ?? 3
                },
                new Car
                {
                    Make = "Toyota",
                    Model = "Camry",
                    Year = 2023,
                    LicensePlate = "GAS-001",
                    Color = "Black",
                    DailyRate = 59.99m,
                    FuelType = "Gas",
                    IsElectric = false,
                    Description = "Comfortable and fuel-efficient sedan",
                    ImageUrl = "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?w=800",
                    IsAvailable = true,
                    State = "Kuala Lumpur",
                    City = "Kuala Lumpur",
                    CategoryId = sedanCategory?.Id ?? 1
                },
                new Car
                {
                    Make = "Honda",
                    Model = "Civic",
                    Year = 2023,
                    LicensePlate = "GAS-002",
                    Color = "Red",
                    DailyRate = 49.99m,
                    FuelType = "Gas",
                    IsElectric = false,
                    Description = "Compact and economical car",
                    ImageUrl = "https://images.unsplash.com/photo-1606664515524-ed2f786a0bd6?w=800",
                    IsAvailable = true,
                    State = "Penang",
                    City = "Butterworth",
                    CategoryId = hatchbackCategory?.Id ?? 3
                },
                new Car
                {
                    Make = "BMW",
                    Model = "iX",
                    Year = 2024,
                    LicensePlate = "EV-004",
                    Color = "Phytonic Blue",
                    DailyRate = 149.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 111,
                    Range = 324,
                    ChargingTime = 35,
                    Description = "Luxury electric SUV with cutting-edge technology",
                    ImageUrl = "https://images.unsplash.com/photo-1617788138017-80ad40651399?w=800",
                    IsAvailable = true,
                    State = "Selangor",
                    City = "Shah Alam",
                    CategoryId = suvCategory?.Id ?? 2
                }
            };

            context.Cars.AddRange(cars);
            await context.SaveChangesAsync();
        }

        private static async Task SeedSubscriptionsAsync(CarRentalDbContext context)
        {
            var subscriptions = new[]
            {
                new Subscription
                {
                    Name = "Basic Plan",
                    Description = "Perfect for occasional renters",
                    MonthlyPrice = 29.99m,
                    DiscountPercentage = 5,
                    MaxRentalsPerMonth = 2,
                    MaxDaysPerRental = 7,
                    IncludesEVPriority = false,
                    IsActive = true
                },
                new Subscription
                {
                    Name = "Premium Plan",
                    Description = "Great for frequent renters with EV priority",
                    MonthlyPrice = 59.99m,
                    DiscountPercentage = 15,
                    MaxRentalsPerMonth = 5,
                    MaxDaysPerRental = 14,
                    IncludesEVPriority = true,
                    IsActive = true
                },
                new Subscription
                {
                    Name = "Elite Plan",
                    Description = "Unlimited rentals with maximum benefits",
                    MonthlyPrice = 99.99m,
                    DiscountPercentage = 25,
                    MaxRentalsPerMonth = 999,
                    MaxDaysPerRental = 30,
                    IncludesEVPriority = true,
                    IsActive = true
                }
            };

            context.Subscriptions.AddRange(subscriptions);
            await context.SaveChangesAsync();
        }

        private static async Task SeedPromotionsAsync(CarRentalDbContext context)
        {
            var promotions = new[]
            {
                new Promotion
                {
                    Name = "Summer Special",
                    Description = "Get 20% off on all rentals",
                    Code = "SUMMER20",
                    DiscountPercentage = 20,
                    MaxDiscountAmount = 100m,
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    EndDate = DateTime.UtcNow.AddDays(60),
                    IsActive = true,
                    IsEVOnly = false,
                    MaxUses = 1000,
                    CurrentUses = 0
                },
                new Promotion
                {
                    Name = "EV Weekend",
                    Description = "30% off on electric vehicles for weekend rentals",
                    Code = "EVWEEKEND",
                    DiscountPercentage = 30,
                    MaxDiscountAmount = 150m,
                    StartDate = DateTime.UtcNow.AddDays(-7),
                    EndDate = DateTime.UtcNow.AddDays(30),
                    IsActive = true,
                    IsEVOnly = true,
                    MaxUses = 500,
                    CurrentUses = 0
                },
                new Promotion
                {
                    Name = "New Customer",
                    Description = "Welcome offer for new customers",
                    Code = "WELCOME15",
                    DiscountPercentage = 15,
                    MaxDiscountAmount = 75m,
                    StartDate = DateTime.UtcNow.AddDays(-90),
                    EndDate = DateTime.UtcNow.AddDays(90),
                    IsActive = true,
                    IsEVOnly = false,
                    MaxUses = null,
                    CurrentUses = 0
                }
            };

            context.Promotions.AddRange(promotions);
            await context.SaveChangesAsync();
        }

        // Legacy method for backwards compatibility
        public static void Initialize(CarRentalDbContext context)
        {
            // This method is kept for backwards compatibility but should use InitializeAsync
            if (context.Cars.Any() || context.Subscriptions.Any() || context.Promotions.Any())
            {
                return;
            }

            // Seed Categories
            var categories = new[]
            {
                new Category { Name = "SUV", Description = "Sport Utility Vehicles", IsActive = true },
                new Category { Name = "Sedan", Description = "Four-door passenger cars", IsActive = true },
                new Category { Name = "Hatchback", Description = "Compact cars with rear hatch", IsActive = true },
                new Category { Name = "Coupe", Description = "Two-door sports cars", IsActive = true },
                new Category { Name = "Convertible", Description = "Open-top vehicles", IsActive = true },
                new Category { Name = "Truck", Description = "Pickup trucks and utility vehicles", IsActive = true },
                new Category { Name = "Van", Description = "Large passenger or cargo vehicles", IsActive = true }
            };

            context.Categories.AddRange(categories);
            context.SaveChanges();

            var sedanCategory = categories.First(c => c.Name == "Sedan");
            var suvCategory = categories.First(c => c.Name == "SUV");
            var hatchbackCategory = categories.First(c => c.Name == "Hatchback");

            // Seed Cars with Malaysian states
            var cars = new[]
            {
                new Car
                {
                    Make = "Tesla",
                    Model = "Model 3",
                    Year = 2023,
                    LicensePlate = "EV-001",
                    Color = "Pearl White",
                    DailyRate = 89.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 75,
                    Range = 358,
                    ChargingTime = 30,
                    Description = "Premium electric sedan with autopilot features",
                    ImageUrl = "https://images.unsplash.com/photo-1560957173-2c5b8e2e0b3a?w=800",
                    IsAvailable = true,
                    State = "Kuala Lumpur",
                    City = "Kuala Lumpur"
                },
                new Car
                {
                    Make = "Tesla",
                    Model = "Model Y",
                    Year = 2024,
                    LicensePlate = "EV-002",
                    Color = "Midnight Silver",
                    DailyRate = 99.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 75,
                    Range = 330,
                    ChargingTime = 30,
                    Description = "Spacious electric SUV perfect for families",
                    ImageUrl = "https://images.unsplash.com/photo-1606664515524-ed2f786a0bd6?w=800",
                    IsAvailable = true,
                    State = "Penang",
                    City = "George Town"
                },
                new Car
                {
                    Make = "Nissan",
                    Model = "Leaf",
                    Year = 2023,
                    LicensePlate = "EV-003",
                    Color = "Blue",
                    DailyRate = 69.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 60,
                    Range = 226,
                    ChargingTime = 40,
                    Description = "Affordable and reliable electric vehicle",
                    ImageUrl = "https://images.unsplash.com/photo-1617788138017-80ad40651399?w=800",
                    IsAvailable = true,
                    State = "Selangor",
                    City = "Petaling Jaya"
                },
                new Car
                {
                    Make = "Toyota",
                    Model = "Camry",
                    Year = 2023,
                    LicensePlate = "GAS-001",
                    Color = "Black",
                    DailyRate = 59.99m,
                    FuelType = "Gas",
                    IsElectric = false,
                    Description = "Comfortable and fuel-efficient sedan",
                    ImageUrl = "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?w=800",
                    IsAvailable = true,
                    State = "Kuala Lumpur",
                    City = "Kuala Lumpur"
                },
                new Car
                {
                    Make = "Honda",
                    Model = "Civic",
                    Year = 2023,
                    LicensePlate = "GAS-002",
                    Color = "Red",
                    DailyRate = 49.99m,
                    FuelType = "Gas",
                    IsElectric = false,
                    Description = "Compact and economical car",
                    ImageUrl = "https://images.unsplash.com/photo-1606664515524-ed2f786a0bd6?w=800",
                    IsAvailable = true,
                    State = "Penang",
                    City = "Butterworth"
                },
                new Car
                {
                    Make = "BMW",
                    Model = "iX",
                    Year = 2024,
                    LicensePlate = "EV-004",
                    Color = "Phytonic Blue",
                    DailyRate = 149.99m,
                    FuelType = "Electric",
                    IsElectric = true,
                    BatteryCapacity = 111,
                    Range = 324,
                    ChargingTime = 35,
                    Description = "Luxury electric SUV with cutting-edge technology",
                    ImageUrl = "https://images.unsplash.com/photo-1617788138017-80ad40651399?w=800",
                    IsAvailable = true,
                    State = "Selangor",
                    City = "Shah Alam"
                }
            };

            context.Cars.AddRange(cars);

            // Seed Subscriptions
            var subscriptions = new[]
            {
                new Subscription
                {
                    Name = "Basic Plan",
                    Description = "Perfect for occasional renters",
                    MonthlyPrice = 29.99m,
                    DiscountPercentage = 5,
                    MaxRentalsPerMonth = 2,
                    MaxDaysPerRental = 7,
                    IncludesEVPriority = false,
                    IsActive = true
                },
                new Subscription
                {
                    Name = "Premium Plan",
                    Description = "Great for frequent renters with EV priority",
                    MonthlyPrice = 59.99m,
                    DiscountPercentage = 15,
                    MaxRentalsPerMonth = 5,
                    MaxDaysPerRental = 14,
                    IncludesEVPriority = true,
                    IsActive = true
                },
                new Subscription
                {
                    Name = "Elite Plan",
                    Description = "Unlimited rentals with maximum benefits",
                    MonthlyPrice = 99.99m,
                    DiscountPercentage = 25,
                    MaxRentalsPerMonth = 999,
                    MaxDaysPerRental = 30,
                    IncludesEVPriority = true,
                    IsActive = true
                }
            };

            context.Subscriptions.AddRange(subscriptions);

            // Seed Promotions
            var promotions = new[]
            {
                new Promotion
                {
                    Name = "Summer Special",
                    Description = "Get 20% off on all rentals",
                    Code = "SUMMER20",
                    DiscountPercentage = 20,
                    MaxDiscountAmount = 100m,
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    EndDate = DateTime.UtcNow.AddDays(60),
                    IsActive = true,
                    IsEVOnly = false,
                    MaxUses = 1000,
                    CurrentUses = 0
                },
                new Promotion
                {
                    Name = "EV Weekend",
                    Description = "30% off on electric vehicles for weekend rentals",
                    Code = "EVWEEKEND",
                    DiscountPercentage = 30,
                    MaxDiscountAmount = 150m,
                    StartDate = DateTime.UtcNow.AddDays(-7),
                    EndDate = DateTime.UtcNow.AddDays(30),
                    IsActive = true,
                    IsEVOnly = true,
                    MaxUses = 500,
                    CurrentUses = 0
                },
                new Promotion
                {
                    Name = "New Customer",
                    Description = "Welcome offer for new customers",
                    Code = "WELCOME15",
                    DiscountPercentage = 15,
                    MaxDiscountAmount = 75m,
                    StartDate = DateTime.UtcNow.AddDays(-90),
                    EndDate = DateTime.UtcNow.AddDays(90),
                    IsActive = true,
                    IsEVOnly = false,
                    MaxUses = null,
                    CurrentUses = 0
                }
            };

            context.Promotions.AddRange(promotions);

            context.SaveChanges();
        }

        private static void SeedRoles(CarRentalDbContext context)
        {
            // Roles are managed by Identity, so we skip this for now
            // Roles will be created when first user is assigned to them
        }
    }
}

