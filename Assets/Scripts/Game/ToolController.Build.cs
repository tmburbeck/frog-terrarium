using System.Collections.Generic;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The building side of the tools: placing parts, moving the selected part, and placing saved
    /// blueprints. Each frame it works out a "ghost" (where the part would go after snapping) and
    /// whether that spot is allowed, so the game can draw it green or red before you click.
    /// </summary>
    public sealed partial class ToolController
    {
        /// <summary>The part the Build tool places.</summary>
        public PartKind BuildKind { get; private set; } = PartKind.Rock;
        /// <summary>Grid placement rounds the spot to 25 cm and the turn to 45°. Off is free placement.</summary>
        public bool GridOn = true;
        /// <summary>How far the part being placed is turned, in degrees clockwise.</summary>
        public float BuildRotation;
        /// <summary>The size of the part being placed (0.5 to 2 times normal).</summary>
        public float BuildScale = 1f;
        /// <summary>The colour of the part being placed.</summary>
        public Color32 BuildColour = new Color32(130, 125, 118, 255);

        /// <summary>Id of the selected building part (0 = none).</summary>
        public int SelectedPartId { get; set; }

        /// <summary>The blueprint the "place blueprint" tool is holding, if any.</summary>
        public Blueprint ActiveBlueprint { get; private set; }

        /// <summary>Where the part being placed or moved would go, or null when there is nothing to show.</summary>
        public PlacedPart Ghost { get; private set; }
        /// <summary>The blueprint's parts where they would go, and the height each one's bottom would be at.</summary>
        public readonly List<PlacedPart> BlueprintGhost = new List<PlacedPart>();
        public readonly List<float> BlueprintGhostHeights = new List<float>();
        public bool GhostValid { get; private set; }
        public string GhostWhy { get; private set; } = "";

        private float _ghostKey = float.NaN;
        private float _bpX, _bpZ, _bpRotation;

        /// <summary>Picks the part to build and switches to the Build tool, starting with its natural colour.</summary>
        public void ChooseBuildPart(PartKind kind)
        {
            if (kind != BuildKind || CurrentTool != Tool.Build)
            {
                var info = Parts.Info(kind);
                BuildColour = new Color32(info.R, info.G, info.B, 255);
            }
            BuildKind = kind;
            SetTool(Tool.Build);
        }

        /// <summary>Turns the part (or blueprint) being placed: one grid step, or 15° in free placement.</summary>
        public void Turn(int direction)
        {
            float step = GridOn ? Parts.GridAngle : 15f;
            BuildRotation = Parts.NormaliseAngle(Mathf.Round((BuildRotation + direction * step) / step) * step);
            _ghostKey = float.NaN;
        }

        /// <summary>Picks up the selected part to move it. Whatever stands on it comes too.</summary>
        public void StartMovingPart()
        {
            var p = _world?.Parts.Find(SelectedPartId);
            if (p == null) return;
            BuildRotation = p.Rotation;
            BuildScale = p.Scale;
            SetTool(Tool.MovePart);
        }

        /// <summary>Copies the selected part's kind, size, turn and colour into the Build tool.</summary>
        public void CopySelectedPart()
        {
            var p = _world?.Parts.Find(SelectedPartId);
            if (p == null) return;
            ChooseBuildPart(p.Kind);
            BuildRotation = p.Rotation;
            BuildScale = p.Scale;
            BuildColour = new Color32(p.R, p.G, p.B, 255);
            SelectedPartId = 0;
        }

        /// <summary>Holds a blueprint ready to place with the next click.</summary>
        public void StartPlacingBlueprint(Blueprint bp)
        {
            if (bp == null || bp.Parts.Count == 0) { LastAction = "That blueprint is empty."; return; }
            ActiveBlueprint = bp;
            BuildRotation = 0f;
            SetTool(Tool.PlaceBlueprint);
            LastAction = "Click to place \"" + bp.Name + "\" (" + bp.Parts.Count + " parts"
                + (_world != null && _world.Economy.Enabled ? ", " + Economy.Format(BlueprintCost(bp)) : "") + ").";
        }

        /// <summary>What placing a whole blueprint costs in money mode.</summary>
        public static float BlueprintCost(Blueprint bp)
        {
            float total = 0f;
            foreach (var p in bp.Parts) total += Economy.PartCost(p);
            return total;
        }

        public void RemovePart(int id)
        {
            var p = _world?.Parts.Find(id);
            if (p == null) return;
            LastAction = "Removed the " + p.Info.Name.ToLowerInvariant();
            _world.Economy.RefundHalf(Economy.PartCost(p));
            _world.Parts.Remove(id);
            if (SelectedPartId == id) SelectedPartId = 0;
            _ghostKey = float.NaN;
        }

        /// <summary>Changes the selected part's size, if it still fits. Returns why not.</summary>
        public string ResizeSelectedPart(float scale)
        {
            var p = _world?.Parts.Find(SelectedPartId);
            if (p == null || Mathf.Abs(p.Scale - scale) < 1e-4f) return "";
            var target = p.Clone();
            target.Scale = scale;
            // Making a part bigger costs the difference; making it smaller gives nothing back.
            float extra = Economy.PartCost(p.Kind, scale) - Economy.PartCost(p);
            if (!_world.Economy.CanAfford(extra)) return "not enough money (" + Economy.Format(extra) + " more)";
            if (!_world.Parts.Move(p.Id, target, _world.Terrain, out string why)) return why;
            _world.Economy.Spend(extra, "", out _);
            return "";
        }

        /// <summary>Turns the selected part where it stands, if it still fits.</summary>
        public string TurnSelectedPart(int direction)
        {
            var p = _world?.Parts.Find(SelectedPartId);
            if (p == null) return "";
            float step = GridOn ? Parts.GridAngle : 15f;
            var target = p.Clone();
            target.Rotation = Parts.NormaliseAngle(Mathf.Round((p.Rotation + direction * step) / step) * step);
            return _world.Parts.Move(p.Id, target, _world.Terrain, out string why) ? "" : why;
        }

        public void RecolourSelectedPart(Color32 c)
        {
            var p = _world?.Parts.Find(SelectedPartId);
            if (p == null || (p.R == c.r && p.G == c.g && p.B == c.b)) return;
            p.R = c.r; p.G = c.g; p.B = c.b;
            _world.Parts.Changed();
        }

        /// <summary>True when a menu text box has the keyboard, so typing a name does not trigger shortcuts.</summary>
        private static bool Typing => GUIUtility.keyboardControl != 0;

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleBuildKeys(bool shift)
        {
            if (Typing) return;
            if (Input.GetKeyDown(KeyCode.G)) { GridOn = !GridOn; _ghostKey = float.NaN; }
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (IsPlacing) Turn(shift ? -1 : 1);
                else if (SelectedPartId != 0) LastActionIfAny(TurnSelectedPart(shift ? -1 : 1));
            }
            if (Input.GetKeyDown(KeyCode.Delete) && SelectedPartId != 0) RemovePart(SelectedPartId);
        }
