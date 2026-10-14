using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace PG.Sim
{
    // Bölüm 6.1: shared meta object infrastructure. Built in Bölüm 4 for subspecies; Bölüm 6 extends it.
    public enum MetaKind : byte { Kingdom, City, Alliance, Army, Clan, Family, Culture, Language, Religion, Subspecies, Plot, War }

    [Flags]
    public enum MetaFlags : byte { None = 0, Favorite = 1, Edited = 2, Dead = 4, PlayerControlled = 8 }

    public struct MetaHeader
    {
        public long Uid;
        public MetaKind Kind;
        public int NameId;          // -1 until the name generator exists (Bölüm 6.2)
        public uint Color;          // RGBA
        public Banner Banner;       // Bölüm 6.3
        public long FoundedTick, DiedTick;
        public long FounderUid;
        public int MemberCount;     // monthly cache
        public TraitSet Traits;     // trait file depends on the kind
        public MetaFlags Flags;
    }

    // Dead entries are never removed (history and statistics keep pointing at them).
    public sealed class MetaStore<T> : IDisposable where T : unmanaged
    {
        public NativeList<MetaHeader> Headers;
        public NativeList<T> Items;
        public long NextUid = 1;
        readonly MetaKind _kind;

        public MetaStore(MetaKind kind, int capacity = 64)
        {
            _kind = kind;
            Headers = new NativeList<MetaHeader>(capacity, Allocator.Persistent);
            Items = new NativeList<T>(capacity, Allocator.Persistent);
        }

        public int Count => Headers.Length;
        public MetaKind Kind => _kind;

        public int Create(in T data, long founderUid, long tick)
        {
            Headers.Add(new MetaHeader { Uid = NextUid++, Kind = _kind, NameId = -1, FoundedTick = tick, FounderUid = founderUid });
            Items.Add(data);
            return Headers.Length - 1;
        }

        public ref MetaHeader Header(int index) => ref Headers.ElementAt(index);
        public ref T Data(int index) => ref Items.ElementAt(index);
        public bool IsAlive(int index) => index >= 0 && index < Headers.Length && (Headers[index].Flags & MetaFlags.Dead) == 0;

        public void MarkDead(int index, long tick)
        {
            ref var h = ref Headers.ElementAt(index);
            h.Flags |= MetaFlags.Dead;
            h.DiedTick = tick;
            h.MemberCount = 0;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(NextUid);
            w.Write(Headers.Length);
            w.Write(MemoryMarshal.AsBytes(Headers.AsArray().AsReadOnlySpan()));
            w.Write(MemoryMarshal.AsBytes(Items.AsArray().AsReadOnlySpan()));
        }

        public void Read(BinaryReader r)
        {
            NextUid = r.ReadInt64();
            int n = r.ReadInt32();
            Headers.Resize(n, NativeArrayOptions.ClearMemory);
            Items.Resize(n, NativeArrayOptions.ClearMemory);
            ReadExact(r, MemoryMarshal.AsBytes(Headers.AsArray().AsSpan()));
            ReadExact(r, MemoryMarshal.AsBytes(Items.AsArray().AsSpan()));
        }

        static void ReadExact(BinaryReader r, Span<byte> bytes)
        {
            if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated meta store");
        }

        public void Dispose()
        {
            if (Headers.IsCreated) Headers.Dispose();
            if (Items.IsCreated) Items.Dispose();
        }
    }
}
