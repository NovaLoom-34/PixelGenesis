using System;
using System.Collections.Generic;
using PG.Core;
using PG.Sim;
using PG.World;

namespace PG.Persistence
{
    public static class SaveFormat
    {
        public const string Magic = "PXGN";
        public const int FormatVersion = 1;

        public const int SectionContentMap = 1;
        public const int SectionClock = 2;
        public const int SectionRng = 3;
        public const int SectionWorldTiles = 4;
        public const int SectionWorldZones = 5;
        // Optional sections (Bölüm 2): missing in older files -> defaults, so FormatVersion stays 1.
        public const int SectionFeatureState = 6;
        public const int SectionZoneClimate = 7;
        public const int SectionNature = 8;
        public const int SectionLaws = 9;
        public const int SectionUnits = 10; // Bölüm 3
        public const int SectionCiv = 11;   // Bölüm 5
        public const int SectionMeta = 12;  // Bölüm 6
        public const int SectionPowers = 13; // Bölüm 7
        public const int SectionMods = 14;   // Bölüm 9.3: loaded mod ids + versions (optional)
        public const int SectionGovernor = 15; // Governor decision log (optional; written via hook, DECISIONS #97)
        public const int SectionPolicy = 16;   // Policies + proposals + treaties + war plans + peace terms + army logistics (optional, format 7)
        public const int SectionInventions = 17; // Kingdom inventions, research & vehicles (Bölüm 5.12, optional)
        public const int SectionScenario = 18; // Optional challenge id and start year; absent in sandbox/legacy saves.
        public const int SectionWorldRecipe = 19; // Optional portable world-generation settings.
        // Later chapters: Units, Buildings, Meta, History, Stats
    }

    public sealed class SaveSlotInfo
    {
        public int Slot;
        public bool Exists;
        public bool IsAuto;
        public int FormatVersion;
        public string GameVersion;
        public DateTime CreatedUtc;
        public string WorldName;
        public int Year;
        public int Population;
        public int SizeX, SizeY;
        public byte[] ThumbnailPng;
    }

    // Result of a load; the game session turns it into a SimWorld.
    public sealed class GameState
    {
        public string WorldName;
        public GameClock Clock;
        public SimRandomProvider Rng;
        public WorldMap World;
        public string ScenarioId;
        public int ScenarioStartYear;
        public ScenarioStatus ScenarioOutcome = ScenarioStatus.Sandbox;
        public readonly List<string> Warnings = new List<string>();
        internal byte[] NatureRaw, LawsRaw, UnitsRaw, CivRaw, MetaRaw, PowersRaw, GovernorRaw, PolicyRaw, InventionsRaw, WorldRecipeRaw;

        // Call once the SimWorld exists: restores nature state and world laws.
        public void ApplyTo(SimWorld sim)
        {
            NatureSave.ApplyLaws(LawsRaw, sim);
            NatureSave.ApplyNature(NatureRaw, sim, Warnings);
            UnitSave.Apply(UnitsRaw, sim, Warnings);
            CivSave.Apply(CivRaw, sim, Warnings);
            MetaSave.Apply(MetaRaw, sim, Warnings);
            PowerSave.Apply(PowersRaw, sim, Warnings);
            PolicySave.Apply(PolicyRaw, sim, Warnings);
            InventionSave.Apply(InventionsRaw, sim, Warnings);
            SaveSystem.WorldRecipeSectionImporter?.Invoke(WorldRecipeRaw);
            sim.ScenarioId = ScenarioId ?? "";
            sim.ScenarioStartYear = ScenarioStartYear;
            sim.ScenarioOutcome = string.IsNullOrEmpty(sim.ScenarioId) ? ScenarioStatus.Sandbox : ScenarioOutcome;
            if (GovernorRaw != null) SaveSystem.GovernorSectionImporter?.Invoke(GovernorRaw);
        }
    }

    // Parsed file for migrations: header fields + raw (decompressed) sections.
    public sealed class SaveDocument
    {
        public int FormatVersion;
        public bool RecoveredFromBackup;
        public string GameVersion;
        public long CreatedUtc;
        public string WorldName;
        public int Year, Population, SizeX, SizeY;
        public byte[] ThumbnailPng;
        public readonly Dictionary<int, byte[]> Sections = new Dictionary<int, byte[]>();
    }

    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Migrate(SaveDocument doc);
    }
}
