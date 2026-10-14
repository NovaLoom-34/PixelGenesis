using PG.Core;
using PG.Content;

namespace PG.Sim
{
    // Uses the already-persisted Mate/Target/Timer fields, so old saves retain their families.
    public static class UnitCourtship
    {
        public const int DurationTicks = 60;

        public static bool CanPair(UnitWorld world, int a, int b)
        {
            var u = world.Store;
            if (a == b || (uint)a >= (uint)u.HighWater || (uint)b >= (uint)u.HighWater ||
                u.State[a] != UnitStore.StateAlive || u.State[b] != UnitStore.StateAlive) return false;
            if (!world.Laws.IsOn(world.LawReproduction) || u.Species[a] != u.Species[b] ||
                (AgeStage)u.Age[a] != AgeStage.Adult || (AgeStage)u.Age[b] != AgeStage.Adult ||
                u.Sex[a] > 1 || u.Sex[b] > 1 || u.Sex[a] == u.Sex[b]) return false;
            int female = u.Sex[a] == 0 ? a : b;
            var reproduction = u.SpeciesOf(a).Reproduction;
            if (reproduction != Reproduction.Live && reproduction != Reproduction.Egg) return false;
            if (world.StPregnant >= 0 && u.HasStatus(female, world.StPregnant)) return false;
            if (reproduction == Reproduction.Egg && world.StBreedingCooldown >= 0 && u.HasStatus(female, world.StBreedingCooldown)) return false;
            if (!u.SpeciesOf(a).IsCiv) return true;
            // One adult may initiate courtship; Mate() assigns the reciprocal task to the partner.
            // Requiring both independent monthly urge rolls at the same time makes courtship collapse.
            if (((u.Wants[a] & 1) == 0 && (u.Wants[b] & 1) == 0) ||
                u.Saturation[a] <= 60 || u.Saturation[b] <= 60 || u.Happiness[a] <= -20 || u.Happiness[b] <= -20 ||
                Unavailable(u, a) || Unavailable(u, b) || world.Hostile(a, b) || world.Hostile(b, a) || CloseFamily(u, a, b)) return false;
            return (!HasLivingBond(u, a) || u.Mate[a] == u.IdOf(b)) &&
                   (!HasLivingBond(u, b) || u.Mate[b] == u.IdOf(a));
        }

        // Older saves stored a one-way pregnancy link; only reciprocal pairs reserve a spouse.
        public static bool HasLivingBond(UnitStore u, int i) => u.IsAlive(u.Mate[i]) && u.Mate[u.Mate[i].Index] == u.IdOf(i);

        static bool Unavailable(UnitStore u, int i)
        {
            if (u.Has(i, UnitFlags.PlayerControlled | UnitFlags.Stunned | UnitFlags.Immobile | UnitFlags.Asleep | UnitFlags.Aboard)) return true;
            var task = (UnitTask)u.Task[i];
            return task == UnitTask.Flee || task == UnitTask.Attack || task == UnitTask.Hunt || task == UnitTask.Cast || task == UnitTask.Douse;
        }

        public static bool CloseFamily(UnitStore u, int a, int b)
        {
            EntityId aid = u.IdOf(a), bid = u.IdOf(b);
            return u.Mother[a] == bid || u.Father[a] == bid || u.Mother[b] == aid || u.Father[b] == aid ||
                (!u.Mother[a].IsNone && (u.Mother[a] == u.Mother[b] || u.Mother[a] == u.Father[b])) ||
                (!u.Father[a].IsNone && (u.Father[a] == u.Mother[b] || u.Father[a] == u.Father[b]));
        }
    }
}
