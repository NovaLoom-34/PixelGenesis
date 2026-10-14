using PG.Content;
using PG.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    public enum VehicleType : byte
    {
        Cart,           // Land cargo / caravan cart (inv.cart)
        Car,            // Land steam automobile (inv.automobile, fast!)
        FishingBoat,    // Water coastal fishing vessel (inv.sails)
        TradeBoat,      // Water coastal trade ship (inv.sails)
        TransportShip,  // Water colonization transport (inv.shipbuilding)
        Warship         // Water naval combat vessel (inv.shipbuilding)
    }

    public enum VehicleState : byte { Idle, Traveling, Done }

    public struct VehicleData
    {
        public VehicleType Type;
        public float2 Pos;
        public float2 Dir;
        public int City;
        public int Kingdom;
        public float Speed;
        public float Hp;
        public ushort CargoRes;
        public int CargoAmount;
        public int2 Destination;
        public int2 Origin;
        public int PathHandle, PathStep;
        public VehicleState State;
        public FixedList64Bytes<EntityId> Passengers;
    }
}
