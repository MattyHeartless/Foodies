using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Foodies.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Foodies.Tests;

public class PlatformTests : IAsyncLifetime {
    readonly SqlFactory factory = new();
    readonly string email = $"platform-{Guid.NewGuid():N}@example.test";
    const string Password = "Fixture-Password-Only-2026!";
    Guid adminId;
    readonly List<Guid> createdStores = [];
    HttpClient client = null!;
    public async Task InitializeAsync() {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<FoodiesDb>();
        Assert.EndsWith("Foodies_Tests", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog);
        await db.Database.MigrateAsync(); await Seed.Run(db, true);
        var admin = new PlatformAdmin { Email = email, DisplayName = "Administración de prueba" }; PlatformEndpoints.SetPassword(admin, Password);
        adminId = admin.Id; db.PlatformAdmins.Add(admin); await db.SaveChangesAsync();
        client = NewClient();
    }
    HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    static async Task<JsonElement> Csrf(HttpClient c, bool platform = true) {
        var state = await c.GetFromJsonAsync<JsonElement>(platform ? "/api/v1/platform/session" : "/api/v1/auth/session");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", state.GetProperty("csrfToken").GetString()); return state;
    }
    async Task<HttpResponseMessage> Login(HttpClient c, string? password = null) {
        await Csrf(c); var response = await c.PostAsJsonAsync("/api/v1/platform/login", new { email, password = password ?? Password });
        if (response.IsSuccessStatusCode) await Csrf(c);
        return response;
    }
    static SettingsInput Draft(string name = "Tienda de integración") => new(name, "Descripción", "Dirección de prueba", "America/Mexico_City", false, true, 0, 10, []);
    async Task<Guid> CreateStore() {
        var response = await client.PostAsJsonAsync("/api/v1/platform/stores", new PlatformStoreInput("platform-test-" + Guid.NewGuid().ToString("N"), Draft()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); createdStores.Add(id); return id;
    }
    [Fact] public async Task Platform_requires_its_own_cookie_and_logout_preserves_customer_session() {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/stores")).StatusCode);
        await Csrf(client, false); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/development/manager", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/stores")).StatusCode);
        var login = await Login(client); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("foodies.platform="));
        Assert.Contains("httponly", cookie.ToLowerInvariant()); Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/platform/stores")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/platform/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/stores")).StatusCode);
        Assert.NotEqual(JsonValueKind.Null, (await Csrf(client, false)).GetProperty("user").ValueKind);
    }
    [Fact] public async Task Csrf_protects_login_and_mutations_and_secrets_never_leave_session() {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/platform/login", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client)).StatusCode);
        var state = await Csrf(client); Assert.Equal(email, state.GetProperty("administrator").GetProperty("email").GetString());
        Assert.DoesNotContain("password", state.ToString(), StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("securityStamp", state.ToString());
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/platform/stores", new PlatformStoreInput("csrf-store", Draft()))).StatusCode);
    }
    [Fact] public async Task Store_creation_is_draft_validation_is_shared_and_duplicate_slug_is_a_conflict() {
        await Login(client); var id = await CreateStore();
        var rows = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/stores");
        var store = rows.EnumerateArray().Single(row => row.GetProperty("store").GetProperty("id").GetGuid() == id).GetProperty("store");
        Assert.False(store.GetProperty("isActive").GetBoolean()); Assert.True(store.GetProperty("isPaused").GetBoolean());
        var slug = store.GetProperty("slug").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/stores/" + slug)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/platform/stores", new PlatformStoreInput(slug, Draft()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/v1/platform/stores/{id}", Draft() with { IsActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/v1/platform/stores/{id}", Draft() with { TimeZoneId = "invalid/zone" })).StatusCode);
        var published = Draft() with { IsActive = true, IsPaused = false, Hours = [new(1, new(8, 0), new(20, 0), false)] };
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/platform/stores/{id}", published)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stores/" + slug)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/v1/platform/stores/{id}", published with { Hours = [new(1, new(8, 0), new(8, 0), false)] })).StatusCode);
    }
    [Fact] public async Task Membership_assignment_is_scoped_and_revocation_removes_store_access() {
        await Login(client); var id = await CreateStore(); var other = await CreateStore();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/platform/stores/{id}/members", new { email = "unregistered@example.test", role = "Manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/platform/stores/{id}/members", new { email = "dev.manager-one@foodies.test", role = 99 })).StatusCode);
        var response = await client.PutAsJsonAsync($"/api/v1/platform/stores/{id}/members", new { email = "DEV.MANAGER-ONE@foodies.test", role = "Manager" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var memberId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var manager = NewClient(); await Csrf(manager, false); await manager.PostAsync("/api/v1/auth/development/manager", null); await Csrf(manager, false);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/v1/admin/stores/{id}/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await manager.PutAsJsonAsync($"/api/v1/platform/stores/{id}/members", new { email, role = "Manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/platform/stores/{other}/members/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/platform/stores/{id}/members/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/v1/admin/stores/{id}/settings")).StatusCode);
    }
    [Fact] public async Task Password_change_revokes_other_platform_sessions_but_keeps_current_session() {
        await Login(client); using var other = NewClient(); await Login(other);
        var input = new { currentPassword = Password, newPassword = "Changed-Fixture-Password-2026!" };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/platform/password", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/platform/stores")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/v1/platform/stores")).StatusCode);
        using var fresh = NewClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await Login(fresh)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(fresh, input.newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fresh.PostAsJsonAsync("/api/v1/platform/password", new { currentPassword = input.newPassword, newPassword = "short" })).StatusCode);
    }
    [Fact] public async Task Repeated_bad_passwords_temporarily_lock_the_account() {
        await Csrf(client);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/platform/login", new { email, password = "incorrect-fixture" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/platform/login", new { email, password = Password })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<FoodiesDb>();
        var admin = await db.PlatformAdmins.FindAsync(adminId); Assert.True(admin!.LockedUntil > DateTimeOffset.UtcNow);
    }
    [Fact] public async Task Disabling_an_administrator_revokes_existing_session() {
        await Login(client);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<FoodiesDb>(); (await db.PlatformAdmins.FindAsync(adminId))!.IsActive = false; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/stores")).StatusCode);
    }
    [Fact] public async Task Platform_can_upload_for_an_existing_store_using_its_own_session() {
        await Login(client);var id=await CreateStore();using var body=ApiTests.ImageBody(ApiTests.CoverPng);
        var response=await client.PostAsync($"/api/v1/platform/stores/{id}/cover-image",body);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var url=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString();
        Assert.Equal(HttpStatusCode.OK,(await client.PatchAsJsonAsync($"/api/v1/platform/stores/{id}",Draft() with{CoverImageUrl=url})).StatusCode);
        using var invalid=ApiTests.ImageBody(ApiTests.CoverPng);Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync($"/api/v1/platform/stores/{Guid.NewGuid()}/cover-image",invalid)).StatusCode);
    }
    public async Task DisposeAsync() {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<FoodiesDb>();
        await db.Memberships.Where(member => createdStores.Contains(member.StoreId)).ExecuteDeleteAsync();
        await db.Stores.Where(store => createdStores.Contains(store.Id)).ExecuteDeleteAsync();
        await db.PlatformAdmins.Where(admin => admin.Id == adminId).ExecuteDeleteAsync();
        client.Dispose(); await factory.DisposeAsync();
    }
}
