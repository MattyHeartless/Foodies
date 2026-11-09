using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace Foodies.Api;
public static class AdminEndpoints {
    public static void Map(WebApplication app) {
        var admin = app.MapGroup("/api/v1/admin/stores/{storeId:guid}").RequireAuthorization();
        admin.AddEndpointFilter(async (context, next) => {
            var storeId = Guid.Parse(context.HttpContext.Request.RouteValues["storeId"]!.ToString()!);
            await Access.Store(context.HttpContext, context.HttpContext.RequestServices.GetRequiredService<FoodiesDb>(), storeId);
            return await next(context);
        });
        admin.MapGet("/orders", async (Guid storeId, FoodiesDb db, CancellationToken ct) => {
            var list = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.History).Where(x => x.StoreId == storeId).OrderByDescending(x => x.CreatedAt).Take(150).ToListAsync(ct);
            return Results.Ok(list.Select(x => OrderService.View(x)));
        });
        admin.MapPost("/cover-image", async (Guid storeId, HttpContext ctx, FoodiesDb db, MediaStorage media, CancellationToken ct) => {
            await Access.Store(ctx, db, storeId, true);
            return Results.Ok(await media.Upload(storeId, ctx.Request, ct));
        });
        foreach (var (action, status) in new[] { ("accept", OrderStatus.Aceptado), ("start-preparation", OrderStatus.EnPreparacion), ("ready", OrderStatus.Listo), ("delivered", OrderStatus.Entregado) }) {
            admin.MapPost("/orders/{orderId:guid}/" + action, async (Guid storeId, Guid orderId, HttpContext ctx, OrderService service, CancellationToken ct) => Results.Ok(OrderService.View(await service.Transition(storeId, orderId, Access.UserId(ctx), status, null, ct))));
        }
        admin.MapPost("/orders/{orderId:guid}/reject", async (Guid storeId, Guid orderId, ReasonInput input, HttpContext ctx, OrderService service, CancellationToken ct) => Results.Ok(OrderService.View(await service.Transition(storeId, orderId, Access.UserId(ctx), OrderStatus.Rechazado, input.Reason, ct))));
        admin.MapPost("/orders/{orderId:guid}/cancel", async (Guid storeId, Guid orderId, ReasonInput input, HttpContext ctx, OrderService service, CancellationToken ct) => Results.Ok(OrderService.View(await service.Transition(storeId, orderId, Access.UserId(ctx), OrderStatus.Cancelado, input.Reason, ct))));
        admin.MapGet("/dishes", async (Guid storeId, FoodiesDb db, CancellationToken ct) => await db.Dishes.AsNoTracking().Where(x => x.StoreId == storeId).OrderBy(x => x.SortOrder).ToListAsync(ct));
        admin.MapGet("/categories", async (Guid storeId, FoodiesDb db, CancellationToken ct) => await db.Categories.AsNoTracking().Where(x => x.StoreId == storeId).OrderBy(x => x.SortOrder).ToListAsync(ct));
        admin.MapPost("/categories", async (Guid storeId, CategoryInput input, FoodiesDb db, CancellationToken ct) => {
            ValidateCategory(input);
            var cat = new Category { StoreId = storeId, Name = input.Name.Trim(), SortOrder = input.SortOrder, IsActive = input.IsActive };
            db.Categories.Add(cat); await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/admin/stores/{storeId}/categories/{cat.Id}", cat);
        });
        admin.MapPatch("/categories/{id:guid}", async (Guid storeId, Guid id, CategoryInput input, FoodiesDb db, CancellationToken ct) => {
            ValidateCategory(input);
            var cat = await db.Categories.SingleOrDefaultAsync(x => x.Id == id && x.StoreId == storeId, ct) ?? throw new ApiError(404, "not_found", "Categoría no encontrada.");
            cat.Name = input.Name.Trim(); cat.SortOrder = input.SortOrder; cat.IsActive = input.IsActive;
            await db.SaveChangesAsync(ct); return Results.Ok(cat);
        });
        admin.MapPost("/dishes", async (Guid storeId, DishInput input, FoodiesDb db, EventHub events, CancellationToken ct) => {
            await ValidateDish(storeId, input, db, ct); var dish = new Dish { StoreId = storeId }; Apply(dish, input);
            db.Dishes.Add(dish); await db.SaveChangesAsync(ct); events.Publish(storeId, "dish.availability-changed", dish.Id);
            return Results.Created($"/api/v1/admin/stores/{storeId}/dishes/{dish.Id}", dish);
        });
        admin.MapPatch("/dishes/{id:guid}", async (Guid storeId, Guid id, DishInput input, FoodiesDb db, EventHub events, CancellationToken ct) => {
            await ValidateDish(storeId, input, db, ct);
            var dish = await db.Dishes.SingleOrDefaultAsync(x => x.Id == id && x.StoreId == storeId, ct) ?? throw new ApiError(404, "not_found", "Platillo no encontrado.");
            Apply(dish, input); await db.SaveChangesAsync(ct); events.Publish(storeId, "dish.availability-changed", dish.Id); return Results.Ok(dish);
        });
        admin.MapGet("/settings", async (Guid storeId, FoodiesDb db, CancellationToken ct) => SettingsView(await db.Stores.AsNoTracking().Include(x => x.Hours).SingleAsync(x => x.Id == storeId, ct)));
        admin.MapPatch("/settings", async (Guid storeId, SettingsInput input, HttpContext ctx, FoodiesDb db, CancellationToken ct) => {
            await Access.Store(ctx, db, storeId, true);
            ValidateSettings(input);
            MediaStorage.ValidateReference(input.CoverImageUrl, storeId);
            var store = await db.Stores.Include(x => x.Hours).SingleAsync(x => x.Id == storeId, ct);
            store.Name = input.Name.Trim(); store.Description = input.Description; store.AddressText = input.AddressText; store.TimeZoneId = input.TimeZoneId;
            store.MinimumOrderAmount = input.MinimumOrderAmount; store.AveragePreparationMinutes = input.AveragePreparationMinutes; store.IsActive = input.IsActive; store.IsPaused = input.IsPaused;
            store.KitchenPhoneE164 = string.IsNullOrWhiteSpace(input.KitchenPhoneE164) ? null : input.KitchenPhoneE164.Trim();
            store.CoverImageUrl = string.IsNullOrWhiteSpace(input.CoverImageUrl) ? null : input.CoverImageUrl.Trim();
            store.Hours.Clear(); store.Hours.AddRange(input.Hours.Select(x => new BusinessHour { StoreId = storeId, DayOfWeek = x.DayOfWeek, OpeningTime = x.OpeningTime, ClosingTime = x.ClosingTime, IsClosed = x.IsClosed }));
            await db.SaveChangesAsync(ct); return Results.Ok(SettingsView(store));
        });
        admin.MapGet("/events", async (Guid storeId, HttpContext ctx, FoodiesDb db, EventHub events, CancellationToken ct) => {
            ctx.Response.ContentType = "text/event-stream"; ctx.Response.Headers.CacheControl = "no-cache, no-transform"; ctx.Response.Headers["X-Accel-Buffering"] = "no";
            var subscription = events.Subscribe(storeId);
            try {
                await ctx.Response.WriteAsync("retry: 5000\nevent: connected\ndata: {}\n\n", ct); await ctx.Response.Body.FlushAsync(ct);
                while (!ct.IsCancellationRequested) {
                    // Re-check permission and cookie expiry while the connection remains open.
                    var ticket = await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.AuthenticateAsync(ctx);
                    if (ticket.Properties?.ExpiresUtc < DateTimeOffset.UtcNow || !await db.Users.AsNoTracking().AnyAsync(x => x.Id == Access.UserId(ctx) && x.IsActive, ct)) break;
                    await Access.Store(ctx, db, storeId);
                    while (subscription.Reader.TryRead(out var ev)) {
                        await ctx.Response.WriteAsync($"event: {ev.Type}\ndata: {JsonSerializer.Serialize(new { resourceId = ev.ResourceId })}\n\n", ct);
                    }
                    await ctx.Response.WriteAsync(": heartbeat\n\n", ct); await ctx.Response.Body.FlushAsync(ct);
                    using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct); wait.CancelAfter(TimeSpan.FromSeconds(15));
                    try { await subscription.Reader.WaitToReadAsync(wait.Token); } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                }
            } finally { events.Remove(subscription.Id); }
        });
    }
    static object SettingsView(Store s) => new { s.Id, s.Slug, s.CoverImageUrl, s.Name, s.Description, s.AddressText, s.TimeZoneId, s.IsActive, s.IsPaused, s.MinimumOrderAmount, s.AveragePreparationMinutes, s.KitchenPhoneE164, OperationalState = Rules.OperationalState(s, DateTimeOffset.UtcNow), Hours = s.Hours.OrderBy(x => x.DayOfWeek).Select(x => new { x.DayOfWeek, x.OpeningTime, x.ClosingTime, x.IsClosed }) };
    public static void ValidateSettings(SettingsInput input) {
        if (!string.IsNullOrWhiteSpace(input.CoverImageUrl) && !MediaStorage.IsManagedUrl(input.CoverImageUrl.Trim()) && (input.CoverImageUrl.Trim().Length > 500 || !Uri.TryCreate(input.CoverImageUrl.Trim(), UriKind.Absolute, out var cover) || cover.Scheme != "https" || cover.UserInfo.Length > 0))
            throw new ApiError(400, "invalid_cover", "La portada debe ser una URL HTTPS de hasta 500 caracteres, sin credenciales.");
        if (!string.IsNullOrWhiteSpace(input.KitchenPhoneE164) && !System.Text.RegularExpressions.Regex.IsMatch(input.KitchenPhoneE164.Trim(), @"^\+52[0-9]{10}$"))
            throw new ApiError(400, "invalid_kitchen_phone", "El teléfono de avisos debe tener +52 y diez dígitos.");
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100 || input.Description is null || input.Description.Length > 500 || input.AddressText is null || input.AddressText.Length > 300 || input.MinimumOrderAmount < 0 || input.MinimumOrderAmount > 100000 || decimal.Round(input.MinimumOrderAmount, 2) != input.MinimumOrderAmount || input.AveragePreparationMinutes is < 1 or > 240)
            throw new ApiError(400, "invalid_settings", "Revisa nombre, dirección, mínimo y promedio (1 a 240 minutos).");
        if (string.IsNullOrWhiteSpace(input.TimeZoneId) || input.TimeZoneId.Length > 100) throw new ApiError(400, "invalid_timezone", "Zona horaria inválida.");
        try { TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId); } catch { throw new ApiError(400, "invalid_timezone", "Zona horaria inválida."); }
        if (input.Hours is null || input.Hours.Count > 28 || input.Hours.Any(x => x.DayOfWeek is < 0 or > 6 || !x.IsClosed && x.OpeningTime == x.ClosingTime)) throw new ApiError(400, "invalid_hours", "Revisa los horarios. Apertura y cierre deben ser distintos.");
    }
    static void ValidateCategory(CategoryInput i) { if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 80) throw new ApiError(400, "invalid_category", "Escribe una categoría de hasta 80 caracteres."); }
    static async Task ValidateDish(Guid store, DishInput i, FoodiesDb db, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 120 || i.Description is null || i.Description.Length > 500 || i.Price <= 0 || i.Price > 100000 || decimal.Round(i.Price, 2) != i.Price)
            throw new ApiError(400, "invalid_dish", "Revisa el nombre, descripción y precio del platillo.");
        if (!string.IsNullOrEmpty(i.ImageUrl) && (i.ImageUrl.Length > 500 || !Uri.TryCreate(i.ImageUrl, UriKind.Absolute, out var url) || url.Scheme != "https")) throw new ApiError(400, "invalid_image", "La foto debe ser una URL HTTPS de hasta 500 caracteres.");
        if (!await db.Categories.AnyAsync(x => x.Id == i.CategoryId && x.StoreId == store, ct)) throw new ApiError(400, "invalid_category", "La categoría no pertenece a esta tienda.");
    }
    static void Apply(Dish d, DishInput i) { d.CategoryId = i.CategoryId; d.Name = i.Name.Trim(); d.Description = i.Description; d.Price = i.Price; d.ImageUrl = i.ImageUrl; d.IsActive = i.IsActive; d.IsAvailable = i.IsAvailable; d.SortOrder = i.SortOrder; d.UpdatedAt = DateTimeOffset.UtcNow; }
}
