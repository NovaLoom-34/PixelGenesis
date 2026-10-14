using System.Collections.Generic;

namespace PG.Content
{
    // sounds.json (Bölüm 9.1). Trigger = key looked up by the audio layer; Synth = procedural placeholder while Files are missing.
    public sealed class SoundDef : ContentDef
    {
        public List<string> Files { get; set; } = new List<string>();
        public float Volume { get; set; } = 1f;
        public float[] Pitch { get; set; } = { 1f, 1f };
        public int MaxInstances { get; set; } = 4;
        public int CooldownMs { get; set; }
        public string Category { get; set; } = "sfx";
        public bool WorldSpace { get; set; }
        public int MinLod { get; set; }
        public int MaxLod { get; set; } = 3;
        public bool Loop { get; set; }
        public string Trigger { get; set; }
        public string Synth { get; set; }
        public List<string> Clouds { get; set; } = new List<string>();
    }
}
