using System;
using System.Threading.Tasks;
using PG.Content;
using PG.Powers;
using PG.Render;
using PG.Sim;
using PG.WorldGen;

namespace PG.UI
{
    // Implemented by the boot layer. UI reads the simulation and sends commands; it never writes sim arrays.
    public interface IGameHost
    {
        ContentDB Content { get; }
        SimWorld Sim { get; }
        WorldGenSettings CurrentWorldSettings { get; }
        PowerCommandQueue Commands { get; }
        EditCommandQueue Edits { get; }
        MapRenderer Renderer { get; }
        OverlayRenderer Overlay { get; }
        AudioManager Audio { get; }
        int TicksLastSecond { get; }
        bool CanUndo { get; }

        event Action WorldChanged;

        void StartNewWorld(WorldGenSettings settings);
        Task SaveToSlot(int slot);
        Task LoadSlot(int slot);
        void CaptureUndoSnapshot();
        Task UndoLastAction();
    }
}
