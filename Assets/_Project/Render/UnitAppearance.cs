namespace PG.Render
{
    // Presentation-only held prop resolved from the unit's existing job and equipment state.
    public enum UnitProp : byte
    {
        None, Sword, Axe, Spear, Bow, Crossbow, Hammer, Dagger, Staff, Club, Trident, Sling, Pickaxe, Scythe, Musket, UnarmedStrike, Count
    }

    public static class UnitAppearance
    {
        public static UnitProp Resolve(string jobId, string equipmentId, bool doingWork, bool unarmedAttacking = false)
        {
            if (doingWork)
            {
                switch (jobId)
                {
                    case "job.farmer": return UnitProp.Scythe;
                    case "job.lumberjack": return UnitProp.Axe;
                    case "job.miner": return UnitProp.Pickaxe;
                    case "job.builder": return UnitProp.Hammer;
                }
            }

            UnitProp equipment = EquipmentProp(equipmentId);
            if (equipment != UnitProp.None) return equipment;
            return unarmedAttacking ? UnitProp.UnarmedStrike : UnitProp.None;
        }

        static UnitProp EquipmentProp(string equipmentId)
        {
            switch (equipmentId)
            {
                case "eq.sword": return UnitProp.Sword;
                case "eq.axe": return UnitProp.Axe;
                case "eq.spear": return UnitProp.Spear;
                case "eq.bow": return UnitProp.Bow;
                case "eq.crossbow": return UnitProp.Crossbow;
                case "eq.hammer": return UnitProp.Hammer;
                case "eq.dagger": return UnitProp.Dagger;
                case "eq.staff": return UnitProp.Staff;
                case "eq.club": return UnitProp.Club;
                case "eq.trident": return UnitProp.Trident;
                case "eq.sling": return UnitProp.Sling;
                case "eq.pickaxe": return UnitProp.Pickaxe;
                case "eq.scythe": return UnitProp.Scythe;
                case "eq.musket": return UnitProp.Musket;
                default: return UnitProp.None;
            }
        }

        // Authored attack rows contain a race-specific weapon. Use them only when that exact
        // weapon is equipped; other attacks use an unarmed body pose with the correct prop layer.
        public static bool CanUseRaceAttackPose(string speciesId, string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId)) return false;
            switch (speciesId)
            {
                case "sp.human":
                case "sp.elf": return equipmentId == "eq.sword";
                case "sp.orc": return equipmentId == "eq.axe";
                case "sp.dwarf": return equipmentId == "eq.hammer";
                default: return false;
            }
        }
    }
}
