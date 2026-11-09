using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Foodies.Api;

public enum OrderStatus { Pendiente, Aceptado, EnPreparacion, Listo, Entregado, Rechazado, Cancelado, NoRecogido }
public enum StoreRole { Manager, Kitchen }
public class PlatformAdmin {
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(100)] public string DisplayName { get; set; } = "";
    [MaxLength(500)] public string PasswordHash { get; set; } = "";
    [MaxLength(64)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsActive { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
public class User {
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(200)] public string GoogleSubject { get; set; } = "";
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(100)] public string DisplayName { get; set; } = "";
    [MaxLength(500)] public string? AvatarUrl { get; set; }
    [MaxLength(20)] public string? PhoneE164 { get; set; }
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
}
public class Store {
    [MaxLength(500)] public string? CoverImageUrl { get; set; }
    [MaxLength(20)] public string? KitchenPhoneE164 { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(80)] public string Slug { get; set; } = "";
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
    [MaxLength(300)] public string AddressText { get; set; } = "";
    [MaxLength(100)] public string TimeZoneId { get; set; } = "America/Mexico_City";
    public bool IsActive { get; set; } = true;
    public bool IsPaused { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public int AveragePreparationMinutes { get; set; } = 10;
    public List<BusinessHour> Hours { get; set; } = [];
}
public class BusinessHour {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid StoreId { get; set; }
    public int DayOfWeek { get; set; } public TimeOnly OpeningTime { get; set; } public TimeOnly ClosingTime { get; set; }
    public bool IsClosed { get; set; }
}
public class Membership {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid StoreId { get; set; } public Guid UserId { get; set; }
    public StoreRole Role { get; set; } public bool IsActive { get; set; } = true;
}
public class Category {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid StoreId { get; set; }
    [MaxLength(80)] public string Name { get; set; } = ""; public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public class Dish {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid StoreId { get; set; } public Guid CategoryId { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
    public decimal Price { get; set; }
    [MaxLength(500)] public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true; public bool IsAvailable { get; set; } = true; public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class Order {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid StoreId { get; set; } public Guid CustomerUserId { get; set; }
    [MaxLength(20)] public string PublicCode { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
    [MaxLength(100)] public string IdempotencyKey { get; set; } = "";
    [MaxLength(64)] public string RequestHash { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.Pendiente; public decimal Total { get; set; }
    public int? AveragePreparationSnapshotMin { get; set; } public int? ActiveOrdersAheadSnapshot { get; set; }
    public int? EstimatedPreparationSnapshotMin { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; } public DateTimeOffset? ReadyAt { get; set; } public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Timestamp] public byte[] Version { get; set; } = [];
    public List<OrderItem> Items { get; set; } = []; public List<StatusHistory> History { get; set; } = [];
}
public class OrderItem {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrderId { get; set; } public Guid DishId { get; set; }
    [MaxLength(120)] public string NameSnapshot { get; set; } = "";
    public decimal UnitPriceSnapshot { get; set; } public int Quantity { get; set; } public decimal LineTotal { get; set; }
}
public class StatusHistory {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrderId { get; set; }
    public OrderStatus? PreviousStatus { get; set; } public OrderStatus NewStatus { get; set; } public Guid ChangedByUserId { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class PhoneCode {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid UserId { get; set; }
    [MaxLength(20)] public string PhoneE164 { get; set; } = "";
    [MaxLength(100)] public string CodeHash { get; set; } = "";
    [MaxLength(64)] public string Salt { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; } public DateTimeOffset? ConsumedAt { get; set; }
}
public class SmsDelivery {
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrderId { get; set; }
    [MaxLength(30)] public string TemplateType { get; set; } = "OrderReady";
    [MaxLength(20)] public string PhoneE164 { get; set; } = "";
    [MaxLength(30)] public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    [MaxLength(200)] public string? ProviderReference { get; set; }
    [MaxLength(100)] public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
}
public static class Rules {
    public static bool Active(OrderStatus s) => s is OrderStatus.Pendiente or OrderStatus.Aceptado or OrderStatus.EnPreparacion;
    public static int Estimate(int average, int ahead) => checked(average + 2 * ahead);
    public static bool CanTransition(OrderStatus from, OrderStatus to) => (from, to) is
        (OrderStatus.Pendiente, OrderStatus.Aceptado) or (OrderStatus.Pendiente, OrderStatus.Rechazado) or
        (OrderStatus.Aceptado, OrderStatus.EnPreparacion) or (OrderStatus.EnPreparacion, OrderStatus.Listo) or (OrderStatus.Listo, OrderStatus.Entregado) or
        (OrderStatus.Pendiente, OrderStatus.Cancelado) or (OrderStatus.Aceptado, OrderStatus.Cancelado) or (OrderStatus.EnPreparacion, OrderStatus.Cancelado);
    public static string OperationalState(Store store, DateTimeOffset now) {
        if (!store.IsActive) return "Cerrada";
        if (store.IsPaused) return "Pausada";
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(store.TimeZoneId));
        var day = (int)local.DayOfWeek; var time = TimeOnly.FromDateTime(local.DateTime);
        var previous = (day + 6) % 7;
        var open = store.Hours.Any(h => !h.IsClosed && (
            h.DayOfWeek == day && (h.OpeningTime < h.ClosingTime ? time >= h.OpeningTime && time < h.ClosingTime : h.OpeningTime > h.ClosingTime && time >= h.OpeningTime)
            || h.DayOfWeek == previous && h.OpeningTime > h.ClosingTime && time < h.ClosingTime));
        return open ? "Abierta" : "Cerrada";
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
