using Foodies.Api;
using Xunit;
namespace Foodies.Tests;
public class RulesTests {
 [Fact] public void Estimate_is_per_order_with_three_prior_active_orders() => Assert.Equal(16, Rules.Estimate(10,3));
 [Theory]
 [InlineData(OrderStatus.Pendiente, true)][InlineData(OrderStatus.Aceptado,true)][InlineData(OrderStatus.EnPreparacion,true)]
 [InlineData(OrderStatus.Listo,false)][InlineData(OrderStatus.Entregado,false)][InlineData(OrderStatus.Rechazado,false)][InlineData(OrderStatus.Cancelado,false)][InlineData(OrderStatus.NoRecogido,false)]
 public void Queue_policy_is_explicit(OrderStatus status, bool active) => Assert.Equal(active, Rules.Active(status));
 [Theory]
 [InlineData(OrderStatus.Pendiente,OrderStatus.Listo)][InlineData(OrderStatus.Listo,OrderStatus.EnPreparacion)]
 [InlineData(OrderStatus.Entregado,OrderStatus.Listo)][InlineData(OrderStatus.Listo,OrderStatus.Cancelado)]
 public void No_skipped_or_unapproved_transitions(OrderStatus from,OrderStatus to) => Assert.False(Rules.CanTransition(from,to));
 [Theory]
 [InlineData(OrderStatus.Pendiente,true)][InlineData(OrderStatus.Aceptado,true)][InlineData(OrderStatus.EnPreparacion,true)]
 [InlineData(OrderStatus.Listo,false)][InlineData(OrderStatus.Entregado,false)][InlineData(OrderStatus.Rechazado,false)][InlineData(OrderStatus.NoRecogido,false)]
 public void Kitchen_cancellation_stops_before_ready(OrderStatus from,bool allowed)=>Assert.Equal(allowed,Rules.CanTransition(from,OrderStatus.Cancelado));
 [Fact] public void Hours_are_evaluated_in_store_timezone_and_end_is_exclusive() {
  var store=new Store{TimeZoneId="America/Mexico_City",Hours=[new(){DayOfWeek=1,OpeningTime=new(8,0),ClosingTime=new(18,0)}]};
  Assert.Equal("Cerrada",Rules.OperationalState(store,DateTimeOffset.Parse("2026-11-02T13:59:00Z")));
  Assert.Equal("Abierta",Rules.OperationalState(store,DateTimeOffset.Parse("2026-11-02T14:00:00Z")));
  Assert.Equal("Cerrada",Rules.OperationalState(store,DateTimeOffset.Parse("2026-11-03T00:00:00Z")));
  store.IsPaused=true;Assert.Equal("Pausada",Rules.OperationalState(store,DateTimeOffset.Parse("2026-11-02T15:00:00Z")));
 }
 [Fact] public void Overnight_and_split_hours_work() {
  var s=new Store{TimeZoneId="UTC",Hours=[new(){DayOfWeek=1,OpeningTime=new(22,0),ClosingTime=new(2,0)},new(){DayOfWeek=2,OpeningTime=new(8,0),ClosingTime=new(12,0)},new(){DayOfWeek=2,OpeningTime=new(14,0),ClosingTime=new(18,0)}]};
  Assert.Equal("Abierta",Rules.OperationalState(s,DateTimeOffset.Parse("2026-11-03T01:30:00Z")));
  Assert.Equal("Cerrada",Rules.OperationalState(s,DateTimeOffset.Parse("2026-11-03T02:00:00Z")));
  Assert.Equal("Cerrada",Rules.OperationalState(s,DateTimeOffset.Parse("2026-11-03T13:00:00Z")));
  Assert.Equal("Abierta",Rules.OperationalState(s,DateTimeOffset.Parse("2026-11-03T14:00:00Z")));
 }
 [Fact] public void Event_hub_never_crosses_store_boundaries() {
  var hub=new EventHub();var a=Guid.NewGuid();var b=Guid.NewGuid();var s1=hub.Subscribe(a);var s2=hub.Subscribe(b);
  hub.Publish(a,"order.created",Guid.NewGuid());Assert.True(s1.Reader.TryRead(out _));Assert.False(s2.Reader.TryRead(out _));hub.Remove(s1.Id);hub.Remove(s2.Id);
 }
}
