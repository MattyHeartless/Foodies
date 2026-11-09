using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Foodies.Api;

public static class DbLocks {
    public static async Task<IDbContextTransaction> Begin(FoodiesDb db, string resource, CancellationToken ct) {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try {
            var result = new Microsoft.Data.SqlClient.SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await db.Database.ExecuteSqlRawAsync("EXEC @result = sp_getapplock @Resource={0}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000", [resource, result], ct);
            if ((int)result.Value < 0) throw new ApiError(409, "busy", "La operación está ocupada. Intenta de nuevo.");
            return tx;
        } catch { await tx.DisposeAsync(); throw; }
    }
}
public record StoreEvent(string Type, Guid ResourceId);
public class EventHub {
    private readonly ConcurrentDictionary<Guid, (Guid StoreId, Channel<StoreEvent> Channel)> listeners = new();
    public (Guid Id, ChannelReader<StoreEvent> Reader) Subscribe(Guid storeId) {
        var id = Guid.NewGuid(); var channel = Channel.CreateBounded<StoreEvent>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
        listeners[id] = (storeId, channel); return (id, channel.Reader);
    }
    public void Remove(Guid id) { if (listeners.TryRemove(id, out var l)) l.Channel.Writer.TryComplete(); }
    public void Publish(Guid storeId, string type, Guid id) {
        foreach (var l in listeners.Values.Where(x => x.StoreId == storeId)) l.Channel.Writer.TryWrite(new(type, id));
    }
}
public interface ISmsGateway { bool Enabled { get; } Task<string?> Send(string phone, string text, string key, CancellationToken ct); }
public static class SmsMessages {
    public const int RequiredLength = 180;
    // Preserve the exact 180-character text used in the successful handset test.
    public static string Verification(string code) => $"Foodies, antes de continuar y poder usar el servicio de recolección de comida por favor valida tu número de celular           Código {code}       No compartas con nadie este codigo";
    public static string Placed(string code) => $"Foodies: acaba de entrar el pedido #{code}. Abre la vista de cocina para revisar platillos y aceptarlo o rechazarlo. El cliente espera tu confirmación antes de pasar a recoger.";
    public static string Ready(string code) => $"Foodies: ya está listo tu pedido #{code}. Puedes pasar a recogerlo en la tienda donde lo pediste. El pago es al recoger. Muestra el código para ubicar tu pedido cuando llegues.";
    public static string Cancelled(string code) => $"Foodies: la tienda canceló tu pedido #{code}. No necesitas pasar a recogerlo ni realizar ningún pago. Puedes consultar el motivo en Mis pedidos y elegir otra tienda para pedir.";
    public static string ForOrder(string template, string code) => template switch {
        "OrderPlaced" => Placed(code), "OrderReady" => Ready(code), "OrderCancelled" => Cancelled(code),
        _ => throw new ApiError(400, "sms_template", "Plantilla SMS desconocida.")
    };
}
public class HttpSmsGateway(HttpClient client, IConfiguration config) : ISmsGateway {
    public bool Enabled => config.GetValue<bool>("Sms:Enabled") && Uri.TryCreate(config["Sms:Endpoint"], UriKind.Absolute, out var u) && u.Scheme == "https" && !string.IsNullOrWhiteSpace(config["Sms:Username"]) && !string.IsNullOrWhiteSpace(config["Sms:Password"]);
    public async Task<string?> Send(string phone, string text, string key, CancellationToken ct) {
        if (!Enabled) throw new ApiError(503, "sms_unconfigured", "El servicio SMS está pendiente de configuración.");
        if (text is null || text.Length != SmsMessages.RequiredLength) throw new ApiError(400, "sms_length", "El SMS debe contener exactamente 180 caracteres.");
        using var request = new HttpRequestMessage(HttpMethod.Post, config["Sms:Endpoint"]);
        request.Headers.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{config["Sms:Username"]}:{config["Sms:Password"]}")));
        request.Content = JsonContent.Create(new { id = key, textMessage = new { text }, phoneNumbers = new[] { phone }, ttl = 300 });
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("SMS gateway rejected delivery", null, response.StatusCode);
        // Never log provider response bodies: they may echo phone numbers or OTPs.
        try {
            using var body = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!body.RootElement.TryGetProperty("id", out var id) || id.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())) throw new HttpRequestException("SMS gateway returned no message ID");
            if (body.RootElement.TryGetProperty("state", out var state) && state.GetString() is "Failed" or "Expired") throw new HttpRequestException("SMS gateway rejected message");
            var reference = id.GetString()!;
            return reference[..Math.Min(200, reference.Length)];
        } catch (System.Text.Json.JsonException) { throw new HttpRequestException("SMS gateway returned invalid JSON"); }
    }
}
public class SmsWorker(IServiceScopeFactory scopes, ILogger<SmsWorker> log) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            try {
                await using var scope = scopes.CreateAsyncScope(); var sms = scope.ServiceProvider.GetRequiredService<ISmsGateway>();
                if (!sms.Enabled) continue;
                var db = scope.ServiceProvider.GetRequiredService<FoodiesDb>();
                await using var tx = await DbLocks.Begin(db, "foodies:sms-outbox", stoppingToken);
                var now = DateTimeOffset.UtcNow;
                var job = await db.SmsDeliveries.Where(x => x.Status == "Pending" && x.AttemptCount < 3 && x.NextAttemptAt <= now).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(stoppingToken);
                if (job is null) { await tx.CommitAsync(stoppingToken); continue; }
                var order = await db.Orders.SingleAsync(x => x.Id == job.OrderId, stoppingToken);
                job.AttemptCount++;
                try {
                    job.ProviderReference = await sms.Send(job.PhoneE164, SmsMessages.ForOrder(job.TemplateType, order.PublicCode), job.Id.ToString(), stoppingToken);
                    // Provider acknowledged queueing; this is not a handset delivery receipt.
                    job.Status = "Submitted"; job.SentAt = now; job.LastError = null;
                } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ApiError) {
                    job.LastError = ex is HttpRequestException h ? $"Gateway HTTP {(int?)h.StatusCode}" : "Gateway unavailable or timeout";
                    job.Status = job.AttemptCount >= 3 ? "Failed" : "Pending";
                    job.NextAttemptAt = now.AddSeconds(30 * job.AttemptCount);
                    log.LogWarning("SMS delivery {DeliveryId} attempt {Attempt} unsuccessful", job.Id, job.AttemptCount);
                }
                await db.SaveChangesAsync(stoppingToken); await tx.CommitAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogWarning("SMS outbox unavailable ({ErrorType})", ex.GetType().Name); }
        }
    }
}
