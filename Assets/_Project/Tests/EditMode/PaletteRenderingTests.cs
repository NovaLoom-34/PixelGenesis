using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Tests.EditMode
{
    [Category("Graphics")]
    [Explicit("Requires a graphics device; run with the EditMode Graphics test mode.")]
    public sealed class PaletteRenderingTests
    {
        [TestCase(28, 0)]
        [TestCase(112, 3)]
        [TestCase(224, 7)]
        public void GrayIndexSelectsItsPaletteColorInLinearProject(byte gray, int expected)
        {
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear));
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            source.SetPixel(0, 0, new Color32(gray, gray, gray, 255)); source.Apply();
            var palette = new Texture2D(8, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int i = 0; i < 8; i++) palette.SetPixel(i, 0, Color.black);
            palette.SetPixel(0, 0, Color.red);
            palette.SetPixel(3, 0, Color.green);
            palette.SetPixel(7, 0, Color.blue);
            palette.Apply();
            var material = new Material(Resources.Load<Shader>("PG_Unit"));
            material.SetTexture("_MainTex", source);
            material.SetTexture("_Palette", palette);
            var go = new GameObject("PaletteProbe", typeof(MeshFilter), typeof(MeshRenderer));
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-0.8f, -0.8f), new Vector3(0.8f, -0.8f), new Vector3(0.8f, 0.8f), new Vector3(-0.8f, 0.8f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.uv2 = new[] { new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f) };
            mesh.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var cameraGo = new GameObject("PaletteProbeCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(32, 32, 24);
            var readback = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var oldActive = RenderTexture.active;
            Color oldTint = Shader.GetGlobalColor("_EraTint");
            Color oldDayLight = Shader.GetGlobalColor("_DayLight");
            try
            {
                Shader.SetGlobalColor("_EraTint", Color.white);
                Shader.SetGlobalColor("_DayLight", Color.white);
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, 32f, 32f), 0, 0); readback.Apply();
                var pixel = readback.GetPixel(16, 16);
                float selected = expected == 0 ? pixel.r : expected == 3 ? pixel.g : pixel.b;
                Assert.That(selected, Is.GreaterThan(0.7f), $"gray={gray}, rendered={pixel}");
            }
            finally
            {
                Shader.SetGlobalColor("_EraTint", oldTint);
                Shader.SetGlobalColor("_DayLight", oldDayLight);
                RenderTexture.active = oldActive;
                Object.DestroyImmediate(readback);
                target.Release(); Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(palette);
                Object.DestroyImmediate(source);
            }
        }
    }
}
