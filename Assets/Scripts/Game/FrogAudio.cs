using System.Collections.Generic;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The tank's sounds, all made by <see cref="SoundSynth"/> when the game starts (no recordings,
    /// so nothing to license). Adult males call now and then in their own species' voice (and the
    /// throat sac swells in time, see <see cref="ThroatOf"/>); frogs splash into water; tongues
    /// "thwip"; eggs plop; crickets chirp; and each playground piece has its own sound.
    ///
    /// Sounds come from where they happen in the tank, so they get quieter as you zoom out. Volume
    /// and background level are in Settings. Pausing the game pauses the sound.
    /// It only reads the simulation; it never changes it.
    /// </summary>
    public sealed class FrogAudio : MonoBehaviour
    {
        public SimulationRunner runner;

        /// <summary>At most this many frogs call at once, so a big tank is a chorus, not a din.</summary>
        public const int MaxCallers = 3;
        /// <summary>Roughly how often a calm adult male calls, per second.</summary>
        public const float CallChance = 1f / 30f;
        /// <summary>Sounds further than this from the camera are not played at all.</summary>
        public const float Hearing = 30f;
        private const int Voices = 12;

        private readonly AudioSource[] _sources = new AudioSource[Voices];
        private int _next;
        private AudioClip[] _sounds;
        private readonly Dictionary<FrogSpecies, AudioClip> _calls = new Dictionary<FrogSpecies, AudioClip>();

        private sealed class Heard
        {
            public bool InWater;
            public float Cooldown;
            public int Ride;
            public float CallStart = -100f, CallLength, CallRate = 1f;
            public int SeenFrame;
        }
        private readonly Dictionary<int, Heard> _frogs = new Dictionary<int, Heard>();
        private int _frame, _newestSeen = int.MaxValue;
        private float _clock, _nextCricket, _nextPlop;
        private World _world;

        /// <summary>How many calls have started since the game began (for tests).</summary>
        public int CallsStarted { get; private set; }
        /// <summary>How many sounds of each kind have played (for tests).</summary>
        public readonly int[] Played = new int[SoundSynth.KindCount];

        private void Awake()
        {
            _sounds = new AudioClip[SoundSynth.KindCount];
            for (int k = 0; k < _sounds.Length; k++) _sounds[k] = ToClip(SoundSynth.Make((SoundKind)k), ((SoundKind)k).ToString());
            for (int k = 0; k < Voices; k++)
            {
                var go = new GameObject("Sound " + k);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 2f;
                src.maxDistance = 40f;
                src.dopplerLevel = 0f;
                _sources[k] = src;
            }
        }

        public static AudioClip ToClip(float[] samples, string name)
        {
            var clip = AudioClip.Create(name, Mathf.Max(1, samples.Length), 1, SoundSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>The call clip for a species (hybrids get their own blended voice, made the first time it is needed).</summary>
        public AudioClip CallOf(FrogSpecies sp)
        {
            if (!_calls.TryGetValue(sp, out var clip))
            {
                clip = ToClip(SoundSynth.Call(sp, (uint)(sp.CallPitch * 10f)), sp.Name + " call");
                _calls[sp] = clip;
            }
            return clip;
        }

        /// <summary>How full this frog's throat sac is right now (0 to 1): it swells with each note of a call.</summary>
        public float ThroatOf(int id) =>
            _frogs.TryGetValue(id, out var h) ? FrogMoves.Throat(_clock - h.CallStart, h.CallLength, h.CallRate) : 0f;

        private void Update()
        {
            if (runner == null || runner.Sim == null) return;
            AudioListener.volume = GameSettings.Volume;
            bool paused = runner.Sim.TimeScale == 0f;
            AudioListener.pause = paused;
            if (paused) return;
            var world = runner.Sim.World;
            if (world != _world)
            {
                // A new game or a load: start listening afresh, without a burst of sounds for everything already there.
                _world = world;
                _frogs.Clear();
                _newestSeen = int.MaxValue;
            }
            float dt = Time.deltaTime * runner.Sim.TimeScale;
            _clock += dt;
            _frame++;
            var cam = Camera.main;
            Vector3 ear = cam != null ? cam.transform.position : Vector3.zero;
            float hear2 = Hearing * Hearing;
            var a = world.Agents;

            int calling = 0;
            foreach (var h in _frogs.Values) if (_clock - h.CallStart < h.CallLength) calling++;

            int newest = _newestSeen == int.MaxValue ? -1 : _newestSeen;
            int insects = 0, maxId = newest;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Insect) { insects++; continue; }
                int id = a.Id[i];
                if (id > maxId) maxId = id;
                var pos = new Vector3(a.PosX[i], a.PosY[i], a.PosZ[i]);
                bool near = (pos - ear).sqrMagnitude < hear2;
                var stage = a.Stage[i];

                if (stage == LifeStage.Egg)
                {
                    // A newly laid egg: a little plop (one per moment, however big the clutch).
                    if (_newestSeen != int.MaxValue && id > _newestSeen && near && _clock > _nextPlop)
                    {
                        Play(SoundKind.Plop, pos, 0.5f, Random.Range(0.9f, 1.2f));
                        _nextPlop = _clock + 0.4f;
                    }
                    continue;
                }
                if (stage == LifeStage.Tadpole) continue;

                bool fresh = !_frogs.TryGetValue(id, out var heard);
                if (fresh) { heard = new Heard(); _frogs[id] = heard; }
                heard.SeenFrame = _frame;
                var sp = SpeciesCatalog.FrogOf(a, i);
                float size = sp.AdultSize * sp.SizeFraction(a.Growth[i]) * a.Genes[i].Size;
                bool water = a.RidePart[i] == 0 && world.Terrain.WaterDepthAt(pos.x, pos.z) > size * 0.3f;
                int ride = a.RidePart[i];
                float cooldown = a.Cooldown[i];
                if (!fresh && near)
                {
                    if (water && !heard.InWater) Play(SoundKind.Splash, pos, Mathf.Clamp(size * 3.5f, 0.3f, 1f), Random.Range(0.85f, 1.15f) * Mathf.Clamp(0.25f / Mathf.Max(0.05f, size), 0.7f, 1.6f));
                    if (a.Goal[i] == Goal.Eat && cooldown > heard.Cooldown + 0.3f) Play(SoundKind.Gulp, pos, 0.5f, Random.Range(0.9f, 1.15f));
                    if (ride != 0 && heard.Ride != ride)
                    {
                        var part = world.Parts.Find(ride);
                        if (part != null) PlayRide(part.Kind, pos);
                    }
                }
                heard.InWater = water;
                heard.Cooldown = cooldown;
                heard.Ride = ride;

                // Calling: calm adult males call now and then, in their species' voice.
                bool canCall = a.Sex[i] == Sex.Male && (stage == LifeStage.Adult || stage == LifeStage.Elder)
                    && a.Goal[i] != Goal.Sleep && a.Goal[i] != Goal.Flee && a.Goal[i] != Goal.Escape && ride == 0
                    && a.Comfort[i] > 0.5f && _clock - heard.CallStart > heard.CallLength + 3f;
                if (canCall && calling < MaxCallers && Random.value < CallChance * dt)
                {
                    StartCall(a, i, heard, sp, near);
                    calling++;
                }
            }
            if (maxId > newest) _newestSeen = maxId;
            else if (_newestSeen == int.MaxValue) _newestSeen = maxId;

            // Crickets chirp from somewhere in the tank, more often the more there are.
            if (insects > 0 && _clock > _nextCricket)
            {
                int pick = PickInsect(a, insects);
                if (pick >= 0)
                {
                    var pos = new Vector3(a.PosX[pick], a.PosY[pick], a.PosZ[pick]);
                    if ((pos - ear).sqrMagnitude < hear2) Play(SoundKind.Cricket, pos, 0.35f * GameSettings.Ambience, Random.Range(0.92f, 1.08f));
                }
                _nextCricket = _clock + Mathf.Lerp(6f, 1.2f, Mathf.Clamp01(insects / 60f)) * Random.Range(0.6f, 1.4f);
            }

            if (_frame % 300 == 0)
            {
                var gone = new List<int>();
                foreach (var kv in _frogs) if (_frame - kv.Value.SeenFrame > 60) gone.Add(kv.Key);
                foreach (int id in gone) _frogs.Remove(id);
            }
        }

        /// <summary>Makes a frog call right now (the throat swells and, if the camera is near enough, it is heard).</summary>
        public void CallNow(int index)
        {
            var a = runner.Sim.World.Agents;
            int id = a.Id[index];
            if (!_frogs.TryGetValue(id, out var heard)) { heard = new Heard { SeenFrame = _frame }; _frogs[id] = heard; }
            StartCall(a, index, heard, SpeciesCatalog.FrogOf(a, index), true);
        }

        private void StartCall(AgentStore a, int i, Heard heard, FrogSpecies sp, bool audible)
        {
            bool loud = Mutations.Shows(a.Genes[i].Visible, Mutation.LoudCall);
            heard.CallStart = _clock;
            heard.CallLength = SoundSynth.CallLength(sp);
            heard.CallRate = Mathf.Max(0.5f, sp.CallRate);
            CallsStarted++;
            if (audible)
                PlayClip(CallOf(sp), new Vector3(a.PosX[i], a.PosY[i], a.PosZ[i]), loud ? 1f : 0.65f,
                    1f / Mathf.Sqrt(Mathf.Clamp(a.Genes[i].Size, 0.6f, 1.6f)), loud ? 3f : 2f);
        }

        private static int PickInsect(AgentStore a, int insects)
        {
            int target = Random.Range(0, insects);
            for (int i = 0; i < a.Count; i++)
                if (a.Kind[i] == AgentKind.Insect && target-- == 0) return i;
            return -1;
        }

        private void PlayRide(PartKind kind, Vector3 pos)
        {
            switch (kind)
            {
                case PartKind.Slide: Play(SoundKind.Slide, pos, 0.55f, Random.Range(0.9f, 1.2f)); break;
                case PartKind.SpiralStairs: Play(SoundKind.Stairs, pos, 0.4f, Random.Range(0.9f, 1.15f)); break;
                case PartKind.AirTunnel: Play(SoundKind.Tunnel, pos, 0.5f, Random.Range(0.95f, 1.1f)); break;
                case PartKind.Elevator: Play(SoundKind.Lift, pos, 0.45f, 1f); break;
                case PartKind.Teleporter: Play(SoundKind.Teleport, pos, 0.55f, Random.Range(0.95f, 1.1f)); break;
            }
        }

        private void Play(SoundKind kind, Vector3 pos, float volume, float pitch)
        {
            Played[(int)kind]++;
            PlayClip(_sounds[(int)kind], pos, volume, pitch, 2f);
        }

        private void PlayClip(AudioClip clip, Vector3 pos, float volume, float pitch, float minDistance)
        {
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            src.transform.position = pos;
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.minDistance = minDistance;
            src.Play();
        }
    }
}
