using System.Text.Json;

namespace Foodies.Api;

public static class ContainerHealth {
    public static async Task<int> Run() {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try {
            using var response = await client.GetAsync("http://127.0.0.1:8080/api/v1/health");
            if (!response.IsSuccessStatusCode) return 1;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("databaseConnected", out var connected) && connected.ValueKind == JsonValueKind.True ? 0 : 1;
        } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return 1; }
    }
}