#endif

        private void LastActionIfAny(string why)
        {
            if (!string.IsNullOrEmpty(why)) LastAction = "Can't turn it: " + why;
        }

        /// <summary>True while the tool is placing or moving something that has a ghost.</summary>
        public bool IsPlacing => CurrentTool == Tool.Build || CurrentTool == Tool.MovePart || CurrentTool == Tool.PlaceBlueprint;

        private void UpdateGhost(World world)
        {
            if (!IsPlacing || !HasCursor)
            {
                Ghost = null;
                BlueprintGhost.Clear();
                BlueprintGhostHeights.Clear();
                return;
            }
            float x = CursorPoint.x, z = CursorPoint.z;
            if (CurrentTool == Tool.PlaceBlueprint)
            {
                UpdateBlueprintGhost(world, x, z);
                return;
            }

            var ghost = new PlacedPart { Kind = BuildKind, X = x, Z = z, Rotation = BuildRotation, Scale = BuildScale, R = BuildColour.r, G = BuildColour.g, B = BuildColour.b };
            if (CurrentTool == Tool.MovePart)
            {
                var moving = world.Parts.Find(SelectedPartId);
                if (moving == null) { SetTool(Tool.Select); Ghost = null; return; }
                ghost.Id = moving.Id; // so it does not bump into itself
                ghost.Kind = moving.Kind;
                ghost.R = moving.R; ghost.G = moving.G; ghost.B = moving.B;
            }
            world.Parts.Snap(ghost, world.Terrain, GridOn);
            GhostValid = world.Parts.Check(ghost, world.Terrain, out string why);
            if (GhostValid && CurrentTool == Tool.Build)
            {
                // Show money and research problems before the click, too.
                var eco = world.Economy;
                if (!eco.PartUnlocked(ghost.Kind)) { GhostValid = false; why = "it needs " + Economy.UnlockedBy(ghost.Kind) + " research (Money menu)."; }
                else if (!eco.CanAfford(Economy.PartCost(ghost))) { GhostValid = false; why = "not enough money (" + Economy.Format(Economy.PartCost(ghost)) + ")."; }
            }
            GhostWhy = why;
            Ghost = ghost;
        }

        private void UpdateBlueprintGhost(World world, float x, float z)
        {
            var bp = ActiveBlueprint;
            if (bp == null) { SetTool(Tool.Select); return; }
            float r = BuildRotation;
            if (GridOn) Parts.ToGrid(ref x, ref z, ref r);
            // Trying the blueprint out costs a little, so only do it when the spot or turn changes.
            float key = x * 7919f + z * 104729f + r + (GridOn ? 0.5f : 0f) + world.Parts.Version * 13f;
            if (key == _ghostKey && BlueprintGhost.Count > 0) return;
            _ghostKey = key;
            _bpX = x; _bpZ = z; _bpRotation = r;
            BlueprintGhost.Clear();
            BlueprintGhostHeights.Clear();
            var list = bp.PartsAt(x, z, r);
            foreach (var p in list)
            {
                int below = p.OnPartId - 1;
                float h = below >= 0 && below < BlueprintGhost.Count
                    ? BlueprintGhostHeights[below] + BlueprintGhost[below].SizeY
                    : world.Terrain.HeightAt(p.X, p.Z);
                BlueprintGhost.Add(p);
                BlueprintGhostHeights.Add(h);
            }
            GhostValid = bp.Fits(world.Parts, world.Terrain, x, z, r, out string why);
            GhostWhy = why;
            _ghostKey = x * 7919f + z * 104729f + r + (GridOn ? 0.5f : 0f) + world.Parts.Version * 13f; // Fits changed the version
        }

        private void PressBuild(World world)
        {
            switch (CurrentTool)
            {
                case Tool.Build:
                {
                    if (Ghost == null) return;
                    var eco = world.Economy;
                    if (!eco.PartUnlocked(Ghost.Kind)) { LastAction = Ghost.Info.Name + " needs " + Economy.UnlockedBy(Ghost.Kind) + " research first (Money menu)."; break; }
                    float cost = Economy.PartCost(Ghost);
                    if (!eco.CanAfford(cost)) { eco.Spend(cost, "a " + Ghost.Info.Name.ToLowerInvariant(), out string poor); LastAction = poor; break; }
                    var p = world.Parts.Place(Ghost, world.Terrain, out string why);
                    if (p != null) eco.Spend(cost, "", out _);
                    LastAction = p != null ? "Placed a " + p.Info.Name.ToLowerInvariant() + (eco.Enabled ? " for " + Economy.Format(cost) : "") : "Can't build there: " + why;
                    break;
                }
                case Tool.MovePart:
                {
                    if (Ghost == null) return;
                    if (world.Parts.Move(SelectedPartId, Ghost, world.Terrain, out string why))
                    {
                        LastAction = "Moved the " + Ghost.Info.Name.ToLowerInvariant();
                        SetTool(Tool.Select);
                    }
                    else LastAction = "Can't move it there: " + why;
                    break;
                }
                case Tool.PlaceBlueprint:
                {
                    var bp = ActiveBlueprint;
                    if (bp == null) return;
                    var eco = world.Economy;
                    float cost = BlueprintCost(bp);
                    foreach (var part in bp.Parts)
                        if (!eco.PartUnlocked(part.Kind)) { LastAction = "This blueprint uses a " + part.Info.Name.ToLowerInvariant() + ", which needs " + Economy.UnlockedBy(part.Kind) + " research first."; return; }
                    if (!eco.CanAfford(cost)) { eco.Spend(cost, "This blueprint", out string poor); LastAction = poor; break; }
                    int n = bp.PlaceInto(world.Parts, world.Terrain, _bpX, _bpZ, _bpRotation, out string why);
                    if (n > 0) eco.Spend(cost, "", out _);
                    LastAction = n > 0 ? "Placed \"" + bp.Name + "\" (" + n + " parts" + (eco.Enabled ? ", " + Economy.Format(cost) : "") + ")" : "Can't place it there: " + why;
                    break;
                }
            }
            _ghostKey = float.NaN;
        }
    }
}
