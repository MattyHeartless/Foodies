using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
namespace Foodies.Api;
public static partial class AccountEndpoints {
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")] private static partial Regex PhonePattern();
    public static void Map(WebApplication app) {
        var me = app.MapGroup("/api/v1/me").RequireAuthorization();
        me.MapGet("", async (HttpContext ctx, FoodiesDb db) => {
            var u = await db.Users.SingleAsync(x => x.Id == Access.UserId(ctx)); return Results.Ok(new { u.Id, u.DisplayName, u.Email, u.PhoneE164, u.PhoneVerifiedAt });
        });
        me.MapPatch("", async (ProfileInput input, HttpContext ctx, FoodiesDb db, CancellationToken ct) => {
            if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Length > 100) throw new ApiError(400, "invalid_name", "El nombre es obligatorio (máximo 100 caracteres).");
            var u = await db.Users.SingleAsync(x => x.Id == Access.UserId(ctx), ct); u.DisplayName = input.DisplayName.Trim(); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        me.MapPatch("/phone", async (PhoneInput input, HttpContext ctx, FoodiesDb db, PhonePolicy policy, CancellationToken ct) => {
            if (policy.RequiresVerification) throw new ApiError(409, "phone_verification_required", "Verifica el nuevo teléfono mediante el código SMS antes de guardarlo.");
            if (input.PhoneE164 is null || !PhonePattern().IsMatch(input.PhoneE164)) throw new ApiError(400, "invalid_phone", "Usa formato internacional, por ejemplo +525512345678.");
            var uid = Access.UserId(ctx);
            await using var tx = await DbLocks.Begin(db, "foodies:otp:" + uid, ct);
            var user = await db.Users.SingleAsync(x => x.Id == uid, ct);
            if (user.PhoneE164 != input.PhoneE164) {
                user.PhoneE164 = input.PhoneE164; user.PhoneVerifiedAt = null;
                var pending = await db.PhoneCodes.Where(x => x.UserId == uid && x.ConsumedAt == null).ToListAsync(ct);
                foreach (var code in pending) code.ConsumedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");
        me.MapPost("/phone-verifications", async (PhoneInput input, HttpContext ctx, FoodiesDb db, ISmsGateway sms, CancellationToken ct) => {
            if (input.PhoneE164 is null || !PhonePattern().IsMatch(input.PhoneE164)) throw new ApiError(400, "invalid_phone", "Usa formato internacional, por ejemplo +525512345678.");
            if (!sms.Enabled) throw new ApiError(503, "sms_unconfigured", "La verificación SMS estará disponible al configurar el gateway.");
            var uid = Access.UserId(ctx); var now = DateTimeOffset.UtcNow;
            await using var tx = await DbLocks.Begin(db, "foodies:otp:" + uid, ct);
            if (await db.PhoneCodes.AnyAsync(x => x.UserId == uid && x.CreatedAt > now.AddMinutes(-1), ct) || await db.PhoneCodes.CountAsync(x => x.UserId == uid && x.CreatedAt > now.AddHours(-1), ct) >= 5)
                throw new ApiError(429, "otp_rate", "Espera antes de solicitar otro código. Máximo 5 por hora.");
            var old = await db.PhoneCodes.Where(x => x.UserId == uid && x.ConsumedAt == null).ToListAsync(ct);
            foreach (var item in old) item.ConsumedAt = now;
            var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6"); var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var record = new PhoneCode { UserId = uid, PhoneE164 = input.PhoneE164, Salt = salt, CodeHash = Rules.Hash(salt + code), ExpiresAt = now.AddMinutes(5) };
            db.PhoneCodes.Add(record); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            try { await sms.Send(input.PhoneE164, SmsMessages.Verification(code), record.Id.ToString(), ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { throw new ApiError(502, "sms_failed", "No se pudo confirmar el envío. Espera un minuto antes de reintentar."); }
            return Results.Ok(new { expiresAt = record.ExpiresAt });
        }).RequireRateLimiting("sensitive");
        me.MapPost("/phone-verifications/confirm", async (VerifyInput input, HttpContext ctx, FoodiesDb db, CancellationToken ct) => {
            var uid = Access.UserId(ctx); var now = DateTimeOffset.UtcNow;
            await using var tx = await DbLocks.Begin(db, "foodies:otp:" + uid, ct);
            var record = await db.PhoneCodes.Where(x => x.UserId == uid && x.ConsumedAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            if (record is null || record.ExpiresAt <= now || record.Attempts >= 5) throw new ApiError(422, "otp_expired", "Código vencido o sin intentos disponibles. Solicita otro.");
            record.Attempts++;
            var valid = input.Code is not null && input.Code.Length == 6 && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(record.CodeHash), Convert.FromHexString(Rules.Hash(record.Salt + input.Code)));
            if (!valid) { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); throw new ApiError(422, "otp_invalid", "El código es incorrecto."); }
            var user = await db.Users.SingleAsync(x => x.Id == uid, ct); user.PhoneE164 = record.PhoneE164; user.PhoneVerifiedAt = now; record.ConsumedAt = now;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        }).RequireRateLimiting("sensitive");
        me.MapGet("/orders", async (HttpContext ctx, FoodiesDb db, CancellationToken ct) => {
            var uid = Access.UserId(ctx);
            var list = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.History).Where(x => x.CustomerUserId == uid).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
            var stores = await db.Stores.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
            return Results.Ok(list.Select(x => OrderService.View(x, stores.GetValueOrDefault(x.StoreId))));
        });
        me.MapGet("/orders/{id:guid}", async (Guid id, HttpContext ctx, FoodiesDb db, CancellationToken ct) => {
            var uid = Access.UserId(ctx);
            var order = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id && x.CustomerUserId == uid, ct) ?? throw new ApiError(404, "not_found", "Pedido no encontrado.");
            var store = await db.Stores.Where(x => x.Id == order.StoreId).Select(x => x.Name).SingleAsync(ct);
            return Results.Ok(OrderService.View(order, store));
        });
    }
}
