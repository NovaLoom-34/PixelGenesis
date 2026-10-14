using System.Collections.Generic;

namespace PG.Content
{
    public sealed class ChronicleTemplateDef : ContentDef
    {
        public string[] Tr { get; set; }
        public string[] En { get; set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (Tr == null || Tr.Length == 0 || En == null || En.Length == 0)
                errors.Add($"{Id}: chronicle templates need Turkish and English variants");
            Validate(Tr, "tr", errors);
            Validate(En, "en", errors);
        }

        void Validate(string[] variants, string language, List<string> errors)
        {
            if (variants == null) return;
            for (int i = 0; i < variants.Length; i++)
                if (string.IsNullOrWhiteSpace(variants[i])) errors.Add($"{Id}: empty {language} variant at {i}");
        }
    }
}
