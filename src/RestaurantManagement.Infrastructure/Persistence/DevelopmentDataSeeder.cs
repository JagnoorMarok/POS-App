using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Domain.Entities;

namespace RestaurantManagement.Infrastructure.Persistence;

/// <summary>
/// Seeds initial default development/demo data into empty database tables.
/// </summary>
public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(RestaurantDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await context.Categories.AnyAsync(cancellationToken))
        {
            logger.LogDebug("Database already contains category data. Skipping seed.");
            return;
        }

        logger.LogInformation("Seeding initial development restaurant data...");

        var now = DateTime.UtcNow;

        // 1. Categories
        var starters = new Category(Guid.NewGuid(), "Starters", "Appetizers and quick bites", 1, now);
        var mainCourse = new Category(Guid.NewGuid(), "Main Course", "Hearty main dishes and curries", 2, now);
        var breads = new Category(Guid.NewGuid(), "Breads & Rice", "Tandoori breads and aromatic rice", 3, now);
        var desserts = new Category(Guid.NewGuid(), "Desserts", "Traditional sweet delicacies", 4, now);
        var beverages = new Category(Guid.NewGuid(), "Beverages", "Refreshing drinks and teas", 5, now);

        context.Categories.AddRange(starters, mainCourse, breads, desserts, beverages);

        // 2. Products
        var products = new List<Product>
        {
            new(Guid.NewGuid(), starters.Id, "Paneer Tikka", "Charcoal-grilled cottage cheese with bell peppers and spices", 220.00m, now, displayOrder: 1),
            new(Guid.NewGuid(), starters.Id, "Crispy Corn", "Golden fried sweet corn tossed with herbs and chili", 180.00m, now, displayOrder: 2),
            new(Guid.NewGuid(), mainCourse.Id, "Butter Chicken", "Tender chicken cooked in rich makhani tomato cream gravy", 280.00m, now, displayOrder: 1),
            new(Guid.NewGuid(), mainCourse.Id, "Dal Makhani", "Slow-cooked black lentils with butter and cream", 200.00m, now, displayOrder: 2),
            new(Guid.NewGuid(), mainCourse.Id, "Paneer Butter Masala", "Cottage cheese cubes in aromatic spiced tomato sauce", 240.00m, now, displayOrder: 3),
            new(Guid.NewGuid(), breads.Id, "Butter Naan", "Soft leavened tandoor flatbread brushed with butter", 45.00m, now, displayOrder: 1),
            new(Guid.NewGuid(), breads.Id, "Garlic Naan", "Tandoor flatbread topped with minced garlic and cilantro", 55.00m, now, displayOrder: 2),
            new(Guid.NewGuid(), breads.Id, "Jeera Rice", "Basmati rice tempered with roasted cumin seeds", 140.00m, now, displayOrder: 3),
            new(Guid.NewGuid(), desserts.Id, "Gulab Jamun (2 pcs)", "Warm milk-solid dumplings soaked in rose cardamom syrup", 80.00m, now, displayOrder: 1),
            new(Guid.NewGuid(), desserts.Id, "Rasmalai (2 pcs)", "Soft paneer patties steeped in sweetened saffron milk", 100.00m, now, displayOrder: 2),
            new(Guid.NewGuid(), beverages.Id, "Mango Lassi", "Chilled yogurt smoothie with alphonso mango pulp", 90.00m, now, displayOrder: 1),
            new(Guid.NewGuid(), beverages.Id, "Masala Chai", "Freshly brewed spiced Indian tea", 40.00m, now, displayOrder: 2),
            new(Guid.NewGuid(), beverages.Id, "Fresh Lime Soda", "Refreshing lemon soda served sweet or salted", 60.00m, now, displayOrder: 3)
        };

        context.Products.AddRange(products);

        // 3. Restaurant Tables
        var tables = new List<RestaurantTable>
        {
            new(Guid.NewGuid(), "Table 1", 2, now, displayOrder: 1),
            new(Guid.NewGuid(), "Table 2", 2, now, displayOrder: 2),
            new(Guid.NewGuid(), "Table 3", 4, now, displayOrder: 3),
            new(Guid.NewGuid(), "Table 4", 4, now, displayOrder: 4),
            new(Guid.NewGuid(), "Table 5", 6, now, displayOrder: 5),
            new(Guid.NewGuid(), "Table 6", 8, now, displayOrder: 6)
        };

        context.RestaurantTables.AddRange(tables);

        // 4. Restaurant Profile
        var profile = new RestaurantProfile(
            Guid.NewGuid(),
            "The Grand Spice Restaurant",
            now,
            address: "123 Gourmet Boulevard, Food District",
            phoneNumber: "+91 98765 43210",
            currencyCode: "INR",
            gstOrTaxNumber: "27AABCU9603R1ZM");

        context.RestaurantProfiles.Add(profile);

        // 5. Development Employee Accounts
        var hasher = new Security.PasswordHasher();
        var employees = new List<Employee>
        {
            new(Guid.NewGuid(), "admin", "System Administrator", hasher.HashPassword("Admin@123"), Domain.Enums.EmployeeRole.Administrator, now),
            new(Guid.NewGuid(), "manager", "Restaurant Manager", hasher.HashPassword("Manager@123"), Domain.Enums.EmployeeRole.Manager, now),
            new(Guid.NewGuid(), "cashier", "Front Desk Cashier", hasher.HashPassword("Cashier@123"), Domain.Enums.EmployeeRole.Cashier, now),
            new(Guid.NewGuid(), "chef", "Head Chef", hasher.HashPassword("Chef@123"), Domain.Enums.EmployeeRole.KitchenStaff, now)
        };

        context.Employees.AddRange(employees);

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Initial development data seeded successfully (5 Categories, {ProductCount} Products, {TableCount} Tables, {EmployeeCount} Employees).",
            products.Count, tables.Count, employees.Count);
    }
}
