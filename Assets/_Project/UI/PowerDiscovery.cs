using PG.Content;

namespace PG.UI
{
    public static class PowerDiscovery
    {
        // Records achievement-gated powers in the existing profile discovery list.
        // A null achievement id reconciles powers for every achievement on older profiles.
        public static int RevealEarned(Profile profile, Registry<PowerDef> powers, string achievementId, out string firstPowerId)
        {
            firstPowerId = null;
            if (profile == null || powers == null) return 0;
            int count = 0;
            for (int i = 0; i < powers.Count; i++)
            {
                var power = powers[i];
                if (string.IsNullOrEmpty(power.UnlockedBy) ||
                    (achievementId != null && power.UnlockedBy != achievementId) ||
                    !profile.HasAchievement(power.UnlockedBy) || !profile.Discover(power.Id)) continue;
                if (firstPowerId == null) firstPowerId = power.Id;
                count++;
            }
            return count;
        }
    }
}
