using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foodies.Api;

public record PlatformLoginInput(string Email, string Password);
public record PlatformPasswordInput(string CurrentPassword, string NewPassword);
public record PlatformStoreInput(string Slug, SettingsInput Settings);
public record PlatformMemberInput(string Email, StoreRole Role);

public static class PlatformEndpoints {
    public const string Scheme = "Platform";
    static readonly PasswordHasher<PlatformAdmin> Hasher = new();
    static readonly PlatformAdmin Dummy = new();
    static readonly string DummyHash = Hasher.HashPassword(Dummy, Guid.NewGuid().ToString());
    public static string Email(string? value) {
        var email = value?.Trim().ToLowerInvariant() ?? "";
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new ApiError(400, "invalid_email", "Escribe un correo válido.");
        return email;
    }
    public static void SetPassword(PlatformAdmin admin, string password) {
        if (password is null || password.Length is < 12 or > 128) throw new ApiError(400, "invalid_password", "La contraseña debe tener entre 12 y 128 caracteres.");
        admin.PasswordHash = Hasher.HashPassword(admin, password);
        admin.SecurityStamp = Guid.NewGuid().ToString("N");
        admin.FailedAttempts = 0; admin.LockedUntil = null;
    }
    static Task SignIn(HttpContext ctx, PlatformAdmin admin) => ctx.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Name, admin.DisplayName), new Claim("platform_stamp", admin.SecurityStamp)
    ], Scheme)));

    public static void Map(WebApplication app) {
        var auth = app.MapGroup("/api/v1/platform");
        auth.MapGet("/session", async (HttpContext ctx, FoodiesDb db, IAntiforgery csrf) => {
            var admin = ctx.User.Identity?.IsAuthenticated == true ? await db.PlatformAdmins.FindAsync(Access.UserId(ctx)) : null;
            return Results.Ok(new { csrfToken = csrf.GetAndStoreTokens(ctx).RequestToken, administrator = admin is null ? null : new { admin.Id, admin.DisplayName, admin.Email } });
        });
        auth.MapPost("/login", async (PlatformLoginInput input, HttpContext ctx, FoodiesDb db, CancellationToken ct) => {
            if (input.Email is null || input.Email.Length > 254 || input.Password is null || input.Password.Length > 128) throw new ApiError(401, "invalid_credentials", "Correo o contraseña incorrectos.");
            var email = input.Email.Trim().ToLowerInvariant();
            var admin = await db.PlatformAdmins.SingleOrDefaultAsync(x => x.Email == email, ct);
            var valid = Hasher.VerifyHashedPassword(admin ?? Dummy, admin?.PasswordHash ?? DummyHash, input.Password) != PasswordVerificationResult.Failed;
            if (admin is null || !admin.IsActive || admin.LockedUntil > DateTimeOffset.UtcNow || !valid) {
                if (admin is not null && admin.IsActive && !(admin.LockedUntil > DateTimeOffset.UtcNow)) {
                    if (admin.LockedUntil is not null) { admin.FailedAttempts = 0; admin.LockedUntil = null; await db.SaveChangesAsync(ct); }
                    await db.PlatformAdmins.Where(x => x.Id == admin.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.FailedAttempts, x => x.FailedAttempts + 1), ct);
                    await db.PlatformAdmins.Where(x => x.Id == admin.Id && x.FailedAttempts >= 5).ExecuteUpdateAsync(s => s.SetProperty(x => x.LockedUntil, DateTimeOffset.UtcNow.AddMinutes(10)), ct);
                }
                throw new ApiError(401, "invalid_credentials", "Correo o contraseña incorrectos.");
            }
            admin.FailedAttempts = 0; admin.LockedUntil = null; await db.SaveChangesAsync(ct);
            await SignIn(ctx, admin); return Results.NoContent();
        }).RequireRateLimiting("sensitive");
        auth.MapPost("/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(Scheme); return Results.NoContent(); }).RequireAuthorization();

        var platform = app.MapGroup("/api/v1/platform").RequireAuthorization();
        platform.MapPost("/password", async (PlatformPasswordInput input, HttpContext ctx, FoodiesDb db, CancellationToken ct) => {
            var admin = await db.PlatformAdmins.SingleAsync(x => x.Id == Access.UserId(ctx), ct);
            if (input.CurrentPassword is null || input.CurrentPassword.Length > 128 || Hasher.VerifyHashedPassword(admin, admin.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
                throw new ApiError(400, "invalid_credentials", "La contraseña actual no es correcta.");
            SetPassword(admin, input.NewPassword); await db.SaveChangesAsync(ct); await SignIn(ctx, admin); return Results.NoContent();
        }).RequireRateLimiting("sensitive");
        platform.MapGet("/stores", async (FoodiesDb db, CancellationToken ct) => {
            var stores = await db.Stores.AsNoTracking().Include(x => x.Hours).OrderBy(x => x.Name).ToListAsync(ct);
            var members = await (from m in db.Memberships.AsNoTracking() join u in db.Users on m.UserId equals u.Id where m.IsActive select new { m.Id, m.StoreId, m.UserId, m.Role, u.DisplayName, u.Email, UserActive = u.IsActive }).ToListAsync(ct);
            return Results.Ok(stores.Select(s => new { store = PublicEndpoints.StoreView(s), members = members.Where(m => m.StoreId == s.Id) }));
        });
        platform.MapPost("/stores/{id:guid}/cover-image", async (Guid id, HttpContext ctx, FoodiesDb db, MediaStorage media, CancellationToken ct) => {
            if (!await db.Stores.AnyAsync(s => s.Id == id, ct)) throw new ApiError(404,"not_found","Tienda no encontrada.");
            return Results.Ok(await media.Upload(id,ctx.Request,ct));
        });
        platform.MapPost("/stores", async (PlatformStoreInput input, FoodiesDb db, CancellationToken ct) => {
            if (input.Settings is null || input.Slug is null || !Regex.IsMatch(input.Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$") || input.Slug.Length > 80) throw new ApiError(400, "invalid_slug", "Usa letras minúsculas, números y guiones en el enlace de la tienda.");
            Validate(input.Settings);
            var store = new Store { Slug = input.Slug }; Apply(store, input.Settings); db.Stores.Add(store);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { throw new ApiError(409, "slug_taken", "Ese enlace ya pertenece a otra tienda."); }
            return Results.Created($"/api/v1/platform/stores/{store.Id}", PublicEndpoints.StoreView(store));
        });
        platform.MapPatch("/stores/{id:guid}", async (Guid id, SettingsInput input, FoodiesDb db, CancellationToken ct) => {
            Validate(input);
            var store = await db.Stores.Include(x => x.Hours).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ApiError(404, "not_found", "Tienda no encontrada.");
            Apply(store, input); await db.SaveChangesAsync(ct); return Results.Ok(PublicEndpoints.StoreView(store));
        });
        platform.MapPut("/stores/{id:guid}/members", async (Guid id, PlatformMemberInput input, FoodiesDb db, CancellationToken ct) => {
            var email = Email(input.Email);
            if (!Enum.IsDefined(input.Role)) throw new ApiError(400, "invalid_role", "Selecciona Encargado o Cocina.");
            if (!await db.Stores.AnyAsync(x => x.Id == id, ct)) throw new ApiError(404, "not_found", "Tienda no encontrada.");
            var users = await db.Users.Where(x => x.Email.ToLower() == email && x.IsActive).Take(2).ToListAsync(ct);
            if (users.Count != 1) throw new ApiError(404, "user_not_found", "El responsable debe iniciar sesión con Google antes de poder asignarlo.");
            var user = users[0];
            var member = await db.Memberships.SingleOrDefaultAsync(x => x.StoreId == id && x.UserId == user.Id, ct);
            if (member is null) { member = new Membership { StoreId = id, UserId = user.Id }; db.Memberships.Add(member); }
            member.Role = input.Role; member.IsActive = true;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { throw new ApiError(409, "concurrency", "La asignación cambió. Actualiza e intenta de nuevo."); }
            return Results.Ok(new { member.Id, member.StoreId, member.UserId, member.Role, user.DisplayName, user.Email });
        });
        platform.MapDelete("/stores/{id:guid}/members/{memberId:guid}", async (Guid id, Guid memberId, FoodiesDb db, CancellationToken ct) => {
            var member = await db.Memberships.SingleOrDefaultAsync(x => x.Id == memberId && x.StoreId == id, ct) ?? throw new ApiError(404, "not_found", "Asignación no encontrada en esta tienda.");
            member.IsActive = false; await db.SaveChangesAsync(ct); return Results.NoContent();
        });
    }
    static void Validate(SettingsInput input) {
        AdminEndpoints.ValidateSettings(input);
        if (input.IsActive && (string.IsNullOrWhiteSpace(input.AddressText) || !input.Hours.Any(x => !x.IsClosed)))
            throw new ApiError(400, "store_not_ready", "Agrega dirección y al menos un horario de apertura antes de publicar la tienda.");
    }
    static void Apply(Store store, SettingsInput input) {
        MediaStorage.ValidateReference(input.CoverImageUrl, store.Id);
        store.CoverImageUrl = string.IsNullOrWhiteSpace(input.CoverImageUrl) ? null : input.CoverImageUrl.Trim();
        store.Name = input.Name.Trim(); store.Description = input.Description.Trim(); store.AddressText = input.AddressText.Trim(); store.TimeZoneId = input.TimeZoneId;
        store.IsActive = input.IsActive; store.IsPaused = input.IsPaused; store.MinimumOrderAmount = input.MinimumOrderAmount; store.AveragePreparationMinutes = input.AveragePreparationMinutes;
        store.Hours.Clear(); store.Hours.AddRange(input.Hours.Select(h => new BusinessHour { StoreId = store.Id, DayOfWeek = h.DayOfWeek, OpeningTime = h.OpeningTime, ClosingTime = h.ClosingTime, IsClosed = h.IsClosed }));
    }
}
