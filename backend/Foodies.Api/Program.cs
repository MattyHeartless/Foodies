using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Foodies.Api;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

if (args.Contains("--healthcheck")) { Environment.ExitCode = await ContainerHealth.Run(); return; }

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders(); builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
if (!string.IsNullOrWhiteSpace(builder.Configuration["DataProtection:KeyPath"])) {
    var keyPath = Path.GetFullPath(builder.Configuration["DataProtection:KeyPath"]!, builder.Environment.ContentRootPath);
    builder.Services.AddDataProtection().SetApplicationName("Foodies").PersistKeysToFileSystem(Directory.CreateDirectory(keyPath));
}
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<FoodiesDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Foodies")));
builder.Services.AddScoped<OrderService>(); builder.Services.AddSingleton<EventHub>();
builder.Services.AddSingleton<PhonePolicy>();
builder.Services.AddSingleton<MediaStorage>();
builder.Services.AddHttpClient<ISmsGateway, HttpSmsGateway>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHostedService<SmsWorker>();
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "foodies.csrf"; o.Cookie.SameSite = SameSiteMode.Lax; o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });
var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
    o.Cookie.Name = "foodies.session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return ctx.Response.WriteAsJsonAsync(new { code = "unauthorized", message = "Inicia sesión para continuar." }); };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return ctx.Response.WriteAsJsonAsync(new { code = "forbidden", message = "No tienes permiso para esta operación." }); };
});
var googleConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Google:ClientId"]) && !string.IsNullOrWhiteSpace(builder.Configuration["Google:ClientSecret"]);
auth.AddCookie(PlatformEndpoints.Scheme, o => {
    o.Cookie.Name = "foodies.platform"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; o.ExpireTimeSpan = TimeSpan.FromHours(2); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return ctx.Response.WriteAsJsonAsync(new { code = "unauthorized", message = "Inicia sesión en la plataforma." }); };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
if (googleConfigured) auth.AddGoogle(o => {
    o.ClientId = builder.Configuration["Google:ClientId"]!; o.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
    o.CallbackPath = "/api/v1/auth/google/callback"; o.UsePkce = true; o.SaveTokens = false;
    o.CorrelationCookie.SameSite = SameSiteMode.Lax;
    o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Events.OnCreatingTicket = async ctx => {
        var db = ctx.HttpContext.RequestServices.GetRequiredService<FoodiesDb>();
        var subject = ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await db.Users.SingleOrDefaultAsync(x => x.GoogleSubject == subject);
        if (user is null) {
            user = new User { GoogleSubject = subject, Email = ctx.Principal!.FindFirstValue(ClaimTypes.Email) ?? "", DisplayName = ctx.Principal!.FindFirstValue(ClaimTypes.Name) ?? "Cliente" };
            db.Users.Add(user); await db.SaveChangesAsync();
        }
        if (!user.IsActive) throw new ApiError(403, "inactive", "La cuenta está desactivada.");
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new(ClaimTypes.NameIdentifier, user.Id.ToString())); identity.AddClaim(new(ClaimTypes.Name, user.DisplayName));
        ctx.Principal = new ClaimsPrincipal(identity);
    };
    o.Events.OnRemoteFailure = ctx => { ctx.HandleResponse(); ctx.Response.Redirect(builder.Configuration["PublicOrigin"] + "/perfil?authError=1"); return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 180, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("sensitive", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (!Uri.TryCreate(app.Configuration["PublicOrigin"], UriKind.Absolute, out var publicOrigin) || publicOrigin.Scheme is not ("http" or "https") || publicOrigin.AbsolutePath != "/" || publicOrigin.Query.Length > 0 || publicOrigin.Fragment.Length > 0 || publicOrigin.UserInfo.Length > 0) throw new InvalidOperationException("PublicOrigin debe ser un origen HTTP(S) válido, sin ruta, consulta ni credenciales.");
if (!app.Environment.IsDevelopment() && publicOrigin.Scheme != "https") throw new InvalidOperationException("PublicOrigin requiere HTTPS fuera de Development.");
if (args.Contains("--migrate")) {
    try {
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<FoodiesDb>();
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (!connection.InitialCatalog.StartsWith("Foodies", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("La base destino debe comenzar con Foodies; no se migran bases ajenas.");
        await db.Database.MigrateAsync(); await Seed.Run(db, app.Environment.IsDevelopment());
        Console.WriteLine("Migraciones y datos iniciales aplicados.");
    } catch (Microsoft.Data.SqlClient.SqlException ex) { Console.Error.WriteLine($"SQL Server rechazó la operación (código {ex.Number}). Revisa conexión y permisos."); Environment.ExitCode = 1; }
    return;
}
if (args.Contains("--operator")) { await Operator.Run(app.Services, args); return; }
app.Use(async (ctx, next) => {
    ctx.Response.Headers.XContentTypeOptions = "nosniff"; ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    ctx.Response.Headers.XFrameOptions = "DENY";
    if (ctx.Request.Path.StartsWithSegments("/api/v1/me") || ctx.Request.Path.StartsWithSegments("/api/v1/admin") || ctx.Request.Path.StartsWithSegments("/api/v1/auth") || ctx.Request.Path.StartsWithSegments("/api/v1/platform")) ctx.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (ApiError ex) { if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = ex.Status; await ctx.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }); } }
    catch (DbUpdateConcurrencyException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { code = "concurrency", message = "El recurso cambió. Actualiza e intenta de nuevo." }); }
    catch (BadHttpRequestException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { code = "invalid_request", message = "Revisa los datos de la solicitud." }); }
    catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex) {
        app.Logger.LogError("Request {TraceId} failed ({ErrorType})", ctx.TraceIdentifier, ex.GetType().Name);
        if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = 503; await ctx.Response.WriteAsJsonAsync(new { code = "service_unavailable", message = "Servicio temporalmente no disponible. Revisa la conexión a la base de datos.", traceId = ctx.TraceIdentifier }); }
    }
});
// Use the trusted public origin for OAuth and Secure cookies behind the Next.js proxy.
app.Use(async (ctx, next) => {
    ctx.Request.Scheme = publicOrigin.Scheme;
    ctx.Request.Host = publicOrigin.IsDefaultPort ? new HostString(publicOrigin.Host) : new HostString(publicOrigin.Host, publicOrigin.Port);
    await next();
});
app.UseAuthentication();
app.Use(async (ctx, next) => {
    if (ctx.Request.Path.StartsWithSegments("/api/v1/platform")) {
        var ticket = await ctx.AuthenticateAsync(PlatformEndpoints.Scheme);
        ctx.User = ticket.Succeeded ? ticket.Principal! : new ClaimsPrincipal(new ClaimsIdentity());
    }
    await next();
});
app.UseAuthorization(); app.UseRateLimiter();
app.Use(async (ctx, next) => {
    if (ctx.User.Identity?.IsAuthenticated == true) {
        var db = ctx.RequestServices.GetRequiredService<FoodiesDb>();
        var platform = ctx.Request.Path.StartsWithSegments("/api/v1/platform");
        var actorId = Access.UserId(ctx);
        var stamp = ctx.User.FindFirstValue("platform_stamp");
        var active = platform ? await db.PlatformAdmins.AnyAsync(x => x.Id == actorId && x.IsActive && x.SecurityStamp == stamp, ctx.RequestAborted) : await db.Users.AnyAsync(x => x.Id == actorId && x.IsActive, ctx.RequestAborted);
        if (!active) {
            await ctx.SignOutAsync(platform ? PlatformEndpoints.Scheme : CookieAuthenticationDefaults.AuthenticationScheme); ctx.Response.StatusCode = 401; return;
        }
    }
    if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method)) {
        try { await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx); }
        catch (AntiforgeryValidationException) { throw new ApiError(400, "csrf", "La sesión del formulario caducó. Recarga la página."); }
    }
    await next();
});
app.MapGet("/api/v1/health", async (FoodiesDb db) => {
    var connected = false; try { connected = await db.Database.CanConnectAsync(); } catch { }
    return Results.Ok(new { status = connected ? "ready" : "database-unavailable", databaseConnected = connected });
});
app.MapGet("/api/v1/media/stores/{storeId:guid}/{fileName}", (Guid storeId, string fileName, HttpContext ctx, MediaStorage media) => media.Read(storeId, fileName, ctx));
app.MapGet("/api/v1/auth/session", async (HttpContext ctx, IAntiforgery csrf, FoodiesDb db, ISmsGateway sms, PhonePolicy phonePolicy) => {
    var user = ctx.User.Identity?.IsAuthenticated == true ? await db.Users.FindAsync(Access.UserId(ctx)) : null;
    var memberships = user is null ? [] : await (from m in db.Memberships join s in db.Stores on m.StoreId equals s.Id where m.UserId == user.Id && m.IsActive select new { m.StoreId, m.Role, s.Name }).ToListAsync();
    return Results.Ok(new { csrfToken = csrf.GetAndStoreTokens(ctx).RequestToken, user = user is null ? null : new { user.Id, user.DisplayName, user.Email, user.PhoneE164, user.PhoneVerifiedAt }, memberships,
        googleConfigured, smsConfigured = sms.Enabled, phoneVerificationRequired = phonePolicy.RequiresVerification, developmentAccess = app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("DevelopmentAccess:Enabled") });
});
app.MapGet("/api/v1/auth/google/start", (string? returnUrl) => {
    if (!googleConfigured) throw new ApiError(503, "google_unconfigured", "Google Login está pendiente de configuración.");
    var path = returnUrl is not null && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.Contains('\\') && !returnUrl.Any(char.IsControl) ? returnUrl : "/perfil";
    return Results.Challenge(new AuthenticationProperties { RedirectUri = app.Configuration["PublicOrigin"] + path }, ["Google"]);
}).RequireRateLimiting("sensitive");
app.MapPost("/api/v1/auth/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("DevelopmentAccess:Enabled")) {
    app.MapPost("/api/v1/auth/development/{account}", async (string account, HttpContext ctx, FoodiesDb db) => {
        var subject = account switch { "customer" => "dev:customer", "manager" => "dev:manager-one", "kitchen" => "dev:kitchen-one", "manager-two" => "dev:manager-two", _ => throw new ApiError(400, "invalid_account", "Cuenta local desconocida.") };
        var user = await db.Users.SingleOrDefaultAsync(x => x.GoogleSubject == subject && x.IsActive) ?? throw new ApiError(503, "seed_required", "Aplica las migraciones y datos iniciales.");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.DisplayName)], CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(new ClaimsPrincipal(identity)); return Results.Ok(new { user.DisplayName });
    }).RequireRateLimiting("sensitive");
}
PublicEndpoints.Map(app); AccountEndpoints.Map(app); AdminEndpoints.Map(app); PlatformEndpoints.Map(app);
app.MapGet("/api/v1/openapi.json", () => Results.File(Path.Combine(app.Environment.ContentRootPath, "openapi.json"), "application/json"));
app.Run();
public partial class Program { }

namespace Foodies.Api {
    public static class Access {
        public static Guid UserId(HttpContext ctx) => Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        public static async Task<Membership> Store(HttpContext ctx, FoodiesDb db, Guid storeId, bool managerOnly = false) {
            var userId = UserId(ctx);
            var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.StoreId == storeId && x.UserId == userId && x.IsActive, ctx.RequestAborted);
            if (member is null || managerOnly && member.Role != StoreRole.Manager) throw new ApiError(403, "forbidden", "No tienes permiso para operar esta tienda.");
            return member;
        }
    }
}
