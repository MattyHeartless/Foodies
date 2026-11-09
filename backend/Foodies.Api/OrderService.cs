using Microsoft.EntityFrameworkCore;
namespace Foodies.Api;
public class OrderService(FoodiesDb db, EventHub events, PhonePolicy phonePolicy) {
    public async Task<Order> Place(Guid userId, PlaceOrderInput input, string key, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new ApiError(400, "idempotency_required", "Envía Idempotency-Key (1 a 100 caracteres).");
        if (input.Items is null || input.Items.Count is < 1 or > 30 || input.Items.Any(x => x.Quantity is < 1 or > 50 || x.ExpectedUnitPrice < 0 || x.ExpectedUnitPrice > 100000 || decimal.Round(x.ExpectedUnitPrice, 2) != x.ExpectedUnitPrice) || input.Items.Select(x => x.DishId).Distinct().Count() != input.Items.Count)
            throw new ApiError(400, "invalid_cart", "El carrito debe contener entre 1 y 30 platillos distintos, con cantidades de 1 a 50.");
        var fingerprint = Rules.Hash(input.StoreId + ":" + string.Join(";", input.Items.OrderBy(x => x.DishId).Select(x => FormattableString.Invariant($"{x.DishId}:{x.Quantity}:{x.ExpectedUnitPrice:F2}"))));
        await using var tx = await DbLocks.Begin(db, "foodies:user:" + userId, ct);
        var existing = await db.Orders.Include(x => x.Items).Include(x => x.History).SingleOrDefaultAsync(x => x.CustomerUserId == userId && x.IdempotencyKey == key, ct);
        if (existing is not null) {
            if (existing.RequestHash != fingerprint) throw new ApiError(409, "idempotency_conflict", "La clave ya se utilizó con otro carrito.");
            await tx.CommitAsync(ct); return existing;
        }
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        if (string.IsNullOrWhiteSpace(user.PhoneE164)) throw new ApiError(422, "phone_required", "Guarda tu teléfono antes de solicitar.");
        if (phonePolicy.RequiresVerification && user.PhoneVerifiedAt is null) throw new ApiError(422, "phone_verification_required", "Verifica tu teléfono antes de solicitar.");
        var store = await db.Stores.Include(x => x.Hours).SingleOrDefaultAsync(x => x.Id == input.StoreId && x.IsActive, ct) ?? throw new ApiError(404, "not_found", "Tienda no encontrada.");
        if (Rules.OperationalState(store, DateTimeOffset.UtcNow) != "Abierta") throw new ApiError(409, "store_closed", "La tienda está cerrada o pausada.");
        var ids = input.Items.Select(x => x.DishId).ToArray();
        var dishes = await db.Dishes.Where(x => x.StoreId == store.Id && ids.Contains(x.Id) && x.IsActive && x.IsAvailable && db.Categories.Any(c => c.Id == x.CategoryId && c.StoreId == store.Id && c.IsActive)).ToListAsync(ct);
        if (dishes.Count != ids.Length) throw new ApiError(409, "unavailable", "Hay platillos agotados o inactivos. Revisa el carrito.");
        if (input.Items.Any(x => dishes.Single(d => d.Id == x.DishId).Price != x.ExpectedUnitPrice)) throw new ApiError(409, "price_changed", "Cambió el precio de un platillo. Revisa los precios actuales antes de confirmar.");
        var order = new Order { CustomerUserId = userId, StoreId = store.Id, IdempotencyKey = key, RequestHash = fingerprint };
        order.Items = input.Items.Select(x => { var dish = dishes.Single(d => d.Id == x.DishId); return new OrderItem { DishId = dish.Id, NameSnapshot = dish.Name, UnitPriceSnapshot = dish.Price, Quantity = x.Quantity, LineTotal = dish.Price * x.Quantity }; }).ToList();
        order.Total = order.Items.Sum(x => x.LineTotal);
        if (order.Total < store.MinimumOrderAmount) throw new ApiError(422, "minimum_not_met", $"El mínimo de la tienda es {store.MinimumOrderAmount:0.00} MXN.");
        order.History.Add(new() { NewStatus = OrderStatus.Pendiente, ChangedByUserId = userId });
        db.Orders.Add(order);
        if (!string.IsNullOrWhiteSpace(store.KitchenPhoneE164))
            db.SmsDeliveries.Add(new() { OrderId = order.Id, TemplateType = "OrderPlaced", PhoneE164 = store.KitchenPhoneE164 });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        events.Publish(store.Id, "order.created", order.Id); return order;
    }
    public async Task<Order> Transition(Guid storeId, Guid orderId, Guid actor, OrderStatus target, string? reason, CancellationToken ct) {
        await using var tx = await DbLocks.Begin(db, "foodies:store:" + storeId, ct);
        var order = await db.Orders.Include(x => x.Items).Include(x => x.History).SingleOrDefaultAsync(x => x.StoreId == storeId && x.Id == orderId, ct) ?? throw new ApiError(404, "not_found", "Pedido no encontrado.");
        if (order.Status == target) { await tx.CommitAsync(ct); return order; }
        if (!Rules.CanTransition(order.Status, target)) throw new ApiError(409, "invalid_transition", "El pedido ya cambió o la transición no está permitida.");
        if (target is OrderStatus.Rechazado or OrderStatus.Cancelado && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)) throw new ApiError(400, "reason_required", "Indica un motivo de hasta 500 caracteres.");
        reason = reason?.Trim();
        var now = DateTimeOffset.UtcNow;
        if (target == OrderStatus.Aceptado) {
            var average = await db.Stores.Where(x => x.Id == storeId).Select(x => x.AveragePreparationMinutes).SingleAsync(ct);
            var ahead = await db.Orders.CountAsync(x => x.StoreId == storeId && x.Id != order.Id && x.CreatedAt < order.CreatedAt && (x.Status == OrderStatus.Pendiente || x.Status == OrderStatus.Aceptado || x.Status == OrderStatus.EnPreparacion), ct);
            order.AveragePreparationSnapshotMin = average; order.ActiveOrdersAheadSnapshot = ahead;
            order.EstimatedPreparationSnapshotMin = Rules.Estimate(average, ahead); order.AcceptedAt = now;
        }
        if (target is OrderStatus.Listo or OrderStatus.Cancelado) {
            if (target == OrderStatus.Listo) order.ReadyAt = now;
            var customer = await db.Users.SingleAsync(x => x.Id == order.CustomerUserId, ct);
            if (customer.PhoneVerifiedAt is not null && !string.IsNullOrWhiteSpace(customer.PhoneE164))
                db.SmsDeliveries.Add(new() { OrderId = order.Id, PhoneE164 = customer.PhoneE164, TemplateType = target == OrderStatus.Cancelado ? "OrderCancelled" : "OrderReady" });
        }
        if (target == OrderStatus.Entregado) order.DeliveredAt = now;
        order.History.Add(new() { PreviousStatus = order.Status, NewStatus = target, ChangedByUserId = actor, Reason = reason, ChangedAt = now });
        order.Status = target; order.UpdatedAt = now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); events.Publish(storeId, "order.status-changed", order.Id); return order;
    }
    public static object View(Order o, string? storeName = null) => new {
        o.Id, o.StoreId, StoreName = storeName, o.PublicCode, o.Status, o.Total, o.CreatedAt, o.AcceptedAt, o.ReadyAt, o.DeliveredAt,
        o.EstimatedPreparationSnapshotMin, o.ActiveOrdersAheadSnapshot,
        Items = o.Items.Select(x => new { x.DishId, x.NameSnapshot, x.UnitPriceSnapshot, x.Quantity, x.LineTotal }),
        History = o.History.OrderBy(x => x.ChangedAt).Select(x => new { x.PreviousStatus, x.NewStatus, x.Reason, x.ChangedAt })
    };
}
