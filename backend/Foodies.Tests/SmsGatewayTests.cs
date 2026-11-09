using System.Net;
using System.Text;
using System.Text.Json;
using Foodies.Api;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Foodies.Tests;
public class SmsGatewayTests {
    sealed class SimulationTransport(string origin) : DelegatingHandler(new HttpClientHandler()) {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            request.RequestUri = new Uri(origin.TrimEnd('/') + "/3rdparty/v1/messages");
            request.Headers.Add("x-mock-slug", "smsgate");
            return base.SendAsync(request, ct);
        }
    }
    [Fact] public async Task Optional_local_simulation_exercises_real_http_serialization_and_provider_failure() {
        var origin = Environment.GetEnvironmentVariable("FOODIES_SMS_SIMULATION");
        if (string.IsNullOrEmpty(origin)) return;
        Assert.StartsWith("http://127.0.0.1:", origin);
        using var client = new HttpClient(new SimulationTransport(origin));
        var gateway = new HttpSmsGateway(client, Config());
        Assert.Equal("fixture-success", await gateway.Send("+525500000001", SmsMessages.Verification("000123"), "fixture-success", default));
        Assert.Equal("fixture-placed", await gateway.Send("+525500000099", SmsMessages.ForOrder("OrderPlaced","0123456789"), "fixture-placed", default));
        Assert.Equal("fixture-ready", await gateway.Send("+525500000001", SmsMessages.ForOrder("OrderReady","0123456789"), "fixture-ready", default));
        Assert.Equal("fixture-cancelled", await gateway.Send("+525500000001", SmsMessages.ForOrder("OrderCancelled","0123456789"), "fixture-cancelled", default));
        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.Send("+525500000001", SmsMessages.Verification("000123"), "fixture-failure", default));
    }
    static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["Sms:Enabled"] = "true", ["Sms:Endpoint"] = "https://sms-fixture.invalid/messages", ["Sms:Username"] = "fixture", ["Sms:Password"] = "fixture-only"
    }).Build();
    sealed class GatewayHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> handle) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request);
    }
    [Fact] public async Task Sends_basic_auth_utf8_native_payload_and_stable_id_accepting_pending_as_submission() {
        using var client = new HttpClient(new GatewayHandler(async request => {
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            Assert.Equal("fixture:fixture-only", Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!)));
            Assert.Equal("utf-8", request.Content!.Headers.ContentType!.CharSet);
            using var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            Assert.Equal("stable-id", body.RootElement.GetProperty("id").GetString());
            Assert.Equal("Verificación número".PadRight(180, '.'), body.RootElement.GetProperty("textMessage").GetProperty("text").GetString());
            Assert.Equal("+525500000001", body.RootElement.GetProperty("phoneNumbers")[0].GetString());
            Assert.Equal(300, body.RootElement.GetProperty("ttl").GetInt32());
            return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{\"id\":\"stable-id\",\"state\":\"Pending\"}") };
        }));
        Assert.Equal("stable-id", await new HttpSmsGateway(client, Config()).Send("+525500000001", ("Verificación número".PadRight(180, '.')), "stable-id", default));
    }
    [Fact] public async Task Requires_exactly_180_characters_before_contacting_provider() {
        var calls = 0;
        using var client = new HttpClient(new GatewayHandler(_ => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"test\"}") }); }));
        var gateway = new HttpSmsGateway(client, Config());
        await gateway.Send("+525500000001", new string('a',180), "id", default);
        var error = await Assert.ThrowsAsync<ApiError>(() => gateway.Send("+525500000001", new string('a',181), "id", default));
        Assert.Equal("sms_length", error.Code);
        await Assert.ThrowsAsync<ApiError>(() => gateway.Send("+525500000001", new string('a',179), "id", default));
        Assert.Equal(1,calls);
        Assert.Equal(180, SmsMessages.Verification("000123").Length);
        Assert.Equal(180, SmsMessages.Ready("0123456789").Length);
        Assert.Equal(180, SmsMessages.Placed("0123456789").Length);
        Assert.Equal(180, SmsMessages.Cancelled("0123456789").Length);
        Assert.Equal(SmsMessages.Placed("0123456789"), SmsMessages.ForOrder("OrderPlaced", "0123456789"));
        Assert.Equal(SmsMessages.Ready("0123456789"), SmsMessages.ForOrder("OrderReady", "0123456789"));
        Assert.Throws<ApiError>(() => SmsMessages.ForOrder("unknown", "0123456789"));
        Assert.Contains("Código 000123", SmsMessages.Verification("000123"));
        Assert.Equal(183, Encoding.UTF8.GetByteCount(SmsMessages.Verification("000123")));
    }
    [Theory]
    [InlineData(401,"{}")]
    [InlineData(500,"{}")]
    [InlineData(200,"not-json")]
    [InlineData(200,"{}")]
    [InlineData(200,"{\"id\":\"test\",\"state\":\"Failed\"}")]
    public async Task Rejects_provider_failures_and_invalid_acknowledgements(int status, string body) {
        using var client = new HttpClient(new GatewayHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) })));
        await Assert.ThrowsAsync<HttpRequestException>(() => new HttpSmsGateway(client, Config()).Send("+525500000001",SmsMessages.Verification("000123"),"id",default));
    }
}
