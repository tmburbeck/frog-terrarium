using System.Collections;
using FrogTerrarium.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace FrogTerrarium.Tests
{
    /// <summary>The modern rendering set-up: URP, the camera effects, the room and the glass tank.</summary>
    public class GraphicsPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var name in new[] { "Test Bootstrap", "Simulation", "Tank Terrain", "Tank Visuals", "Main Camera" })
            {
                GameObject go;
                while ((go = GameObject.Find(name)) != null) Object.DestroyImmediate(go);
            }
            GameSettings.DepthOfField = true;
            GraphicsSafety.Level = GraphicsSafety.Full;
        }

        private static SandboxBootstrap Boot()
        {
            var root = new GameObject("Test Bootstrap");
            root.SetActive(false);
            var boot = root.AddComponent<SandboxBootstrap>();
            boot.startingFrogs = 4;
            boot.startingInsects = 10;
            root.SetActive(true);
            return boot;
        }

        [Test]
        public void Project_UsesTheUniversalRenderPipeline()
        {
            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline,
                "the project should draw with URP (see Editor/RenderPipelineSetup)");
            Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace, "modern lighting needs linear colour");
        }

        [UnityTest]
        public IEnumerator Tank_HasGlassFrameAndStand()
        {
            var boot = Boot();
            yield return null;
            yield return null;
            // 4 glass walls, 4 bottom rails, 4 top rails, 4 corner posts and the stand.
            Assert.AreEqual(17, boot.Visuals.TankPieces);
            var front = GameObject.Find("Glass front");
            Assert.IsNotNull(front);
            Assert.AreEqual("Glass", front.GetComponent<Renderer>().sharedMaterial.name);
            Assert.AreEqual(ShadowCastingMode.Off, front.GetComponent<Renderer>().shadowCastingMode, "glass should not cast shadows");
            float halfX = boot.Runner.Sim.World.HalfSizeX;
            Assert.AreEqual(halfX + TankVisuals.GlassGap, front.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Tank_GlassGrowsWithABiggerTank()
        {
            var boot = Boot();
            yield return null;
            Assert.IsTrue(boot.Runner.Sim.World.ResizeTank(13f, 8f));
            yield return null;
            yield return null;
            var right = GameObject.Find("Glass right");
            Assert.IsNotNull(right);
            Assert.AreEqual(8f + TankVisuals.GlassGap, right.GetComponent<MeshFilter>().sharedMesh.bounds.extents.z, 1e-3f);
            Assert.AreEqual(17, boot.Visuals.TankPieces, "the old glass should be gone");
        }

        [UnityTest]
        public IEnumerator Camera_HasEffectsAndFocusFollowsZoom()
        {
            var boot = Boot();
            yield return null;
            var cam = Camera.main;
            Assert.IsTrue(cam.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.AreEqual(CameraClearFlags.Skybox, cam.clearFlags);
            Assert.AreEqual("Backdrop", RenderSettings.skybox.name);
            var profile = boot.Visuals.Effects.sharedProfile;
            Assert.IsTrue(profile.TryGet<Tonemapping>(out _));
            Assert.IsTrue(profile.TryGet<Bloom>(out _));
            Assert.IsTrue(profile.TryGet<DepthOfField>(out var focus));

            var orbit = cam.GetComponent<OrbitCamera>();
            orbit.distance = 3f;
            yield return null;
            Assert.AreEqual(3f, focus.focusDistance.value, 1e-3f);
            Assert.IsTrue(focus.active);

            GameSettings.DepthOfField = false;
            yield return null;
            Assert.IsFalse(focus.active, "the Settings switch turns the focus blur off");
        }

        [UnityTest]
        public IEnumerator Frogs_ArePaintedWithTheSkinAndEyeShaders()
        {
            var skin = GameMaterials.Load("FrogSkin");
            var eye = GameMaterials.Load("FrogEye");
            Assert.AreEqual("FrogTerrarium/FrogSkin", skin.shader.name);
            Assert.AreEqual("FrogTerrarium/FrogEye", eye.shader.name);
            Assert.IsTrue(skin.enableInstancing && eye.enableInstancing, "one draw call for many frogs needs instancing");
            Assert.IsTrue(skin.shader.isSupported && eye.shader.isSupported);

            var boot = Boot();
            yield return null;
            yield return null;
            yield return null;
            var frogs = boot.Runner.Frogs;
            Assert.IsTrue(frogs.Detailed, "frogs should use the skin and eye shaders, not plain colours");
            // 4 frogs: a body, 6 leg bones, 4 feet and 2 eyes each at the very least.
            Assert.GreaterOrEqual(frogs.LastInstanceCount, 4 * 13);
        }

        [UnityTest]
        public IEnumerator SimpleGraphics_TurnsTheHeavyEffectsOff_AndFullTurnsThemBack()
        {
            Boot();
            yield return null;
            var cam = Camera.main;
            var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            GraphicsSafety.Level = GraphicsSafety.NoMsaa;
            Assert.AreEqual(1, urp.msaaSampleCount, "level 1 turns off edge smoothing");
            Assert.IsTrue(urp.supportsHDR);
            GraphicsSafety.Level = GraphicsSafety.Simple;
            Assert.IsFalse(cam.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.IsFalse(urp.supportsHDR);
            Assert.IsFalse(cam.allowMSAA);
            GraphicsSafety.Level = GraphicsSafety.Full;
            Assert.IsTrue(cam.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.AreEqual(4, urp.msaaSampleCount);
            Assert.IsTrue(urp.supportsHDR && cam.allowHDR && cam.allowMSAA);
        }

        [Test]
        public void Focus_IsSharperPerUnitWhenZoomedIn()
        {
            Assert.Greater(TankVisuals.ApertureFor(1f), TankVisuals.ApertureFor(5f));
            Assert.AreEqual(1f, TankVisuals.ApertureFor(100f));
            Assert.AreEqual(32f, TankVisuals.ApertureFor(0.01f));
        }

        [Test]
        public void RoomReflection_HasTheCeilingUpAndALampPatch()
        {
            Assert.AreEqual(Vector3.up, TankVisuals.FaceDirection(CubemapFace.PositiveY, 0f, 0f));
            Assert.AreEqual(Vector3.down, TankVisuals.FaceDirection(CubemapFace.NegativeY, 0f, 0f));
            Assert.AreEqual(Vector3.forward, TankVisuals.FaceDirection(CubemapFace.PositiveZ, 0f, 0f));
            Assert.AreEqual(Vector3.right, TankVisuals.FaceDirection(CubemapFace.PositiveX, 0f, 0f));
            var cube = TankVisuals.MakeRoomReflection(Color.white, Color.grey, Color.black, Vector3.up);
            Assert.Greater(cube.mipmapCount, 4, "rough surfaces need the blurrier mip levels");
            Object.DestroyImmediate(cube);
        }
    }
}
