using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Foodies.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Foodies.Tests;
public sealed class SqlFactory(string environment = "Development", string? publicOrigin = null, bool configureGoogle = false, bool allowUnverified = false, bool smsEnabled = false) : WebApplicationFactory<Program> {
 public static string Root { get { var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"SPECIFICATION.md")))d=d.Parent;return d?.FullName??throw new Exception("Repository root not found"); } }
 protected override void ConfigureWebHost(IWebHostBuilder builder) {
  var local=new ConfigurationBuilder().AddJsonFile(Path.Combine(Root,"backend","Foodies.Api","appsettings.Local.json"),true).AddEnvironmentVariables().Build();
  var connection=new SqlConnectionStringBuilder(local.GetConnectionString("Foodies")??throw new Exception("Configure ConnectionStrings__Foodies or appsettings.Local.json to run SQL integration tests.")){InitialCatalog="Foodies_Tests"};
  builder.UseEnvironment(environment).UseContentRoot(Path.Combine(Root,"backend","Foodies.Api"));
  builder.ConfigureAppConfiguration((_,c)=>{
   var values=new Dictionary<string,string?>{{"Media:StoragePath",Path.Combine(Root,".runtime","test-media")},{"ConnectionStrings:Foodies",connection.ConnectionString},{"DevelopmentAccess:Enabled","true"},{"Sms:Enabled",smsEnabled.ToString()},{"Sms:Username",""},{"Sms:Password",""},{"PhoneVerification:AllowUnverifiedOrders",allowUnverified.ToString()}};
   if(publicOrigin is not null){values["PublicOrigin"]=publicOrigin;values["AllowedHosts"]=new Uri(publicOrigin).Host;}
   if(configureGoogle){values["Google:ClientId"]="test-client-id";values["Google:ClientSecret"]="test-secret-only";}
   c.AddInMemoryCollection(values);
  });
 }
}
public class ApiTests : IAsyncLifetime {
 readonly SqlFactory factory=new();HttpClient customer=null!,manager=null!,kitchen=null!,other=null!;
 Guid storeId,dishId,otherStore,otherDish,customerId;
 public async Task InitializeAsync() {
  using(var scope=factory.Services.CreateScope()) {
   var db=scope.ServiceProvider.GetRequiredService<FoodiesDb>();
   Assert.Equal("Foodies_Tests",new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog);
   await db.Database.MigrateAsync();await Seed.Run(db,true);
   var u=await db.Users.SingleAsync(x=>x.GoogleSubject=="dev:customer");u.PhoneE164="+525500000001";u.PhoneVerifiedAt=DateTimeOffset.UtcNow;customerId=u.Id;
   var second=await db.Users.SingleAsync(x=>x.GoogleSubject=="dev:manager-two");second.PhoneE164="+525500000002";second.PhoneVerifiedAt=DateTimeOffset.UtcNow;
   var s=TestStore();var t=TestStore();db.Stores.AddRange(s,t);storeId=s.Id;otherStore=t.Id;
   var c=new Category{StoreId=s.Id,Name="Comida"};var c2=new Category{StoreId=t.Id,Name="Otros"};db.Categories.AddRange(c,c2);
   var d=new Dish{StoreId=s.Id,CategoryId=c.Id,Name="Bowl original",Price=100};var d2=new Dish{StoreId=t.Id,CategoryId=c2.Id,Name="Otro bowl",Price=90};db.Dishes.AddRange(d,d2);dishId=d.Id;otherDish=d2.Id;
   foreach(var sub in new[]{"dev:manager-one","dev:kitchen-one"}){var actor=await db.Users.SingleAsync(x=>x.GoogleSubject==sub);db.Memberships.Add(new(){StoreId=s.Id,UserId=actor.Id,Role=sub.Contains("manager")?StoreRole.Manager:StoreRole.Kitchen});}
   await db.SaveChangesAsync();
  }
  customer=await Login("customer");manager=await Login("manager");kitchen=await Login("kitchen");other=await Login("manager-two");
 }
 static Store TestStore()=>new(){Name="Test "+Guid.NewGuid().ToString("N"),Slug="test-"+Guid.NewGuid().ToString("N"),TimeZoneId="UTC",AveragePreparationMinutes=10,Hours=Enumerable.Range(0,7).Select(d=>new BusinessHour{DayOfWeek=d,OpeningTime=TimeOnly.MinValue,ClosingTime=TimeOnly.MaxValue}).ToList()};
 async Task<HttpClient> Login(string account){var c=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});await Csrf(c);var r=await c.PostAsync("/api/v1/auth/development/"+account,null);Assert.Equal(HttpStatusCode.OK,r.StatusCode);await Csrf(c);return c;}
 static async Task Csrf(HttpClient c){var s=await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/session");c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",s.GetProperty("csrfToken").GetString());}
 async Task<HttpResponseMessage> Place(string? key=null,Guid? store=null,Guid? dish=null,decimal price=100,int quantity=1,HttpClient? client=null){using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/orders");request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());request.Content=JsonContent.Create(new{storeId=store??storeId,items=new[]{new{dishId=dish??dishId,quantity,expectedUnitPrice=price}}});return await (client??customer).SendAsync(request);}
 async Task<Guid> NewOrder(){var r=await Place();Assert.Equal(HttpStatusCode.Created,r.StatusCode);return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();}
 async Task<HttpResponseMessage> Transition(Guid id,string action,HttpClient? actor=null,object? body=null)=>await (actor??manager).PostAsJsonAsync($"/api/v1/admin/stores/{storeId}/orders/{id}/{action}",body);
 async Task Mutate(Func<FoodiesDb,Task> change){using var s=factory.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<FoodiesDb>();await change(db);await db.SaveChangesAsync();}
 [Fact] public async Task Public_catalog_and_real_cookie_csrf_protection(){using var anonymous=factory.CreateClient();Assert.Equal(HttpStatusCode.OK,(await anonymous.GetAsync("/api/v1/stores")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/v1/me/orders")).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsync("/api/v1/auth/development/customer",null)).StatusCode);var menu=await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/stores/{storeId}/dishes");Assert.Contains(menu.GetProperty("dishes").EnumerateArray(),d=>d.GetProperty("id").GetGuid()==dishId);}
 [Fact] public async Task Full_cycle_snapshots_eta_and_unique_ready_outbox(){for(var i=0;i<3;i++)await NewOrder();var unrelated=await Place(store:otherStore,dish:otherDish,price:90);Assert.Equal(HttpStatusCode.Created,unrelated.StatusCode);var id=await NewOrder();
  var accepted=await Transition(id,"accept");Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);var json=await accepted.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(16,json.GetProperty("estimatedPreparationSnapshotMin").GetInt32());Assert.Equal(3,json.GetProperty("activeOrdersAheadSnapshot").GetInt32());
  await Mutate(async db=>{var d=await db.Dishes.FindAsync(dishId);d!.Name="Precio cambiado";d.Price=120;});
  Assert.Equal(HttpStatusCode.Conflict,(await Transition(id,"delivered")).StatusCode);
  foreach(var action in new[]{"start-preparation","ready","ready","delivered"})Assert.Equal(HttpStatusCode.OK,(await Transition(id,action)).StatusCode);
  using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FoodiesDb>();var order=await db.Orders.Include(o=>o.Items).Include(o=>o.History).SingleAsync(o=>o.Id==id);
  Assert.Equal(OrderStatus.Entregado,order.Status);Assert.Equal(100,order.Total);Assert.Equal("Bowl original",order.Items.Single().NameSnapshot);Assert.Equal(100,order.Items.Single().UnitPriceSnapshot);Assert.Equal(5,order.History.Count);Assert.Single(await db.SmsDeliveries.Where(x=>x.OrderId==id).ToListAsync());Assert.Equal(16,order.EstimatedPreparationSnapshotMin);
 }
 [Fact] public async Task Concurrent_confirmations_create_exactly_one_order_and_conflicting_payload_fails(){var key=Guid.NewGuid().ToString();var responses=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>Place(key)));Assert.All(responses,r=>Assert.Equal(HttpStatusCode.Created,r.StatusCode));var ids=new List<Guid>();foreach(var r in responses)ids.Add((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());Assert.Single(ids.Distinct());Assert.Equal(HttpStatusCode.Conflict,(await Place(key,quantity:2)).StatusCode);}
 [Fact] public async Task Concurrent_acceptance_does_not_duplicate_history(){var id=await NewOrder();var results=await Task.WhenAll(Transition(id,"accept"),Transition(id,"accept"));Assert.All(results,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FoodiesDb>();Assert.Equal(2,(await db.Orders.Include(x=>x.History).SingleAsync(x=>x.Id==id)).History.Count);}
 [Fact] public async Task Server_revalidates_prices_stock_minimum_hours_phone_and_store(){
  Assert.Equal(HttpStatusCode.Conflict,(await Place(price:99)).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Place(dish:otherDish,price:90)).StatusCode);
  await Mutate(async db=>{(await db.Stores.FindAsync(storeId))!.MinimumOrderAmount=200;});Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Place()).StatusCode);
  await Mutate(async db=>{var s=await db.Stores.FindAsync(storeId);s!.MinimumOrderAmount=0;s.IsPaused=true;});Assert.Equal(HttpStatusCode.Conflict,(await Place()).StatusCode);
  await Mutate(async db=>{(await db.Stores.FindAsync(storeId))!.IsPaused=false;(await db.Dishes.FindAsync(dishId))!.IsAvailable=false;});Assert.Equal(HttpStatusCode.Conflict,(await Place()).StatusCode);
  await Mutate(async db=>{(await db.Dishes.FindAsync(dishId))!.IsAvailable=true;var s=await db.Stores.Include(x=>x.Hours).SingleAsync(x=>x.Id==storeId);s.Hours.Clear();});Assert.Equal(HttpStatusCode.Conflict,(await Place()).StatusCode);
  await Mutate(async db=>{(await db.Users.FindAsync(customerId))!.PhoneVerifiedAt=null;});Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Place()).StatusCode);
 }
 [Fact] public async Task Store_and_customer_ownership_are_enforced_on_every_surface(){var id=await NewOrder();foreach(var suffix in new[]{"orders","dishes","categories","settings","events"})Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync($"/api/v1/admin/stores/{storeId}/{suffix}")).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/v1/me/orders/{id}")).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsync($"/api/v1/admin/stores/{storeId}/orders/{id}/accept",null)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await kitchen.PatchAsJsonAsync($"/api/v1/admin/stores/{storeId}/settings",new SettingsInput("Store","","","UTC",true,false,0,10,[]))).StatusCode);
  using var s=factory.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<FoodiesDb>();var c=await db.Categories.FirstAsync(x=>x.StoreId==storeId);
  Assert.Equal(HttpStatusCode.NotFound,(await manager.PatchAsJsonAsync($"/api/v1/admin/stores/{storeId}/dishes/{otherDish}",new DishInput(c.Id,"x","",1,null,true,true,0))).StatusCode);
 }
 [Fact] public async Task Reject_requires_reason_and_never_schedules_ready_sms(){var id=await NewOrder();Assert.Equal(HttpStatusCode.BadRequest,(await Transition(id,"reject",body:new{reason=""})).StatusCode);Assert.Equal(HttpStatusCode.OK,(await Transition(id,"reject",body:new{reason="Sin ingredientes"})).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await Transition(id,"ready")).StatusCode);using var s=factory.Services.CreateScope();Assert.False(await s.ServiceProvider.GetRequiredService<FoodiesDb>().SmsDeliveries.AnyAsync(x=>x.OrderId==id));}
 [Fact] public async Task Unconfigured_gateway_never_pretends_phone_is_verified(){var r=await customer.PostAsJsonAsync("/api/v1/me/phone-verifications",new{phoneE164="+525512345678"});Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);Assert.Equal("sms_unconfigured",(await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());}
 [Fact] public async Task Manual_phone_can_be_saved_then_used_for_order_without_verifying_or_queuing_sms(){
  using var temporary = new SqlFactory(allowUnverified:true);using var c=temporary.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
  await Csrf(c);Assert.Equal(HttpStatusCode.OK,(await c.PostAsync("/api/v1/auth/development/customer",null)).StatusCode);await Csrf(c);
  var state=await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/session");Assert.False(state.GetProperty("phoneVerificationRequired").GetBoolean());
  Assert.Equal(HttpStatusCode.BadRequest,(await c.PatchAsJsonAsync("/api/v1/me/phone",new{phoneE164="5512345678"})).StatusCode);
  await Mutate(async db=>{db.PhoneCodes.Add(new(){UserId=customerId,PhoneE164="+525500000088",Salt="salt",CodeHash=Rules.Hash("salt123456"),ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(5)});await Task.CompletedTask;});
  Assert.Equal(HttpStatusCode.NoContent,(await c.PatchAsJsonAsync("/api/v1/me/phone",new{phoneE164="+525500000077"})).StatusCode);
  var profile=await c.GetFromJsonAsync<JsonElement>("/api/v1/me");Assert.Equal("+525500000077",profile.GetProperty("phoneE164").GetString());Assert.Equal(JsonValueKind.Null,profile.GetProperty("phoneVerifiedAt").ValueKind);
  // Strict mode remains enforced for the same saved but unverified phone.
  Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Place()).StatusCode);
  var placed=await Place(client:c);Assert.Equal(HttpStatusCode.Created,placed.StatusCode);var id=(await placed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  foreach(var action in new[]{"accept","start-preparation","ready"})Assert.Equal(HttpStatusCode.OK,(await Transition(id,action)).StatusCode);
  using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FoodiesDb>();Assert.False(await db.SmsDeliveries.AnyAsync(x=>x.OrderId==id));Assert.False(await db.PhoneCodes.AnyAsync(x=>x.UserId==customerId&&x.ConsumedAt==null));
  await Mutate(async db=>(await db.Users.FindAsync(customerId))!.PhoneE164=null);
  Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Place(client:c)).StatusCode);
 }
 [Fact] public async Task Strict_phone_policy_blocks_direct_save_and_csrf_protects_manual_save(){
  Assert.Equal(HttpStatusCode.Conflict,(await customer.PatchAsJsonAsync("/api/v1/me/phone",new{phoneE164="+525500000077"})).StatusCode);
  using var temporary=new SqlFactory(allowUnverified:true);using var c=temporary.CreateClient();await Csrf(c);await c.PostAsync("/api/v1/auth/development/customer",null);await Csrf(c);c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await c.PatchAsJsonAsync("/api/v1/me/phone",new{phoneE164="+525500000077"})).StatusCode);
 }
 [Fact] public async Task Otp_attempts_expiry_consumption_and_phone_changes_are_enforced(){
  var salt=Guid.NewGuid().ToString("N");Guid codeId=Guid.Empty;
  await Mutate(async db=>{var old=await db.PhoneCodes.Where(x=>x.UserId==customerId&&x.ConsumedAt==null).ToListAsync();foreach(var c in old)c.ConsumedAt=DateTimeOffset.UtcNow;var record=new PhoneCode{UserId=customerId,PhoneE164="+525500000099",Salt=salt,CodeHash=Rules.Hash(salt+"123456"),ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(5)};codeId=record.Id;db.PhoneCodes.Add(record);});
  for(var i=0;i<5;i++)Assert.Equal(HttpStatusCode.UnprocessableEntity,(await customer.PostAsJsonAsync("/api/v1/me/phone-verifications/confirm",new{code="000000"})).StatusCode);
  Assert.Equal(HttpStatusCode.UnprocessableEntity,(await customer.PostAsJsonAsync("/api/v1/me/phone-verifications/confirm",new{code="123456"})).StatusCode);
  await Mutate(async db=>{var c=await db.PhoneCodes.FindAsync(codeId);c!.Attempts=0;c.ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(-1);});
  Assert.Equal(HttpStatusCode.UnprocessableEntity,(await customer.PostAsJsonAsync("/api/v1/me/phone-verifications/confirm",new{code="123456"})).StatusCode);
  await Mutate(async db=>(await db.PhoneCodes.FindAsync(codeId))!.ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(5));
  Assert.Equal(HttpStatusCode.NoContent,(await customer.PostAsJsonAsync("/api/v1/me/phone-verifications/confirm",new{code="123456"})).StatusCode);
  Assert.Equal(HttpStatusCode.UnprocessableEntity,(await customer.PostAsJsonAsync("/api/v1/me/phone-verifications/confirm",new{code="123456"})).StatusCode);
  using var scope=factory.Services.CreateScope();var user=await scope.ServiceProvider.GetRequiredService<FoodiesDb>().Users.FindAsync(customerId);Assert.Equal("+525500000099",user!.PhoneE164);Assert.NotNull(user.PhoneVerifiedAt);
 }
 [Fact] public async Task Unsafe_requests_without_csrf_cannot_update_authenticated_resources(){customer.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await Place()).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await customer.PatchAsJsonAsync("/api/v1/me",new{displayName="Alterado"})).StatusCode);}
 [Fact] public async Task Sensitive_routes_are_rate_limited(){for(var i=0;i<12;i++)Assert.Equal(HttpStatusCode.Conflict,(await Place(price:99)).StatusCode);Assert.Equal(HttpStatusCode.TooManyRequests,(await Place()).StatusCode);}
 [Fact] public async Task Production_google_challenge_uses_public_https_origin_and_dev_access_is_absent(){
  // Configure the provider before Program registers authentication, exactly as in deployment.
  var oldId=Environment.GetEnvironmentVariable("Google__ClientId");var oldSecret=Environment.GetEnvironmentVariable("Google__ClientSecret");
  try {Environment.SetEnvironmentVariable("Google__ClientId","test-client-id");Environment.SetEnvironmentVariable("Google__ClientSecret","test-secret-only");
  using var prod=new SqlFactory("Production","https://foodies.example",true);
  using var c=prod.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false,BaseAddress=new Uri("https://foodies.example")});
  var session=await c.GetAsync("/api/v1/auth/session");Assert.Equal(HttpStatusCode.OK,session.StatusCode);Assert.False((await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("developmentAccess").GetBoolean());Assert.Contains(session.Headers.GetValues("Set-Cookie"),cookie=>cookie.Contains("secure",StringComparison.OrdinalIgnoreCase));
  var challenge=await c.GetAsync("/api/v1/auth/google/start?returnUrl=%2Fmis-pedidos");Assert.Equal(HttpStatusCode.Redirect,challenge.StatusCode);
  var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
  Assert.Equal("https://foodies.example/api/v1/auth/google/callback",query["redirect_uri"].ToString());Assert.Equal("S256",query["code_challenge_method"].ToString());Assert.False(string.IsNullOrEmpty(query["state"].ToString()));
  Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync("/api/v1/auth/development/customer")).StatusCode);
  } finally {Environment.SetEnvironmentVariable("Google__ClientId",oldId);Environment.SetEnvironmentVariable("Google__ClientSecret",oldSecret);}
 }
 [Fact] public async Task Sse_stream_delivers_new_order_to_authorized_store(){using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(12));using var stream=await manager.GetAsync($"/api/v1/admin/stores/{storeId}/events",HttpCompletionOption.ResponseHeadersRead,cts.Token);Assert.Equal(HttpStatusCode.OK,stream.StatusCode);Assert.Equal("text/event-stream",stream.Content.Headers.ContentType!.MediaType);using var reader=new StreamReader(await stream.Content.ReadAsStreamAsync(cts.Token));Assert.Equal("retry: 5000",await reader.ReadLineAsync(cts.Token));await NewOrder();var found=false;for(var i=0;i<20;i++){var line=await reader.ReadLineAsync(cts.Token);if(line=="event: order.created"){found=true;break;}}Assert.True(found);}
 [Fact] public async Task Kitchen_phone_is_private_validated_and_queues_only_created_and_ready_once() {
  var path=$"/api/v1/admin/stores/{storeId}/settings";
  var settings=(await manager.GetFromJsonAsync<SettingsInput>(path))!;
  Assert.Equal(HttpStatusCode.BadRequest,(await manager.PatchAsJsonAsync(path,settings with {KitchenPhoneE164="3312345678"})).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await kitchen.PatchAsJsonAsync(path,settings with {KitchenPhoneE164="+525500000099"})).StatusCode);
  var changed=await manager.PatchAsJsonAsync(path,settings with {KitchenPhoneE164="+525500000099"});
  Assert.Equal(HttpStatusCode.OK,changed.StatusCode);
  Assert.Equal("+525500000099",(await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("kitchenPhoneE164").GetString());
  string slug;using(var scope=factory.Services.CreateScope())slug=await scope.ServiceProvider.GetRequiredService<FoodiesDb>().Stores.Where(s=>s.Id==storeId).Select(s=>s.Slug).SingleAsync();
  using var anonymous=factory.CreateClient();var publicStore=await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/stores/{slug}");
  Assert.False(publicStore.TryGetProperty("kitchenPhoneE164",out _));
  var key=Guid.NewGuid().ToString();var responses=await Task.WhenAll(Place(key),Place(key));
  var id=(await responses[0].Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  foreach(var action in new[]{"accept","start-preparation","ready","ready","delivered"})Assert.Equal(HttpStatusCode.OK,(await Transition(id,action)).StatusCode);
  using(var scope=factory.Services.CreateScope()) {
   var deliveries=await scope.ServiceProvider.GetRequiredService<FoodiesDb>().SmsDeliveries.Where(x=>x.OrderId==id).ToListAsync();
   Assert.Equal(2,deliveries.Count);Assert.Equal("+525500000099",Assert.Single(deliveries,x=>x.TemplateType=="OrderPlaced").PhoneE164);
   Assert.Equal("+525500000001",Assert.Single(deliveries,x=>x.TemplateType=="OrderReady").PhoneE164);
  }
  Assert.Equal(HttpStatusCode.OK,(await manager.PatchAsJsonAsync(path,settings with {KitchenPhoneE164=null})).StatusCode);
  var withoutPhone=await NewOrder();using(var scope=factory.Services.CreateScope())Assert.False(await scope.ServiceProvider.GetRequiredService<FoodiesDb>().SmsDeliveries.AnyAsync(x=>x.OrderId==withoutPhone));
 }
 [Theory]
 [InlineData(0)][InlineData(1)][InlineData(2)]
 public async Task Kitchen_can_cancel_before_ready_with_reason_and_one_customer_sms(int stage) {
  var id=await NewOrder();
  if(stage>=1)Assert.Equal(HttpStatusCode.OK,(await Transition(id,"accept")).StatusCode);
  if(stage>=2)Assert.Equal(HttpStatusCode.OK,(await Transition(id,"start-preparation")).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Transition(id,"cancel",kitchen,new{reason="  "})).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await Transition(id,"cancel",kitchen,new{reason=new string('x',501)})).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Transition(id,"cancel",other,new{reason="No corresponde"})).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Transition(id,"cancel",customer,new{reason="No autorizado"})).StatusCode);
  var responses=await Task.WhenAll(Transition(id,"cancel",kitchen,new{reason="  Se agotaron los ingredientes  "}),Transition(id,"cancel",kitchen,new{reason="  Se agotaron los ingredientes  "}));
  Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
  var detail=await customer.GetFromJsonAsync<JsonElement>($"/api/v1/me/orders/{id}");
  Assert.Equal("Cancelado",detail.GetProperty("status").GetString());
  Assert.Equal("Se agotaron los ingredientes",detail.GetProperty("history").EnumerateArray().Single(h=>h.GetProperty("newStatus").GetString()=="Cancelado").GetProperty("reason").GetString());
  Assert.Equal(HttpStatusCode.Conflict,(await Transition(id,"accept")).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await Transition(id,"ready")).StatusCode);
  using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FoodiesDb>();
  var jobs=await db.SmsDeliveries.Where(x=>x.OrderId==id).ToListAsync();
  var job=Assert.Single(jobs);Assert.Equal("OrderCancelled",job.TemplateType);Assert.Equal("+525500000001",job.PhoneE164);
  var stored=await db.Orders.Include(x=>x.History).SingleAsync(x=>x.Id==id);
  Assert.Single(stored.History,h=>h.NewStatus==OrderStatus.Cancelado);
 }
 [Fact] public async Task Cancellation_is_blocked_after_ready_and_for_unverified_phone_no_sms_is_queued() {
  var id=await NewOrder();foreach(var action in new[]{"accept","start-preparation","ready"})await Transition(id,action);
  Assert.Equal(HttpStatusCode.Conflict,(await Transition(id,"cancel",body:new{reason="Ya listo"})).StatusCode);
  await Transition(id,"delivered");Assert.Equal(HttpStatusCode.Conflict,(await Transition(id,"cancel",body:new{reason="Ya entregado"})).StatusCode);
  var rejected=await NewOrder();await Transition(rejected,"reject",body:new{reason="Sin existencias"});
  Assert.Equal(HttpStatusCode.Conflict,(await Transition(rejected,"cancel",body:new{reason="Cerrado"})).StatusCode);
  var pending=await NewOrder();await Mutate(async db=>(await db.Users.FindAsync(customerId))!.PhoneVerifiedAt=null);
  Assert.Equal(HttpStatusCode.OK,(await Transition(pending,"cancel",body:new{reason="Sin existencias"})).StatusCode);
  using var scope=factory.Services.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<FoodiesDb>().SmsDeliveries.AnyAsync(x=>x.OrderId==pending));
 }
 [Fact] public async Task Cover_is_manager_editable_validated_public_and_independent_per_store() {
  var path=$"/api/v1/admin/stores/{storeId}/settings";var settings=(await manager.GetFromJsonAsync<SettingsInput>(path))!;
  foreach(var invalid in new[]{"http://example.test/cover.jpg","data:image/png;base64,invalid","https://user:password@example.test/cover.jpg","https://example.test/"+new string('x',500)})
   Assert.Equal(HttpStatusCode.BadRequest,(await manager.PatchAsJsonAsync(path,settings with {CoverImageUrl=invalid})).StatusCode);
  const string cover="https://example.test/store-cover.jpg";
  Assert.Equal(HttpStatusCode.Forbidden,(await kitchen.PatchAsJsonAsync(path,settings with {CoverImageUrl=cover})).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await other.PatchAsJsonAsync(path,settings with {CoverImageUrl=cover})).StatusCode);
  var changed=await manager.PatchAsJsonAsync(path,settings with {CoverImageUrl="  "+cover+"  "});Assert.Equal(HttpStatusCode.OK,changed.StatusCode);
  Assert.Equal(cover,(await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("coverImageUrl").GetString());
  using var anonymous=factory.CreateClient();var catalog=await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/stores");
  var selected=catalog.EnumerateArray().Single(s=>s.GetProperty("id").GetGuid()==storeId);
  Assert.Equal(cover,selected.GetProperty("coverImageUrl").GetString());
  Assert.Equal(JsonValueKind.Null,catalog.EnumerateArray().Single(s=>s.GetProperty("id").GetGuid()==otherStore).GetProperty("coverImageUrl").ValueKind);
  var detail=await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/stores/"+selected.GetProperty("slug").GetString());Assert.Equal(cover,detail.GetProperty("coverImageUrl").GetString());
  Assert.Equal(HttpStatusCode.OK,(await manager.PatchAsJsonAsync(path,settings with {CoverImageUrl=null})).StatusCode);
  Assert.Equal(JsonValueKind.Null,(await manager.GetFromJsonAsync<JsonElement>(path)).GetProperty("coverImageUrl").ValueKind);
 }
 public static readonly byte[] CoverPng=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=");
 public static MultipartFormDataContent ImageBody(byte[] bytes,string name="cover.png") {var body=new MultipartFormDataContent();body.Add(new ByteArrayContent(bytes),"file",name);return body;}
 [Fact] public async Task Image_upload_is_scoped_csrf_protected_public_and_persists_across_hosts() {
  var path=$"/api/v1/admin/stores/{storeId}/cover-image";
  foreach(var actor in new[]{kitchen,other,customer}){using var body=ImageBody(CoverPng);Assert.Equal(HttpStatusCode.Forbidden,(await actor.PostAsync(path,body)).StatusCode);}
  manager.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");using(var body=ImageBody(CoverPng))Assert.Equal(HttpStatusCode.BadRequest,(await manager.PostAsync(path,body)).StatusCode);await Csrf(manager);
  using var uploadBody=ImageBody(CoverPng,"../../business.svg");var response=await manager.PostAsync(path,uploadBody);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
  var url=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
  Assert.Matches($"^/api/v1/media/stores/{storeId:N}/[a-f0-9]{{32}}\\.png$",url);
  var settings=(await manager.GetFromJsonAsync<SettingsInput>($"/api/v1/admin/stores/{storeId}/settings"))!;
  Assert.Equal(HttpStatusCode.OK,(await manager.PatchAsJsonAsync($"/api/v1/admin/stores/{storeId}/settings",settings with{CoverImageUrl=url})).StatusCode);
  var foreign=url.Replace(storeId.ToString("N"),otherStore.ToString("N"));
  Assert.Equal(HttpStatusCode.BadRequest,(await manager.PatchAsJsonAsync($"/api/v1/admin/stores/{storeId}/settings",settings with{CoverImageUrl=foreign})).StatusCode);
  using var anotherHost=new SqlFactory();using var anonymous=anotherHost.CreateClient();var image=await anonymous.GetAsync(url);
  Assert.Equal(HttpStatusCode.OK,image.StatusCode);Assert.Equal("image/png",image.Content.Headers.ContentType!.MediaType);Assert.Equal(CoverPng,await image.Content.ReadAsByteArrayAsync());
  Assert.Contains("immutable",image.Headers.CacheControl!.ToString());Assert.Equal("nosniff",Assert.Single(image.Headers.GetValues("X-Content-Type-Options")));
  Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync($"/api/v1/media/stores/{storeId:N}/appsettings.Local.json")).StatusCode);
 }
 [Fact] public async Task Upload_rejects_empty_svg_invalid_signature_and_large_files() {
  var path=$"/api/v1/admin/stores/{storeId}/cover-image";
  using(var body=ImageBody([]))Assert.Equal(HttpStatusCode.BadRequest,(await manager.PostAsync(path,body)).StatusCode);
  using(var body=ImageBody(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'></svg>"),"pretend.png"))Assert.Equal(HttpStatusCode.UnsupportedMediaType,(await manager.PostAsync(path,body)).StatusCode);
  using(var body=ImageBody(new byte[(int)MediaStorage.MaxBytes+1]))Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await manager.PostAsync(path,body)).StatusCode);
 }
 public Task DisposeAsync(){customer?.Dispose();manager?.Dispose();kitchen?.Dispose();other?.Dispose();factory.Dispose();return Task.CompletedTask;}
}
