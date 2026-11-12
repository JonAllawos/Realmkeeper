using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>
    /// Moves real goods along trade routes. Each day:
    /// 1. Arrivals: shipments due today hand their goods to the destination (perishables lose
    ///    the route's transit loss once). Goods that do not fit wait on the road; after
    ///    <see cref="EconomyRules.MaxWaitDays"/> days they travel back to the origin. The
    ///    destination pays the origin on delivery, limited by the coin it has (no debt).
    /// 2. Departures: a route due for a departure with no shipment on the road picks the good
    ///    the destination needs most that the origin can spare. Checks, in order: origin surplus
    ///    above its reserve; route capacity; destination free storage (including goods already
    ///    heading there); what the destination can pay for. Then the goods leave the origin.
    /// 3. Conservation: per resource, stock + goods on the road must equal the start-of-step total
    ///    minus recorded transit loss, and total coin must be unchanged. Otherwise the system
    ///    throws and the scheduler discards the whole day.
    /// </summary>
    public sealed class TradeSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.trade";

        private readonly EconomyRules _rules;

        public TradeSystem(EconomyRules rules) { _rules = rules ?? throw new ArgumentNullException(nameof(rules)); }

        public string SystemId => Id;
        public int ExecutionOrder => 150;

        public void Execute(SimulationDayContext context)
        {
            var world = context.World;
            var day = context.Date.ToDayNumber();
            var settlements = new Dictionary<SettlementId, SettlementData>();
            foreach (var s in world.Settlements) settlements[s.SettlementId] = s;

            var before = Totals(world);
            var lost = new Dictionary<ResourceTypeId, long>();

            foreach (var s in world.Settlements)
            {
                var e = s.Economy;
                e.TradeActivity -= (e.TradeActivity + _rules.TradeActivityDecayDays - 1) / _rules.TradeActivityDecayDays;
            }

            ProcessArrivals(context, settlements, day, lost);
            ProcessDepartures(context, settlements, day);

            var after = Totals(world);
            foreach (var resource in ResourceTypes.All)
            {
                before.TryGetValue(resource, out var b);
                after.TryGetValue(resource, out var a);
                lost.TryGetValue(resource, out var l);
                if (a + l != b)
                {
                    throw new InvalidOperationException(
                        $"Trade broke conservation of {resource}: before {b}, after {a}, recorded loss {l}.");
                }
            }
        }

        // ----- Arrivals ---------------------------------------------------------------------

        private void ProcessArrivals(SimulationDayContext context, Dictionary<SettlementId, SettlementData> settlements, long day,
            Dictionary<ResourceTypeId, long> lost)
        {
            var shipments = context.World.Shipments;
            for (var i = 0; i < shipments.Count; i++)
            {
                var shipment = shipments[i];
                if (shipment.ArrivalDay > day) continue;

                var route = context.World.FindTradeRoute(shipment.TradeRouteId)
                            ?? throw new InvalidOperationException($"Shipment {shipment.ShipmentId} has no route.");
                if (!string.IsNullOrEmpty(route.BlockedBy) && !shipment.IsReturning) { shipment.ArrivalDay = day + 1; continue; } // Phase 16: the way is closed; the goods wait on the road
                var destination = Lookup(settlements, shipment.DestinationId);
                var origin = Lookup(settlements, shipment.OriginId);
                var resource = shipment.ResourceTypeId;

                if (!shipment.LossApplied)
                {
                    shipment.LossApplied = true;
                    if (_rules.IsPerishable(resource) && !shipment.IsReturning)
                    {
                        var arriving = (int)((long)shipment.Quantity * route.EfficiencyPercent / 100);
                        var loss = shipment.Quantity - arriving;
                        shipment.Quantity = arriving;
                        shipment.LostQuantity += loss;
                        lost.TryGetValue(resource, out var l);
                        lost[resource] = l + loss;
                    }

                    // Phase 14 (ADR-0071): outlaws holding the road take their cut of every shipment that passes.
                    var cut = Places.RoadThreat.PercentTaken(context.World, route);
                    if (cut > 0 && !shipment.IsReturning)
                    {
                        var kept = (int)((long)shipment.Quantity * (100 - cut) / 100);
                        var taken = shipment.Quantity - kept;
                        shipment.Quantity = kept;
                        shipment.LostQuantity += taken;
                        EconomyMath.Flow(destination.Economy, resource, day).LostOnRoad += taken;
                        lost.TryGetValue(resource, out var t);
                        lost[resource] = t + taken;
                    }
                }

                var free = (long)destination.Economy.CapacityOf(resource) - destination.ResourceInventory.Get(resource);
                var accept = (int)Math.Max(0, Math.Min(shipment.Quantity, free));
                if (accept > 0)
                {
                    destination.ResourceInventory.Add(resource, accept);
                    EconomyMath.Flow(destination.Economy, resource, day).Imported += accept;
                    shipment.Quantity -= accept;
                    shipment.DeliveredQuantity += accept;

                    if (!shipment.IsReturning)
                    {
                        var payment = Pay(destination, origin, accept * _rules.Price(resource), day);
                        shipment.Payment += payment;
                        var value = accept * _rules.ValueOf(resource);
                        destination.Economy.TradeActivity += value;
                        origin.Economy.TradeActivity += value;
                        route.Delivered.Add(resource, accept);
                    }
                }

                if (shipment.Quantity == 0)
                {
                    Complete(context, route, shipment, day);
                    shipments.RemoveAt(i);
                    i--;
                    continue;
                }

                // Destination full: wait, then turn back. Goods stay on the road, never lost.
                shipment.DaysWaiting++;
                route.Status = TradeRouteStatus.Blocked;
                if (!shipment.IsReturning && shipment.DaysWaiting > _rules.MaxWaitDays)
                {
                    shipment.IsReturning = true;
                    shipment.DestinationId = shipment.OriginId;
                    shipment.DaysWaiting = 0;
                    shipment.ArrivalDay = day + route.TravelDays;
                }
            }
        }

        private void Complete(SimulationDayContext context, TradeRouteData route, ShipmentData shipment, long day)
        {
            route.Status = route.IsSuspended ? TradeRouteStatus.Suspended : TradeRouteStatus.Active;
            var ids = new[] { route.OriginId, route.DestinationId };
            if (shipment.IsReturning)
            {
                context.ReportEvent(HistoricalEventType.TradeShipmentReturned, EventSeverity.Warning, EventImportance.Minor, ids,
                    new EventData().Set("route", route.TradeRouteId.ToString()).Set("resource", shipment.ResourceTypeId.Value)
                        .Set("quantity", shipment.DeliveredQuantity));
                return;
            }

            route.ShipmentsDelivered++;
            route.LastDeliveryDay = day;
            route.LastDeliveryResource = shipment.ResourceTypeId;
            route.LastDeliveryQuantity = shipment.DeliveredQuantity;
            route.LastDeliveryPayment = shipment.Payment;

            // Every delivery is recorded on the route; the chronicle gets at most one entry per route per interval.
            if (route.LastReportedDay < 0 || day - route.LastReportedDay >= _rules.TradeReportIntervalDays)
            {
                route.LastReportedDay = day;
                context.ReportEvent(HistoricalEventType.TradeShipmentDelivered, EventSeverity.Info, EventImportance.Minor, ids,
                    new EventData().Set("route", route.TradeRouteId.ToString()).Set("resource", shipment.ResourceTypeId.Value)
                        .Set("quantity", shipment.DeliveredQuantity).Set("lost", shipment.LostQuantity).Set("payment", shipment.Payment)
                        .Set("shipments", route.ShipmentsDelivered));
            }
        }

        /// <summary>Moves up to <paramref name="amount"/> coin from buyer to seller; returns what was paid.</summary>
        private static int Pay(SettlementData buyer, SettlementData seller, int amount, long day)
        {
            var available = buyer.ResourceInventory.Get(ResourceTypes.Coin);
            var room = (long)seller.Economy.CapacityOf(ResourceTypes.Coin) - seller.ResourceInventory.Get(ResourceTypes.Coin);
            var paid = (int)Math.Max(0, Math.Min(Math.Min(amount, available), room));
            if (paid == 0) return 0;
            if (!buyer.ResourceInventory.TryRemove(ResourceTypes.Coin, paid)) throw new InvalidOperationException("Payment exceeded the buyer's coin.");
            seller.ResourceInventory.Add(ResourceTypes.Coin, paid);
            EconomyMath.Flow(buyer.Economy, ResourceTypes.Coin, day).Exported += paid;
            EconomyMath.Flow(seller.Economy, ResourceTypes.Coin, day).Imported += paid;
            return paid;
        }

        // ----- Departures -------------------------------------------------------------------

        private void ProcessDepartures(SimulationDayContext context, Dictionary<SettlementId, SettlementData> settlements, long day)
        {
            var world = context.World;
            var onRoad = new HashSet<TradeRouteId>();
            var incoming = new Dictionary<(SettlementId, ResourceTypeId), long>();
            foreach (var s in world.Shipments)
            {
                onRoad.Add(s.TradeRouteId);
                incoming.TryGetValue((s.DestinationId, s.ResourceTypeId), out var q);
                incoming[(s.DestinationId, s.ResourceTypeId)] = q + s.Quantity;
            }

            foreach (var route in world.TradeRoutes)
            {
                // Status decides whether trade happens: a suspended route sends nothing. Its shipment
                // already on the road (if any) is still delivered by ProcessArrivals.
                if (route.IsSuspended)
                {
                    if (!onRoad.Contains(route.TradeRouteId)) route.Status = TradeRouteStatus.Suspended;
                    continue;
                }

                if (!string.IsNullOrEmpty(route.BlockedBy)) { if (!onRoad.Contains(route.TradeRouteId)) route.Status = TradeRouteStatus.Blocked; continue; } // Phase 16: nothing sets out on a closed way
                if (route.Status == TradeRouteStatus.Suspended) route.Status = TradeRouteStatus.Active; // resumed
                if (day < route.NextDepartureDay) continue;
                route.NextDepartureDay = day + route.FrequencyDays;
                if (onRoad.Contains(route.TradeRouteId)) continue; // one shipment per route at a time

                var origin = Lookup(settlements, route.OriginId);
                var destination = Lookup(settlements, route.DestinationId);
                var plan = ChooseCargo(origin, destination, route, incoming);
                if (plan.quantity < _rules.MinimumShipment)
                {
                    route.Status = TradeRouteStatus.Idle;
                    continue;
                }

                if (!origin.ResourceInventory.TryRemove(plan.resource, plan.quantity))
                    throw new InvalidOperationException($"Route {route.TradeRouteId} planned more {plan.resource} than the origin holds.");
                EconomyMath.Flow(origin.Economy, plan.resource, day).Exported += plan.quantity;

                var shipment = new ShipmentData
                {
                    ShipmentId = new ShipmentId(DeterministicGuid.Next(context.Random)),
                    TradeRouteId = route.TradeRouteId,
                    OriginId = route.OriginId,
                    DestinationId = route.DestinationId,
                    ResourceTypeId = plan.resource,
                    Quantity = plan.quantity,
                    DepartureDay = day,
                    ArrivalDay = day + route.TravelDays,
                };
                world.Shipments.Add(shipment);
                onRoad.Add(route.TradeRouteId);
                incoming.TryGetValue((route.DestinationId, plan.resource), out var already);
                incoming[(route.DestinationId, plan.resource)] = already + plan.quantity;
                route.Status = TradeRouteStatus.InTransit;
            }
        }

        /// <summary>
        /// Picks the shipment for a route: for each good, the smallest of origin surplus, destination
        /// need, route capacity, destination free storage and what the destination can pay for.
        /// The most urgent good wins: food while the destination is short of food, otherwise the good
        /// the destination holds the fewest days of (relative to its target). Ties go to the earlier
        /// resource in display order.
        /// </summary>
        public (ResourceTypeId resource, int quantity) ChooseCargo(SettlementData origin, SettlementData destination, TradeRouteData route,
            IReadOnlyDictionary<(SettlementId, ResourceTypeId), long> incoming)
        {
            var best = (resource: default(ResourceTypeId), quantity: 0);
            long bestUrgency = long.MinValue;
            var buyerCoin = destination.ResourceInventory.Get(ResourceTypes.Coin);

            foreach (var resource in ResourceTypes.Goods)
            {
                if (resource == ResourceTypes.Food ? origin.IsInFoodShortage : origin.Economy.FindStatus(resource)?.InShortage == true) continue;

                var originDemand = _rules.DailyDemand(resource, origin.Population, origin.Economy.TotalWorkers);
                var surplus = (long)origin.ResourceInventory.Get(resource) - (long)originDemand * _rules.ReserveDays(resource);
                if (surplus <= 0) continue;

                incoming.TryGetValue((destination.SettlementId, resource), out var arriving);
                var destinationDemand = _rules.DailyDemand(resource, destination.Population, destination.Economy.TotalWorkers);
                var stock = destination.ResourceInventory.Get(resource);
                var need = (long)destinationDemand * _rules.TargetDays(resource) - stock - arriving;
                if (need <= 0) continue;

                var free = (long)destination.Economy.CapacityOf(resource) - stock - arriving;
                var affordable = buyerCoin / Math.Max(1, _rules.Price(resource));
                var quantity = Math.Min(Math.Min(surplus, need), Math.Min(route.CapacityPerShipment, Math.Min(free, affordable)));
                if (quantity <= 0) continue;

                // Urgency: missing share of the destination's target stock, in per-mille (food in crisis first).
                var target = Math.Max(1L, (long)destinationDemand * _rules.TargetDays(resource));
                var urgency = need * 1000 / target;
                if (resource == ResourceTypes.Food && destination.IsInFoodShortage) urgency += 1000;
                if (urgency > bestUrgency)
                {
                    bestUrgency = urgency;
                    best = (resource, (int)quantity);
                }
            }

            return best;
        }

        // ----- Helpers ----------------------------------------------------------------------

        private static SettlementData Lookup(Dictionary<SettlementId, SettlementData> settlements, SettlementId id) =>
            settlements.TryGetValue(id, out var s) ? s : throw new InvalidOperationException($"Trade references unknown settlement {id}.");

        private static Dictionary<ResourceTypeId, long> Totals(WorldData world)
        {
            var totals = new Dictionary<ResourceTypeId, long>();
            foreach (var s in world.Settlements)
            {
                foreach (var stack in s.ResourceInventory.Stacks)
                {
                    totals.TryGetValue(stack.ResourceTypeId, out var t);
                    totals[stack.ResourceTypeId] = t + stack.Quantity;
                }
            }

            foreach (var shipment in world.Shipments)
            {
                totals.TryGetValue(shipment.ResourceTypeId, out var t);
                totals[shipment.ResourceTypeId] = t + shipment.Quantity;
            }

            return totals;
        }
    }
}
