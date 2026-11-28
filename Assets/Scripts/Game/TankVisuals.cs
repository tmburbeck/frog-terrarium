using FrogTerrarium.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Everything that makes the tank look like a real terrarium on a table, rather than objects
    /// floating in a grey void:
    /// - the lights: a warm lamp above the tank that casts soft shadows, a cool fill light from the
    ///   other side, and soft light from the room (ambient light) in the style set's colours;
    /// - the room around the tank (a soft gradient "sky"), which shiny things such as wet frogs,
    ///   water and glass reflect, with a bright patch where the lamp is;
    /// - the glass walls, a dark metal frame and a wooden stand under the tank;
    /// - the camera effects (post-processing): film-like colour (tonemapping), a soft glow on
    ///   bright things (bloom), darker corners (vignette) and depth of field, which keeps what the
    ///   camera looks at sharp and blurs the far side of the tank like a real close-up photo.
    /// Reads the simulation; never changes it.
    /// </summary>
    public sealed class TankVisuals : MonoBehaviour
    {
        public SimulationRunner runner;

        /// <summary>Height of the glass walls above the bottom of the tank (the ground can rise to 4).</summary>
        public const float WallHeight = 5.2f;
        /// <summary>Gap between the soil and the glass.</summary>
        public const float GlassGap = 0.03f;

        public Light Lamp { get; private set; }
        public Light Fill { get; private set; }
        public Volume Effects { get; private set; }
        /// <summary>How many glass walls, frame pieces and stand pieces are standing (for tests).</summary>
        public int TankPieces => _tank != null ? _tank.childCount : 0;

        private Transform _tank;
        private float _builtX = -1f, _builtZ = -1f;
        private int _builtStyle = -1;
        private Material _glass, _frame, _stand, _backdrop;
        private DepthOfField _focus;
        private Cubemap _room;
        private Mesh _cube;

        private void Awake()
        {
            _glass = GameMaterials.Load("Glass");
            _frame = GameMaterials.Load("Frame");
            _stand = GameMaterials.Load("Stand");
            _backdrop = GameMaterials.Load("Backdrop");
            _cube = DeviceView.PrimitiveMesh(PrimitiveType.Cube);
            _tank = new GameObject("Glass Tank").transform;
            _tank.SetParent(transform, false);
            MakeLights();
            MakeEffects();
        }

        private void Start() => SetUpCamera(Camera.main);

        private void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            var world = runner.Sim.World;
            if (!Mathf.Approximately(world.HalfSizeX, _builtX) || !Mathf.Approximately(world.HalfSizeZ, _builtZ))
                BuildTank(world.HalfSizeX, world.HalfSizeZ);
            if (world.Style != _builtStyle) ApplyStyle(world.Style);
            UpdateFocus();
            GraphicsSafety.Watch();
        }

        // ---------------------------------------------------------------- lights

        private void MakeLights()
        {
            var lampGo = new GameObject("Sun");
            lampGo.transform.SetParent(transform, false);
            lampGo.transform.rotation = Quaternion.Euler(58f, -35f, 0f);
            Lamp = lampGo.AddComponent<Light>();
            Lamp.type = LightType.Directional;
            Lamp.color = new Color(1f, 0.94f, 0.84f);
            Lamp.intensity = 1.6f;
            Lamp.shadows = LightShadows.Soft;
            Lamp.shadowStrength = 0.88f;
            RenderSettings.sun = Lamp;

            var fillGo = new GameObject("Fill Light");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
            Fill = fillGo.AddComponent<Light>();
            Fill.type = LightType.Directional;
            Fill.color = new Color(0.62f, 0.73f, 0.92f);
            Fill.intensity = 0.32f;
            Fill.shadows = LightShadows.None;
        }

        /// <summary>The room's colours from the style set's backdrop colour: lit ceiling, walls and floor.</summary>
        public static void RoomColours(TankStyle style, out Color top, out Color middle, out Color bottom)
        {
            var back = new Color(style.BackR / 255f, style.BackG / 255f, style.BackB / 255f);
            top = Color.Lerp(back, new Color(0.78f, 0.75f, 0.68f), 0.45f);
            middle = Color.Lerp(back, new Color(0.42f, 0.42f, 0.4f), 0.25f);
            bottom = back * 0.35f;
            bottom.a = 1f;
        }

        private void ApplyStyle(int style)
        {
            _builtStyle = style;
            var s = TankLooks.Styles[style >= 0 && style < TankLooks.Styles.Length ? style : 0];
            RoomColours(s, out var top, out var middle, out var bottom);
            if (_backdrop != null)
            {
                _backdrop.SetColor("_Top", top);
                _backdrop.SetColor("_Horizon", middle);
                _backdrop.SetColor("_Bottom", bottom);
                RenderSettings.skybox = _backdrop;
            }
            // Light from the room: from the ceiling, the walls and the table.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(top, Color.white, 0.2f) * 0.85f;
            RenderSettings.ambientEquatorColor = middle * 0.8f;
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.12f, 0.09f);
            RenderSettings.ambientIntensity = 1f;
            // What shiny things reflect: the same room, with the lamp as a bright soft patch.
            if (_room != null) Destroy(_room);
            _room = MakeRoomReflection(top, middle, bottom, -(Lamp.transform.rotation * Vector3.forward));
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = _room;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>
        /// A small cube map of the room, made from code. Each smaller mip level is a blurrier
        /// version (the lamp's patch spreads out), which is what rough surfaces reflect.
        /// </summary>
        public static Cubemap MakeRoomReflection(Color top, Color middle, Color bottom, Vector3 lampDirection)
        {
            const int size = 64;
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "Room reflection" };
            Color t = top.linear, m = middle.linear, b = bottom.linear;
            var lampColour = new Color(1f, 0.94f, 0.84f).linear;
            lampDirection.Normalize();
            for (int mip = 0; mip < cube.mipmapCount; mip++)
            {
                int n = Mathf.Max(1, size >> mip);
                // The lamp's patch: about 15 degrees across when sharp, wider and dimmer when blurred.
                float spread = 15f + mip * 14f;
                float brightness = 5f * (15f / spread) * (15f / spread);
                var pixels = new Color[n * n];
                for (int face = 0; face < 6; face++)
                {
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            var d = FaceDirection((CubemapFace)face, (x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                            Color c = d.y > 0f ? Color.Lerp(m, t, Mathf.SmoothStep(0f, 1f, d.y / 0.8f))
                                               : Color.Lerp(m, b, Mathf.SmoothStep(0f, 1f, -d.y / 0.5f));
                            float angle = Vector3.Angle(d, lampDirection);
                            float patch = 1f - Mathf.SmoothStep(0f, 1f, angle / spread);
                            c += lampColour * (patch * brightness);
                            c.a = 1f;
                            pixels[y * n + x] = c;
                        }
                    }
                    cube.SetPixels(pixels, (CubemapFace)face, mip);
                }
            }
            cube.Apply(false, true);
            return cube;
        }

        /// <summary>The direction through a point on a cube map face (u, v from -1 to 1, v going down).</summary>
        public static Vector3 FaceDirection(CubemapFace face, float u, float v)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -v, -u).normalized;
                case CubemapFace.NegativeX: return new Vector3(-1f, -v, u).normalized;
                case CubemapFace.PositiveY: return new Vector3(u, 1f, v).normalized;
                case CubemapFace.NegativeY: return new Vector3(u, -1f, -v).normalized;
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f).normalized;
                default: return new Vector3(-u, -v, -1f).normalized;
            }
        }

        // ---------------------------------------------------------------- camera and effects

        private void MakeEffects()
        {
            var go = new GameObject("Camera Effects");
            go.transform.SetParent(transform, false);
            Effects = go.AddComponent<Volume>();
            Effects.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Frog Terrarium effects";
            Effects.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);

            var colour = profile.Add<ColorAdjustments>();
            colour.postExposure.Override(0.45f);
            colour.contrast.Override(8f);
            colour.saturation.Override(6f);

            var white = profile.Add<WhiteBalance>();
            white.temperature.Override(5f);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(true);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.5f);

            _focus = profile.Add<DepthOfField>();
            _focus.mode.Override(DepthOfFieldMode.Bokeh);
            _focus.focusDistance.Override(18f);
            _focus.focalLength.Override(50f);
            _focus.aperture.Override(2f);
        }

        public static void SetUpCamera(Camera cam)
        {
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f; // close enough to look a frog in the eye
            cam.farClipPlane = 300f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.None; // the pipeline already smooths edges (4x MSAA)
            // Chips that cannot do all of that get less (see GraphicsSafety).
            GraphicsSafety.Apply(cam);
        }

        /// <summary>
        /// The lens setting (f-number) that gives the same, gentle amount of blur at any zoom: a
        /// frog twice as far away as the one in focus is blurred by about 0.6% of the screen height.
        /// Close up that is a lot of blur in the world; zoomed out over the whole tank, very little.
        /// </summary>
        public static float ApertureFor(float focusDistance) => Mathf.Clamp(8.7f / Mathf.Max(0.1f, focusDistance), 1f, 32f);

        private void UpdateFocus()
        {
            if (_focus == null) return;
            _focus.active = GameSettings.DepthOfField;
            var cam = Camera.main;
            var orbit = cam != null ? cam.GetComponent<OrbitCamera>() : null;
            if (orbit == null) return;
            _focus.focusDistance.value = Mathf.Max(0.1f, orbit.distance);
            _focus.aperture.value = ApertureFor(orbit.distance);
        }

        // ---------------------------------------------------------------- glass, frame and stand

        private void BuildTank(float halfX, float halfZ)
        {
            _builtX = halfX;
            _builtZ = halfZ;
            for (int i = _tank.childCount - 1; i >= 0; i--) Destroy(_tank.GetChild(i).gameObject);

            float gx = halfX + GlassGap, gz = halfZ + GlassGap;
            Glass("Glass front", new Vector3(0f, 0f, -gz), Vector3.back, gx);
            Glass("Glass back", new Vector3(0f, 0f, gz), Vector3.forward, gx);
            Glass("Glass left", new Vector3(-gx, 0f, 0f), Vector3.left, gz);
            Glass("Glass right", new Vector3(gx, 0f, 0f), Vector3.right, gz);

            // The frame: a deep rail round the bottom (it hides the bottom of the soil), a thin rail
            // round the top and a post at each corner.
            const float rail = 0.14f, bottomRail = 0.38f, topRail = 0.16f;
            for (int k = -1; k <= 1; k += 2)
            {
                Box("Frame", _frame, new Vector3(0f, bottomRail * 0.5f, k * gz), new Vector3(gx * 2f + rail, bottomRail, rail));
                Box("Frame", _frame, new Vector3(k * gx, bottomRail * 0.5f, 0f), new Vector3(rail, bottomRail, gz * 2f + rail));
                Box("Frame", _frame, new Vector3(0f, WallHeight - topRail * 0.5f, k * gz), new Vector3(gx * 2f + rail, topRail, rail));
                Box("Frame", _frame, new Vector3(k * gx, WallHeight - topRail * 0.5f, 0f), new Vector3(rail, topRail, gz * 2f + rail));
                for (int j = -1; j <= 1; j += 2)
                    Box("Frame", _frame, new Vector3(k * gx, WallHeight * 0.5f, j * gz), new Vector3(rail, WallHeight, rail));
            }

            // The wooden stand the tank sits on.
            Box("Stand", _stand, new Vector3(0f, -0.18f, 0f), new Vector3(gx * 2f + 9f, 0.36f, gz * 2f + 7f));
        }

        private void Glass(string name, Vector3 at, Vector3 outward, float halfWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_tank, false);
            var side = Vector3.Cross(Vector3.up, outward); // along the wall
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                at - side * halfWidth, at - side * halfWidth + Vector3.up * WallHeight,
                at + side * halfWidth + Vector3.up * WallHeight, at + side * halfWidth,
            };
            mesh.normals = new[] { outward, outward, outward, outward };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            bool flip = Vector3.Dot(Vector3.Cross(Vector3.up, side), outward) < 0f;
            mesh.triangles = flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _glass;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void Box(string name, Material material, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_tank, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = _cube;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        private void OnDestroy()
        {
            if (_room != null) Destroy(_room);
            if (Effects != null && Effects.sharedProfile != null) Destroy(Effects.sharedProfile);
        }
    }
}
