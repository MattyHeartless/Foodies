using Microsoft.EntityFrameworkCore;
namespace Foodies.Api;
public static class PublicEndpoints {
    public static object StoreView(Store s) => new { s.Id, s.Slug, s.CoverImageUrl, s.Name, s.Description, s.AddressText, s.TimeZoneId, s.IsActive, s.IsPaused, s.MinimumOrderAmount, s.AveragePreparationMinutes, OperationalState = Rules.OperationalState(s, DateTimeOffset.UtcNow), Hours = s.Hours.OrderBy(x => x.DayOfWeek).Select(x => new { x.DayOfWeek, x.OpeningTime, x.ClosingTime, x.IsClosed }) };
    public static void Map(WebApplication app) {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/stores", async (FoodiesDb db, CancellationToken ct) => (await db.Stores.AsNoTracking().Include(x => x.Hours).Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct)).Select(StoreView));
        api.MapGet("/stores/{slug}", async (string slug, FoodiesDb db, CancellationToken ct) => {
            var store = await db.Stores.AsNoTracking().Include(x => x.Hours).SingleOrDefaultAsync(x => x.Slug == slug && x.IsActive, ct) ?? throw new ApiError(404, "not_found", "Tienda no encontrada.");
            return StoreView(store);
        });
        api.MapGet("/stores/{storeId:guid}/dishes", async (Guid storeId, FoodiesDb db, CancellationToken ct) => {
            if (!await db.Stores.AnyAsync(x => x.Id == storeId && x.IsActive, ct)) throw new ApiError(404, "not_found", "Tienda no encontrada.");
            var categories = await db.Categories.AsNoTracking().Where(x => x.StoreId == storeId && x.IsActive).OrderBy(x => x.SortOrder).ToListAsync(ct);
            var dishes = await db.Dishes.AsNoTracking().Where(x => x.StoreId == storeId && x.IsActive && db.Categories.Any(c => c.Id == x.CategoryId && c.IsActive)).OrderBy(x => x.SortOrder).ToListAsync(ct);
            return Results.Ok(new { categories, dishes });
        });
        api.MapPost("/orders", async (PlaceOrderInput input, HttpContext ctx, OrderService orders, CancellationToken ct) => {
            var order = await orders.Place(Access.UserId(ctx), input, ctx.Request.Headers["Idempotency-Key"].ToString(), ct);
            return Results.Created($"/api/v1/me/orders/{order.Id}", OrderService.View(order));
        }).RequireAuthorization().RequireRateLimiting("sensitive");
    }
}
