using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PG.Content;
using PG.Sim;
using UnityEngine;

namespace PG.Persistence
{
    // Keeps a short rolling history of pre-action saves; old snapshots are removed after five minutes.
    public sealed class UndoSnapshotHistory
    {
        public const int Capacity = 5;
        public static readonly TimeSpan Retention = TimeSpan.FromMinutes(5);

        sealed class Entry
        {
            public string Path;
            public DateTime CapturedUtc;
            public Task Write;
        }

        readonly string _folder;
        readonly List<Entry> _entries = new List<Entry>(Capacity);
        int _nextId;

        public UndoSnapshotHistory(string folder = null)
        {
            _folder = folder ?? Path.Combine(Application.persistentDataPath, "undo");
            Directory.CreateDirectory(_folder);
            foreach (var stale in Directory.GetFiles(_folder, "undo_*.pxgn")) SaveSystem.DeleteFile(stale);
            foreach (var stale in Directory.GetFiles(_folder, "undo_*.tmp")) SaveSystem.DeleteFile(stale);
        }

        public bool CanUndo
        {
            get
            {
                PruneExpired(DateTime.UtcNow);
                return _entries.Count > 0;
            }
        }

        public int Count => _entries.Count;

        public void Clear()
        {
            while (_entries.Count > 0) RemoveAt(_entries.Count - 1);
        }

        public void Capture(SimWorld sim)
        {
            if (sim == null) return;
            var now = DateTime.UtcNow;
            PruneExpired(now);
            while (_entries.Count >= Capacity) RemoveAt(0);

            string path = Path.Combine(_folder, $"undo_{now.Ticks}_{_nextId++}.pxgn");
            var entry = new Entry { Path = path, CapturedUtc = now };
            entry.Write = SaveSystem.SaveFileAsync(path, sim);
            _entries.Add(entry);
        }

        public async Task<GameState> TakeLastAsync(ContentDB content)
        {
            PruneExpired(DateTime.UtcNow);
            if (_entries.Count == 0) return null;

            int index = _entries.Count - 1;
            var entry = _entries[index];
            await entry.Write;
            var state = await SaveSystem.LoadFileAsync(entry.Path, content);
            _entries.RemoveAt(index);
            SaveSystem.DeleteFile(entry.Path);
            SaveSystem.DeleteFile(entry.Path + ".tmp");
            return state;
        }

        void PruneExpired(DateTime now)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (now - _entries[i].CapturedUtc > Retention) RemoveAt(i);
        }

        void RemoveAt(int index)
        {
            var entry = _entries[index];
            _entries.RemoveAt(index);
            if (entry.Write == null || entry.Write.IsCompleted)
            {
                SaveSystem.DeleteFile(entry.Path);
                SaveSystem.DeleteFile(entry.Path + ".tmp");
            }
            else
            {
                entry.Write.ContinueWith(_ =>
                {
                    SaveSystem.DeleteFile(entry.Path);
                    SaveSystem.DeleteFile(entry.Path + ".tmp");
                }, TaskScheduler.Default);
            }
        }
    }
}
