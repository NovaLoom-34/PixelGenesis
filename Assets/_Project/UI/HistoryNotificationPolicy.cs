using System;

namespace PG.UI
{
    public static class HistoryNotificationPolicy
    {
        public const int DefaultThreshold = 60;

        public static int NormalizeThreshold(int threshold) => Math.Max(20, Math.Min(100, threshold));

        public static bool ShouldNotify(ushort importance, int threshold)
            => importance >= NormalizeThreshold(threshold);

        public static bool ShouldNotify(ushort importance, int threshold, bool watchedKingdomEvent)
            => watchedKingdomEvent || ShouldNotify(importance, threshold);
    }
}
