using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PG.Render;
using UnityEngine;
using UnityEngine.TestTools;

namespace PG.Tests.EditMode
{
    public sealed class AudioLoadingTests
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _root;
        AudioManager _manager;
        AudioClip _fallback, _unrelated;

        T Field<T>(string name) => (T)typeof(AudioManager).GetField(name, Private).GetValue(_manager);

        AudioSource Source(AudioClip clip)
        {
            var child = new GameObject("Test source");
            child.transform.SetParent(_root.transform);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.clip = clip;
            return source;
        }

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("Audio loading test");
            _manager = _root.AddComponent<AudioManager>();
            _manager.enabled = false;
            _fallback = AudioClip.Create("fallback", 22050, 1, 22050, false);
            _unrelated = AudioClip.Create("unrelated", 22050, 1, 22050, false);
            typeof(AudioManager).GetField("_clips", Private).SetValue(_manager, new[] { _fallback });
            Field<HashSet<AudioClip>>("_pendingClips").Add(_fallback);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_fallback);
            Object.DestroyImmediate(_unrelated);
        }

        [Test]
        public void DownloadRebindsMatchingLoopsButDoesNotRestartStoppedSourcesOrCutOffEffects()
        {
            var ambient = Field<AudioSource[]>("_ambient");
            var music = Field<AudioSource[]>("_music");
            ambient[0] = Source(_fallback);
            ambient[1] = Source(_unrelated);
            music[0] = Source(_fallback);
            var oneShot = Source(_fallback);
            oneShot.loop = false;
            typeof(AudioManager).GetField("_pool", Private).SetValue(_manager, new[] { oneShot });
            var loaded = AudioClip.Create("loaded", 44100, 1, 22050, false);
            typeof(AudioManager).GetMethod("InstallLoadedClip", Private).Invoke(_manager, new object[] { 0, loaded });

            Assert.That(Field<AudioClip[]>("_clips")[0], Is.SameAs(loaded));
            Assert.That(ambient[0].clip, Is.SameAs(loaded));
            Assert.That(music[0].clip, Is.SameAs(loaded));
            Assert.That(ambient[1].clip, Is.SameAs(_unrelated));
            Assert.That(oneShot.clip, Is.SameAs(_fallback), "An in-flight effect must finish its original clip.");
            Assert.That(ambient[0].isPlaying, Is.False, "A stopped source must stay stopped.");
            Assert.That(Field<HashSet<AudioClip>>("_pendingClips"), Is.Empty);
        }

        [Test]
        public void NullDecodedClipKeepsFallbackAndReleasesLoadingMute()
        {
            typeof(AudioManager).GetMethod("InstallLoadedClip", Private).Invoke(_manager, new object[] { 0, null });
            Assert.That(Field<AudioClip[]>("_clips")[0], Is.SameAs(_fallback));
            Assert.That(Field<HashSet<AudioClip>>("_pendingClips"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AuthoredWavDecodesThroughRuntimeLoaderAndReplacesAmbientFallback()
        {
            Field<AudioSource[]>("_ambient")[0] = Source(_fallback);
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Audio/ambient/amb_forest.wav");
            Assert.That(File.Exists(path), Is.True);
            var load = (IEnumerator)typeof(AudioManager).GetMethod("LoadFile", Private).Invoke(_manager, new object[] { 0, path });
            yield return load;
            var loaded = Field<AudioClip[]>("_clips")[0];
            Assert.That(loaded, Is.Not.SameAs(_fallback));
            Assert.That(loaded.frequency, Is.EqualTo(22050));
            Assert.That(loaded.length, Is.EqualTo(16f).Within(.001f));
            Assert.That(Field<AudioSource[]>("_ambient")[0].clip, Is.SameAs(loaded));
        }
    }
}
