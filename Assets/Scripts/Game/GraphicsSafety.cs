using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Keeps the game drawing on graphics chips that cannot handle every effect.
    ///
    /// Some chips (for example the Qualcomm Adreno in Snapdragon laptops) refuse to make the
    /// screen-sized images the effects draw into, and the game then shows nothing at all. This
    /// steps the graphics down one level at a time until the errors stop, and remembers the level
    /// that worked, so the next start goes straight to it:
    ///   0 Full: everything on.
    ///   1 No edge smoothing (MSAA): the most common thing to fail.
    ///   2 Also no HDR (the extra-bright colours that bloom uses).
    ///   3 Simple: also no camera effects, ambient shadows or water refraction.
    /// The Settings window can also switch Simple graphics on or back to Full.
    /// </summary>
    public static class GraphicsSafety
    {
        public const int Full = 0, NoMsaa = 1, NoHdr = 2, Simple = 3;
        private const string Key = "FrogTerrarium.Settings.GraphicsLevel";

        private static int _level = -1;
        private static int _lastStepFrame = -1000;
        private static bool _listening;

        /// <summary>How far the graphics have been stepped down (0 = everything on).</summary>
        public static int Level
        {
            get { if (_level < 0) _level = Mathf.Clamp(PlayerPrefs.GetInt(Key, Full), Full, Simple); return _level; }
            set
            {
                _level = Mathf.Clamp(value, Full, Simple);
                PlayerPrefs.SetInt(Key, _level);
                PlayerPrefs.Save();
                Apply(Camera.main);
            }
        }

        public static string LevelName(int level) =>
            level == Full ? "Full" : level == NoMsaa ? "Full, without edge smoothing" : level == NoHdr ? "Reduced" : "Simple";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Startup()
        {
            // Automated tests run with no graphics chip at all: nothing to check there.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            // Ask the chip up front what it can do, before the first frame is drawn.
            int level = Level;
            if (!SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Render)
                && !SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render))
                level = Math.Max(level, NoHdr);
            else if (MsaaSupported() < 4)
                level = Math.Max(level, NoMsaa);
            _level = level;
            Apply(null);
            if (!_listening)
            {
                _listening = true;
                Application.logMessageReceivedThreaded += OnLog;
            }
        }

        private static int MsaaSupported()
        {
            try
            {
                var desc = new RenderTextureDescriptor(256, 256, GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormat.D32_SFloat_S8_UInt) { msaaSamples = 4 };
                return SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc);
            }
            catch (Exception) { return 1; }
        }

        // Unity reports images it could not make in the log; step down when that happens.
        private static volatile bool _failed;
        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            if (message.StartsWith("RenderTexture.Create failed", StringComparison.Ordinal)
                || message.StartsWith("RenderPass: Unable to find surface", StringComparison.Ordinal))
                _failed = true;
        }

        /// <summary>Called every frame (by TankVisuals): steps down if the last frames failed to draw.</summary>
        public static void Watch()
        {
            if (!_failed) return;
            _failed = false;
            // Give each level a few frames to settle before judging it.
            if (Time.frameCount - _lastStepFrame < 5 || Level >= Simple) return;
            _lastStepFrame = Time.frameCount;
            Debug.LogWarning($"Frog Terrarium: this graphics chip could not draw at level {LevelName(Level)}; stepping down.");
            Level = Level + 1;
        }

        /// <summary>Sets the pipeline and the camera to the current level.</summary>
        public static void Apply(Camera cam)
        {
            int level = Level;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = level >= NoMsaa ? 1 : 4;
                urp.supportsHDR = level < NoHdr;
                urp.supportsCameraOpaqueTexture = level < Simple;
                urp.supportsCameraDepthTexture = level < Simple;
                SetAmbientOcclusion(urp, level < Simple);
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            cam.allowMSAA = level < NoMsaa;
            cam.allowHDR = level < NoHdr;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = level < Simple;
                // Without edge smoothing from MSAA, use the cheap screen-space kind instead.
                data.antialiasing = level >= NoMsaa && level < Simple ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            }
        }

        private static void SetAmbientOcclusion(UniversalRenderPipelineAsset urp, bool on)
        {
            try
            {
                // The list of renderers is not public, so reach it by name; skip quietly if it moves.
                var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (!(field?.GetValue(urp) is IList list)) return;
                foreach (var item in list)
                    if (item is ScriptableRendererData data)
                        foreach (var feature in data.rendererFeatures)
                            if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion") feature.SetActive(on);
            }
            catch (Exception) { }
        }
    }
}
