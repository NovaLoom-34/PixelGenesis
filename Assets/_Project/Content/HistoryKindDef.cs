using System.Collections.Generic;

namespace PG.Content
{
    public sealed class HistoryKindDef : ContentDef
    {
        public int Importance { get; set; } = 10;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (Importance < 0 || Importance > 100)
                errors.Add($"{Id}: history importance must be in 0..100");
        }
    }
}
