using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    public readonly struct VehicleCreatedEvent : ISimEvent
    {
        public readonly VehicleType Type;
        public readonly int City;
        public readonly float2 Pos;

        public VehicleCreatedEvent(VehicleType type, int city, float2 pos)
        {
            Type = type;
            City = city;
            Pos = pos;
        }
    }

    // Bölüm 5.10 & 5.12: Vehicles on water (Ships/Boats) and land (Carts/Cars).
    // Updates movement along path networks, carries goods and passengers between cities.
    public sealed class VehicleSystem : ISimSystem
    {
        public const float CartSpeed = 0.20f;
        public const float CarSpeed = 0.40f;
        public const float BoatSpeed = 0.16f;
        public const float WarshipSpeed = 0.18f;
        public const int MaxVehiclesTotal = 64;

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 45; // right after BoatSystem

        public void Tick(in SimContext ctx)
        {
            var civ = ctx.Civ;
            var w = ctx.Units;
            var u = w.Store;
            var map = ctx.World;
            var meta = ctx.Meta;
            if (civ == null || !civ.Vehicles.IsCreated) return;

            // 1. Move active vehicles
            for (int v = civ.Vehicles.Length - 1; v >= 0; v--)
            {
                ref var veh = ref civ.Vehicles.ElementAt(v);
                if (veh.State == VehicleState.Done)
                {
                    if (veh.PathHandle >= 0) w.Paths.Release(veh.PathHandle);
                    civ.Vehicles.RemoveAtSwapBack(v);
                    continue;
                }
                UpdateVehicle(ctx, civ, w, ref veh);
            }

            // 2. Monthly vehicle creation for cities with the appropriate inventions
            if (ctx.Clock.IsMonthStart && civ.Vehicles.Length < MaxVehiclesTotal)
            {
                ref var rng = ref ctx.Rng.Get(RngStream.Civ);
                SpawnMonthlyVehicles(ctx, civ, meta, ref rng);
            }
        }

        static void UpdateVehicle(in SimContext ctx, CivWorld civ, UnitWorld w, ref VehicleData veh)
        {
            var u = w.Store;
            bool land = veh.Type == VehicleType.Cart || veh.Type == VehicleType.Car;
            Mobility mob = land ? Mobility.Land : Mobility.Water;

            if (veh.PathHandle < 0)
            {
                var status = w.Paths.Request((int2)math.floor(veh.Pos), veh.Destination, mob, out int h);
                if (status == PathStatus.Ready) { veh.PathHandle = h; veh.PathStep = 0; }
                else if (status != PathStatus.OverBudget)
                {
                    Arrive(civ, w, ref veh);
                    return;
                }
                return;
            }

            int len = w.Paths.Length(veh.PathHandle);
            if (veh.PathStep >= len)
            {
                Arrive(civ, w, ref veh);
                return;
            }

            float2 next = (float2)w.Paths.Point(veh.PathHandle, veh.PathStep) + 0.5f;
            float2 d = next - veh.Pos;
            float dist = math.length(d);
            float stepSpeed = veh.Speed;

            if (dist <= stepSpeed)
            {
                veh.Pos = next;
                veh.PathStep++;
            }
            else
            {
                float2 dir = d / dist;
                veh.Pos += dir * stepSpeed;
                veh.Dir = dir;
            }

            // Carry passengers along
            for (int p = 0; p < veh.Passengers.Length; p++)
            {
                var id = veh.Passengers[p];
                if (u.IsAlive(id)) u.Pos[id.Index] = veh.Pos;
            }
        }

        static void Arrive(CivWorld civ, UnitWorld w, ref VehicleData veh)
        {
            var u = w.Store;

            // Deliver cargo if carrying resources
            if (veh.CargoAmount > 0 && veh.City >= 0 && civ.CityAlive(veh.City))
            {
                civ.AddRes(veh.City, veh.CargoRes, veh.CargoAmount);
                veh.CargoAmount = 0;
            }

            // Unload passengers
            foreach (var id in veh.Passengers)
            {
                if (!u.IsAlive(id)) continue;
                int i = id.Index;
                u.Flags[i] &= ~(uint)UnitFlags.Aboard;
                u.Pos[i] = veh.Pos;
                u.NextThink[i] = 0;
            }
            veh.Passengers.Clear();

            if (veh.PathHandle >= 0) w.Paths.Release(veh.PathHandle);
            veh.PathHandle = -1;
            veh.State = VehicleState.Done;
        }

        static void SpawnMonthlyVehicles(in SimContext ctx, CivWorld civ, MetaWorld meta, ref SimRandom rng)
        {
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                int k = civ.KingdomOf(c);
                if (k < 0 || !meta.KingdomAlive(k)) continue;

                // 1. Steam Car (Automobile)
                if (civ.HasInvention(k, "inv.automobile") && rng.Chance(0.08f))
                {
                    int other = FindDestinationCity(civ, c, k);
                    if (other >= 0)
                    {
                        SpawnCar(ctx, civ, c, k, (float2)civ.City(c).Center + 0.5f, civ.City(other).Center);
                        continue;
                    }
                }

                // 2. Trade Cart (Wagon)
                if (civ.HasInvention(k, "inv.cart") && rng.Chance(0.12f))
                {
                    int other = FindDestinationCity(civ, c, k);
                    if (other >= 0)
                    {
                        SpawnCart(ctx, civ, c, k, (float2)civ.City(c).Center + 0.5f, civ.City(other).Center, ref rng);
                    }
                }
            }
        }

        public static void SpawnCart(in SimContext ctx, CivWorld civ, int city, int kingdom, float2 pos, int2 dest, ref SimRandom rng)
        {
            var cart = new VehicleData
            {
                Type = VehicleType.Cart,
                Pos = pos,
                Dir = new float2(1f, 0f),
                City = city,
                Kingdom = kingdom,
                Speed = CartSpeed,
                Hp = 30f,
                Destination = dest,
                Origin = (int2)math.floor(pos),
                PathHandle = -1,
                State = VehicleState.Traveling,
                CargoRes = (ushort)rng.Range(0, math.max(1, civ.ResCount)),
                CargoAmount = 25,
            };
            civ.Vehicles.Add(cart);
            ctx.Events.Publish(new VehicleCreatedEvent(VehicleType.Cart, city, pos));
        }

        public static void SpawnCar(in SimContext ctx, CivWorld civ, int city, int kingdom, float2 pos, int2 dest)
        {
            var car = new VehicleData
            {
                Type = VehicleType.Car,
                Pos = pos,
                Dir = new float2(1f, 0f),
                City = city,
                Kingdom = kingdom,
                Speed = CarSpeed,
                Hp = 50f,
                Destination = dest,
                Origin = (int2)math.floor(pos),
                PathHandle = -1,
                State = VehicleState.Traveling,
                CargoAmount = 0,
            };
            civ.Vehicles.Add(car);
            ctx.Events.Publish(new VehicleCreatedEvent(VehicleType.Car, city, pos));
        }

        static int FindDestinationCity(CivWorld civ, int fromCity, int kingdom)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            float2 fromPos = (float2)civ.City(fromCity).Center;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (c == fromCity || !civ.CityAlive(c)) continue;
                if (civ.KingdomOf(c) != kingdom) continue;
                float d = math.distancesq(fromPos, (float2)civ.City(c).Center);
                if (d < bestDist && d > 64f) // at least 8 tiles away
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }
    }
}
