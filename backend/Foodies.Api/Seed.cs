using Microsoft.EntityFrameworkCore;
namespace Foodies.Api;
public static class Seed {
    public static async Task Run(FoodiesDb db, bool development) {
        if (!development) return;
        if (!await db.Stores.AnyAsync()) {
            var one = new Store { CoverImageUrl = "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=80", Slug = "la-cocina-de-barrio", Name = "La Cocina de Barrio", Description = "Comida casera, hecha con calma para tus días con prisa.", AddressText = "Av. Reforma 222 · Col. Juárez", MinimumOrderAmount = 60, AveragePreparationMinutes = 10 };
            var two = new Store { CoverImageUrl = "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=80", Slug = "verde-y-grano", Name = "Verde & Grano", Description = "Ingredientes frescos, bowls y antojos que te hacen bien.", AddressText = "Londres 45 · Col. Juárez", MinimumOrderAmount = 80, AveragePreparationMinutes = 12 };
            foreach (var store in new[] { one, two }) for (int day = 0; day < 7; day++) store.Hours.Add(new() { DayOfWeek = day, OpeningTime = new(8, 0), ClosingTime = new(20, 0) });
            db.Stores.AddRange(one, two);
            var main = new Category { StoreId = one.Id, Name = "Los favoritos", SortOrder = 0 }; var drinks = new Category { StoreId = one.Id, Name = "Para acompañar", SortOrder = 1 };
            var bowls = new Category { StoreId = two.Id, Name = "Bowls y ensaladas" }; var beverages = new Category { StoreId = two.Id, Name = "Bebidas", SortOrder = 1 };
            db.Categories.AddRange(main, drinks, bowls, beverages);
            db.Dishes.AddRange(
                Make(one, main, "Bowl de pollo a la plancha", "Pollo doradito, arroz, aguacate y vegetales frescos.", 115, "photo-1546069901-ba9599a7e63c"),
                Make(one, main, "Enchiladas de la casa", "Tres enchiladas verdes con pollo, crema y queso fresco.", 98, "photo-1565299624946-b28f40a0ae38"),
                Make(one, main, "Tacos de arrachera", "Tres tacos con guacamole, cebolla y salsa de la casa.", 125, "photo-1551504734-5ee1c4a1479b"),
                Make(one, main, "Ensalada de temporada", "Hojas frescas, jitomate, semillas y aderezo de limón.", 85, "photo-1512621776951-a57141f2eefd"),
                Make(one, drinks, "Agua fresca del día", "Preparada en casa con fruta natural. Vaso de 500 ml.", 28, "photo-1513558161293-cdaf765edfd7"),
                Make(two, bowls, "Bowl mediterráneo", "Quinoa, garbanzo, pepino, hummus y hojas verdes.", 135, "photo-1512621776951-a57141f2eefd"),
                Make(two, bowls, "Bowl de salmón", "Salmón, arroz integral, aguacate y ajonjolí.", 165, "photo-1546069901-ba9599a7e63c"),
                Make(two, beverages, "Limonada de la casa", "Limón recién exprimido y un toque de hierbabuena.", 35, "photo-1513558161293-cdaf765edfd7"));
            await db.SaveChangesAsync();
        }
        var first = await db.Stores.SingleOrDefaultAsync(x => x.Slug == "la-cocina-de-barrio"); var second = await db.Stores.SingleOrDefaultAsync(x => x.Slug == "verde-y-grano");
        foreach (var (subject, name, store, role) in new[] { ("dev:customer", "Cliente de prueba", (Store?)null, StoreRole.Kitchen), ("dev:manager-one", "Ana · Encargada", first, StoreRole.Manager), ("dev:kitchen-one", "Luis · Cocina", first, StoreRole.Kitchen), ("dev:manager-two", "Sofía · Encargada", second, StoreRole.Manager) }) {
            if (await db.Users.AnyAsync(x => x.GoogleSubject == subject)) continue;
            var u = new User { GoogleSubject = subject, Email = subject.Replace(":", ".") + "@foodies.test", DisplayName = name }; db.Users.Add(u);
            if (store is not null) db.Memberships.Add(new() { UserId = u.Id, StoreId = store.Id, Role = role });
        }
        await db.SaveChangesAsync();
    }
    static Dish Make(Store s, Category c, string name, string description, decimal price, string photo) => new() { StoreId = s.Id, CategoryId = c.Id, Name = name, Description = description, Price = price, ImageUrl = $"https://images.unsplash.com/{photo}?auto=format&fit=crop&w=800&q=80" };
}
public static class Operator {
    public static async Task Run(IServiceProvider services, string[] args) {
        // dotnet run -- --operator create-store slug "Name" | assign slug email Manager|Kitchen
        var a = args.SkipWhile(x => x != "--operator").Skip(1).ToArray();
        await using var scope = services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FoodiesDb>();
        if (a.Length == 3 && a[0] == "platform-admin") {
            var email = PlatformEndpoints.Email(a[1]);
            if (string.IsNullOrWhiteSpace(a[2]) || a[2].Length > 100) throw new ArgumentException("Nombre inválido.");
            var password = Environment.GetEnvironmentVariable("FOODIES_PLATFORM_PASSWORD") ?? throw new ArgumentException("Define FOODIES_PLATFORM_PASSWORD para provisionar la cuenta.");
            var admin = await db.PlatformAdmins.SingleOrDefaultAsync(x => x.Email == email);
            if (admin is null) { admin = new PlatformAdmin { Email = email }; db.PlatformAdmins.Add(admin); }
            admin.DisplayName = a[2].Trim(); admin.IsActive = true; PlatformEndpoints.SetPassword(admin, password);
            await db.SaveChangesAsync(); Console.WriteLine("Acceso de plataforma configurado."); return;
        }
        if (a.Length == 3 && a[0] == "create-store") {
            if (!System.Text.RegularExpressions.Regex.IsMatch(a[1], "^[a-z0-9-]{1,80}$") || string.IsNullOrWhiteSpace(a[2]) || a[2].Length > 100) throw new ArgumentException("Slug o nombre inválido.");
            var store = new Store { Slug = a[1], Name = a[2], IsPaused = true }; db.Stores.Add(store); await db.SaveChangesAsync(); Console.WriteLine($"Tienda creada y pausada: {store.Id}. Configura horarios desde el panel.");
        } else if (a.Length == 4 && a[0] == "assign" && Enum.TryParse<StoreRole>(a[3], out var role) && Enum.IsDefined(role)) {
            var store = await db.Stores.SingleAsync(x => x.Slug == a[1]); var user = await db.Users.SingleAsync(x => x.Email == a[2] && x.IsActive);
            var m = await db.Memberships.SingleOrDefaultAsync(x => x.StoreId == store.Id && x.UserId == user.Id);
            if (m is null) { m = new() { StoreId = store.Id, UserId = user.Id }; db.Memberships.Add(m); }
            m.Role = role; m.IsActive = true; await db.SaveChangesAsync(); Console.WriteLine("Membresía asignada.");
        } else { Console.Error.WriteLine("Uso: --operator create-store slug \"Nombre\" | --operator assign slug correo Manager|Kitchen"); Environment.ExitCode = 1; }
    }
}
