namespace Foodies.Api;
public record OrderLineInput(Guid DishId, int Quantity, decimal ExpectedUnitPrice);
public record PlaceOrderInput(Guid StoreId, List<OrderLineInput> Items);
public record PhoneInput(string PhoneE164);
public record VerifyInput(string Code);
public record ProfileInput(string DisplayName);
public record ReasonInput(string Reason);
public record CategoryInput(string Name, int SortOrder, bool IsActive = true);
public record DishInput(Guid CategoryId, string Name, string Description, decimal Price, string? ImageUrl, bool IsActive, bool IsAvailable, int SortOrder);
public record HourInput(int DayOfWeek, TimeOnly OpeningTime, TimeOnly ClosingTime, bool IsClosed);
public record SettingsInput(string Name, string Description, string AddressText, string TimeZoneId, bool IsActive, bool IsPaused, decimal MinimumOrderAmount, int AveragePreparationMinutes, List<HourInput> Hours, string? KitchenPhoneE164 = null, string? CoverImageUrl = null);
public class ApiError(int status, string code, string message) : Exception(message) { public int Status { get; } = status; public string Code { get; } = code; }
