using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Combat Overhaul: the fight in the world. Hover the ground to see the route the rules allow (a line along the
    /// squares, green while the movement left covers it, amber where Dash would be needed), its cost and what is left;
    /// click to move there. A move that needs Dash asks first, and nothing is spent until the player agrees. Bodies
    /// walk their routes instead of jumping; attacks, spells, damage, healing, misses and deaths show in the world,
    /// each once, keyed by the combat log's sequence. Keys: 1–4 open the bar's groups, Space ends the turn, Tab shows
    /// the grid, Z faces the camera on whoever acts, right-drag turns the camera, Esc cancels. Nothing here decides a
    /// rule: plans come from <see cref="WorldSession.PlanPlayerMove"/> and every move is the engine's command.
    /// </summary>
    public sealed partial class ExplorationController
    {
        public const float WalkMetresPerSecond = 3.2f;

        private LineRenderer _pathNow;
        private LineRenderer _pathDash;
        private GameObject _destination;
        private readonly Queue<Vector3> _playerRoute = new Queue<Vector3>();
        private readonly Dictionary<string, Vector3> _markerTargets = new Dictionary<string, Vector3>();
        private int _fxSeen = -1;
        private Vector2 _lastMouse;

        /// <summary>When false the mouse does not drive the fight (tests and captures preview and click through the API).</summary>
        public bool MouseDrivesCombat { get; set; } = true;

        /// <summary>The plan for the destination under the cursor (or last previewed), null when none.</summary>
        public MovePlan HoverPlan { get; private set; }

        /// <summary>True while the player's body is still walking its last route.</summary>
        public bool PlayerWalking => _playerRoute.Count > 0;

        /// <summary>The floating feedback shown so far this fight (newest last), for tests and captures.</summary>
        public List<string> CombatFeedback { get; } = new List<string>();

        // ----- Squares and the world -----------------------------------------------------------------------

        /// <summary>The square under a world position (the grid is 1.5 m squares around where the fight began).</summary>
        public GridPos SquareAt(Vector3 world)
        {
            var z = Combat?.Snapshot?.Player?.Position.Z ?? 0;
            return new GridPos(Mathf.RoundToInt((world.x - _gridOrigin.x) / SquareMetres), Mathf.RoundToInt((world.z - _gridOrigin.z) / SquareMetres), z);
        }

        /// <summary>The centre of a square in the world.</summary>
        public Vector3 SquareCentre(GridPos p) => SquareToWorld(p);

        private bool PlayersMoveTurn => InFight && Combat.Snapshot.IsPlayersTurn && Combat.Snapshot.Prompt == null && !Combat.Composing && !Combat.AimingFeature && !Combat.ConfirmOpen;

        // ----- Preview and move ----------------------------------------------------------------------------

        /// <summary>
        /// Plans a move to the place in the world nearest a point: the square under it, or — when that square is a wall,
        /// a shut door or taken — the nearest square beside it that has a route. Shows the route and the cursor label.
        /// </summary>
        public MovePlan PreviewCombatMove(Vector3 world, Vector2? panelAt = null)
        {
            if (!PlayersMoveTurn || _session == null) { ClearPreview(); return null; }
            var clicked = SquareAt(world);
            var plan = _session.PlanPlayerMove(clicked);
            if (!plan.HasRoute && (plan.Problem == null || !plan.Problem.Contains("already")))
            {
                MovePlan best = null;
                var bestDistance = float.MaxValue;
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var near = new GridPos(clicked.X + dx, clicked.Y + dy, clicked.Z);
                        var p = _session.PlanPlayerMove(near);
                        if (!p.HasRoute) continue;
                        var d = (SquareToWorld(near) - world).sqrMagnitude;
                        if (d < bestDistance) { best = p; bestDistance = d; }
                    }

                if (best != null) plan = best;
            }

            HoverPlan = plan;
            DrawPreview(plan);
            Combat.ShowHover(HoverText(plan), panelAt ?? PanelPointOf(world), plan.HasRoute && (plan.Reachable || plan.DashReaches));
            return plan;
        }

        /// <summary>Moves along the previewed plan: at once when the movement left covers it; asks first when Dash is needed.</summary>
        public CommandResult CommitCombatMove()
        {
            var plan = HoverPlan;
            if (plan == null) return CommandResult.InvalidState("Point at where to go first.");
            if (!PlayersMoveTurn) return CommandResult.InvalidState("It is not your turn to move.");
            plan = _session.PlanPlayerMove(plan.Destination); // the battlefield may have changed since the hover
            HoverPlan = plan;
            if (plan.Reachable) return WalkPlan(plan);
            if (plan.HasRoute && plan.DashReaches)
            {
                var speed = Combat.Snapshot.Player.Speed;
                Combat.AskConfirm($"That is {plan.Cost} ft away and you have {plan.Left} ft of movement left. Dash uses your action this turn and gives you {speed} ft more. Dash and go there?",
                    "Dash and move", () => DashThenMove(plan.Destination));
                return CommandResult.Ok("Dash needed: confirm or cancel.");
            }

            if (plan.HasRoute && plan.ReachableSteps > 0)
            {
                var steps = plan.ReachableSteps;
                var dest = plan.Destination;
                Combat.AskConfirm($"That is {plan.Cost} ft away: too far this turn{(Combat.Snapshot.Player.ActionUsed ? " (your action is spent, so no Dash)" : " even with Dash")}. Go as far as you can along the way ({plan.Left} ft)?",
                    "Go as far as I can", () => WalkPartial(dest, steps));
                return CommandResult.Ok("Too far: confirm or cancel.");
            }

            var why = plan.Problem ?? (plan.Left == 0 ? "You have no movement left this turn." : "You cannot get there.");
            RefreshCombat(why, true);
            return CommandResult.Invalid(why, new[] { why });
        }

        /// <summary>Plans and moves to a point in the world in one go (tests and scripted input).</summary>
        public CommandResult CombatMoveToWorld(Vector3 world)
        {
            PreviewCombatMove(world);
            return CommitCombatMove();
        }

        private CommandResult DashThenMove(GridPos destination)
        {
            var dash = Combat.Snapshot.PlayerOptions.FirstOrDefault(o => o.Action is DashAction);
            if (dash == null || dash.Unavailable != null) { var why = dash?.Unavailable ?? "Dash is not available."; RefreshCombat(why, true); return CommandResult.Invalid(why, new[] { why }); }
            var dashed = CombatAct(dash.Action);
            if (!dashed.IsSuccess) return dashed;
            var plan = _session.PlanPlayerMove(destination);
            HoverPlan = plan;
            return plan.Reachable ? WalkPlan(plan) : CommandResult.Invalid(plan.Problem ?? "Still too far.", new[] { plan.Problem ?? "Still too far." });
        }

        private CommandResult WalkPartial(GridPos destination, int steps)
        {
            var plan = _session.PlanPlayerMove(destination);
            if (!plan.HasRoute || plan.ReachableSteps == 0) return CommandResult.InvalidState(plan.Problem ?? "No movement left.");
            var part = _session.PlanPlayerMove(plan.Path[System.Math.Min(steps, plan.ReachableSteps) - 1]);
            return part.Reachable ? WalkPlan(part) : CommandResult.InvalidState(part.Problem ?? "Cannot move.");
        }

        private CommandResult WalkPlan(MovePlan plan)
        {
            var before = Combat.Snapshot.Player.Position;
            var result = CombatAct(new MoveAction(plan.Path.ToArray()));
            if (result.IsSuccess)
            {
                // The body walks the route square by square (up to where the engine left it: an opportunity attack can stop it).
                var now = Combat.Snapshot?.Player?.Position ?? before;
                _playerRoute.Clear();
                foreach (var step in plan.Path)
                {
                    _playerRoute.Enqueue(SquareToWorld(step));
                    if (step.Equals(now)) break;
                }
            }

            ClearPreview();
            return result;
        }

        private string HoverText(MovePlan plan)
        {
            if (!plan.HasRoute) return plan.Problem ?? "You cannot go there.";
            var lines = new List<string>();
            if (plan.Reachable) lines.Add($"{plan.Cost} ft · {plan.LeftAfter} ft left after");
            else if (plan.DashReaches) lines.Add($"{plan.Cost} ft: you have {plan.Left} ft — needs Dash (uses your action). Click to choose.");
            else lines.Add($"{plan.Cost} ft: too far this turn ({plan.Left} ft left{(Combat.Snapshot.Player.ActionUsed ? ", action spent" : ", even with Dash")}).");
            if (plan.DifficultSquares > 0) lines.Add($"{plan.DifficultSquares} square(s) of difficult terrain (double cost).");
            if (plan.Provokes.Count > 0) lines.Add("Leaves the reach of " + string.Join(", ", plan.Provokes) + ": an opportunity attack.");
            return string.Join("\n", lines);
        }

        private Vector2 PanelPointOf(Vector3 world)
        {
            var cam = CameraRig?.Camera;
            var panel = Hud?.Root?.panel;
            if (cam == null || panel == null) return Vector2.zero;
            var screen = cam.WorldToScreenPoint(world);
            return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
        }

        private void DrawPreview(MovePlan plan)
        {
            EnsurePreviewObjects();
            if (plan == null || !plan.HasRoute)
            {
                _pathNow.positionCount = 0;
                _pathDash.positionCount = 0;
                _destination.SetActive(plan != null);
                if (plan != null) PlaceDestination(OnGround(SquareToWorld(plan.Destination)), new Color(0.85f, 0.3f, 0.2f));
                return;
            }

            var lift = Vector3.up * 0.06f;
            var points = new List<Vector3> { OnGround(SquareToWorld(Combat.Snapshot.Player.Position)) + lift };
            points.AddRange(plan.Path.Select(p => OnGround(SquareToWorld(p)) + lift));
            var split = Mathf.Clamp(plan.ReachableSteps, 0, plan.Path.Count);
            _pathNow.positionCount = split + 1;
            for (var i = 0; i <= split; i++) _pathNow.SetPosition(i, points[i]);
            _pathDash.positionCount = plan.Path.Count > split ? plan.Path.Count - split + 1 : 0;
            for (var i = split; i < points.Count && _pathDash.positionCount > 0; i++) _pathDash.SetPosition(i - split, points[i]);
            PlaceDestination(points[points.Count - 1], plan.Reachable ? new Color(0.45f, 0.85f, 0.4f) : plan.DashReaches ? new Color(0.95f, 0.7f, 0.25f) : new Color(0.85f, 0.3f, 0.2f));
        }

        private void PlaceDestination(Vector3 at, Color colour)
        {
            _destination.SetActive(true);
            _destination.transform.position = at + Vector3.up * 0.04f;
            _destination.GetComponent<Renderer>().sharedMaterial = _materials.Get(colour, 0.3f);
        }

        private void EnsurePreviewObjects()
        {
            if (_pathNow != null) return;
            _pathNow = MakeLine("Move path (reachable)", new Color(0.45f, 0.85f, 0.4f));
            _pathDash = MakeLine("Move path (needs Dash)", new Color(0.95f, 0.7f, 0.25f));
            _destination = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _destination.name = "Move destination";
            Object.Destroy(_destination.GetComponent<Collider>());
            _destination.transform.localScale = new Vector3(0.9f, 0.015f, 0.9f);
            _destination.SetActive(false);
        }

        private LineRenderer MakeLine(string name, Color colour)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.12f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.useWorldSpace = true;
            line.sharedMaterial = _materials.Get(colour, 0.2f);
            line.startColor = line.endColor = colour;
            line.positionCount = 0;
            return line;
        }

        private void ClearPreview()
        {
            HoverPlan = null;
            if (_pathNow != null) { _pathNow.positionCount = 0; _pathDash.positionCount = 0; _destination.SetActive(false); }
            Combat?.ShowHover(null, default, true);
        }

        private void DestroyPreview()
        {
            if (_pathNow != null) { Object.Destroy(_pathNow.gameObject); Object.Destroy(_pathDash.gameObject); Object.Destroy(_destination); }
            _pathNow = null;
            _pathDash = null;
            _destination = null;
            HoverPlan = null;
            _playerRoute.Clear();
            _markerTargets.Clear();
            _fxSeen = -1;
        }

        // ----- Each frame -----------------------------------------------------------------------------------

        private void UpdateCombatWorld()
        {
            if (CameraRig != null) FaceCamera.Target = CameraRig.Camera; // labels and floating words face the camera that draws them
            if (Combat == null) return;
            WalkBodies();
            if (!InFight) { if (HoverPlan != null) ClearPreview(); return; }
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys != null && !Typing())
            {
                if (keys.escapeKey.wasPressedThisFrame) { if (Combat.ConfirmOpen) Combat.HideConfirm(); else if (Combat.OpenGroup != null) Combat.CloseGroupPopup(); else ClearPreview(); }
                if (keys.tabKey.wasPressedThisFrame) Combat.ToggleGrid();
                if (keys.zKey.wasPressedThisFrame) FocusOnActive();
                if (Combat.Snapshot.IsPlayersTurn && !Combat.ConfirmOpen)
                {
                    if (keys.spaceKey.wasPressedThisFrame) { var end = Combat.Snapshot.PlayerOptions.FirstOrDefault(o => o.Action is EndTurnAction); if (end != null && end.Unavailable == null) CombatAct(end.Action); }
                    if (keys.digit1Key.wasPressedThisFrame) Combat.OpenGroupPopup("attacks");
                    if (keys.digit2Key.wasPressedThisFrame) Combat.OpenGroupPopup("actions");
                    if (keys.digit3Key.wasPressedThisFrame) Combat.OpenGroupPopup("spells");
                    if (keys.digit4Key.wasPressedThisFrame) Combat.OpenGroupPopup("features");
                }

                if (Combat.ConfirmOpen && keys.enterKey.wasPressedThisFrame) Combat.Confirm();
            }

            if (mouse == null || CameraRig?.Camera == null || !MouseDrivesCombat) return;
            var screen = mouse.position.ReadValue();
            if (mouse.rightButton.isPressed) // turn the camera; the player keeps control of it
            {
                var d = screen - _lastMouse;
                CameraRig.SetView(CameraRig.Yaw + d.x * 0.25f, Mathf.Clamp(CameraRig.Pitch - d.y * 0.2f, -10f, 60f));
            }

            _lastMouse = screen;
            if (!PlayersMoveTurn || PlayerWalking) return;
            if (Hud != null && Hud.IsPointerOverUi(screen)) { if (HoverPlan != null) ClearPreview(); return; }
            var ray = CameraRig.Camera.ScreenPointToRay(screen);
            if (!PointOnGround(ray, out var point)) { ClearPreview(); return; }
            var panelAt = RuntimePanelUtils.ScreenToPanel(Hud.Root.panel, new Vector2(screen.x, Screen.height - screen.y));
            if (HoverPlan == null || !SquareAt(point).Equals(HoverPlan.Destination) || mouse.leftButton.wasPressedThisFrame) PreviewCombatMove(point, panelAt);
            else Combat.ShowHover(HoverText(HoverPlan), panelAt, HoverPlan.HasRoute && (HoverPlan.Reachable || HoverPlan.DashReaches));
            if (mouse.leftButton.wasPressedThisFrame) CommitCombatMove();
        }

        /// <summary>A point lifted or lowered onto the ground under it (the scene's colliders, never a body), or as it was.</summary>
        private Vector3 OnGround(Vector3 p)
        {
            var best = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 2.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (!IsBody(h.collider) && h.point.y > best) best = h.point.y;
            return float.IsNegativeInfinity(best) ? p : new Vector3(p.x, best, p.z);
        }

        /// <summary>Where the cursor's ray meets the ground: the nearest collider that is not a body, else the fight's floor plane.</summary>
        private bool PointOnGround(Ray ray, out Vector3 point)
        {
            var hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Ignore).Where(h => !IsBody(h.collider)).OrderBy(h => h.distance).ToList();
            if (hits.Count > 0) { point = hits[0].point; return true; }
            var plane = new Plane(Vector3.up, new Vector3(0f, SquareToWorld(Combat.Snapshot.Player.Position).y, 0f));
            if (plane.Raycast(ray, out var d)) { point = ray.GetPoint(d); return true; }
            point = default;
            return false;
        }

        private bool IsBody(Collider c) => c != null && ((Player != null && c.transform.IsChildOf(Player.transform)) || (_markers != null && c.transform.IsChildOf(_markers.transform)));

        /// <summary>Turns the camera towards whoever is acting (Z), without taking control away.</summary>
        public void FocusOnActive()
        {
            var a = Combat?.Snapshot?.Active;
            if (a == null || CameraRig == null || Player == null) return;
            var at = SquareToWorld(a.Position);
            var from = Player.transform.position;
            var dir = at - from;
            if (dir.sqrMagnitude < 0.5f) return;
            CameraRig.SetView(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, Mathf.Max(CameraRig.Pitch, 18f));
        }

        // ----- Bodies walk instead of jumping ---------------------------------------------------------------

        /// <summary>Where a marker should be; it walks there (called by the marker update in place of a jump).</summary>
        private void MoveMarkerTo(string key, GameObject view, Vector3 target)
        {
            if (!_markerTargets.ContainsKey(key)) view.transform.position = target; // first sight: in place
            _markerTargets[key] = target;
        }

        /// <summary>The player's body: follow the planned route, or walk straight to where the engine put it.</summary>
        private void MovePlayerTo(Vector3 target)
        {
            if (Player == null) return;
            if (_playerRoute.Count > 0)
            {
                var last = _playerRoute.ToArray()[_playerRoute.Count - 1];
                if ((last - target).sqrMagnitude < 0.04f) return;
                _playerRoute.Clear();
            }

            if ((Player.transform.position - target).sqrMagnitude > 0.04f) _playerRoute.Enqueue(target);
        }

        private void WalkBodies()
        {
            var step = WalkMetresPerSecond * Time.deltaTime;
            if (Player != null && _playerRoute.Count > 0)
            {
                var to = _playerRoute.Peek();
                var pos = Player.transform.position;
                var flat = new Vector3(to.x - pos.x, 0f, to.z - pos.z);
                if (flat.magnitude <= step) { Player.Teleport(new LocalPose(to.x, pos.y, to.z, Player.transform.eulerAngles.y)); _playerRoute.Dequeue(); }
                else
                {
                    var next = pos + flat.normalized * step;
                    Player.Teleport(new LocalPose(next.x, pos.y, next.z, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg));
                }
            }

            foreach (var pair in _markerTargets)
            {
                if (!_markerViews.TryGetValue(pair.Key, out var view) || view == null) continue;
                var pos = view.transform.position;
                var flat = new Vector3(pair.Value.x - pos.x, 0f, pair.Value.z - pos.z);
                _markerAnims.TryGetValue(pair.Key, out var walker);
                if (flat.sqrMagnitude < 0.0001f) { walker?.SetSpeed(0f); continue; } // stopped: back to idle
                walker?.SetSpeed(WalkMetresPerSecond);
                view.transform.position = flat.magnitude <= step ? pair.Value : pos + flat.normalized * step;
                var figure = view.transform.Find("Figure"); // the body turns to the way it walks; its name label stays as it is
                if (figure != null) figure.rotation = Quaternion.Slerp(figure.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(figure.localEulerAngles.x, 0f, 0f), Mathf.Clamp01(Time.deltaTime * 10f));
            }
        }

        // ----- What happened, shown in the world (once per log entry) ----------------------------------------

        private Transform BodyOf(CombatantId id)
        {
            var s = Combat?.Snapshot;
            var c = s?.Order.FirstOrDefault(x => x.Id == id);
            if (c == null) return null;
            if (c.Kind == CombatantKind.Player) return Player != null ? Player.transform : null;
            return _markerViews.TryGetValue(id.ToString(), out var v) && v != null ? v.transform : null;
        }

        private void ShowCombatFeedback(EncounterSnapshot s)
        {
            if (s?.Log == null || _materials == null) return;
            var last = s.Log.Count > 0 ? s.Log[s.Log.Count - 1].Sequence : 0;
            if (_fxSeen < 0) { _fxSeen = last; return; } // a fight just opened (or a load): the history is not replayed
            var delay = 0f;
            _floatStack.Clear();
            var casters = new HashSet<CombatantId>();
            foreach (var e in s.Log)
            {
                if (e.Sequence <= _fxSeen) continue;
                var actor = BodyOf(e.ActorId);
                var target = BodyOf(e.TargetId);
                switch (e.Kind)
                {
                    case CombatLogKind.Attack:
                    case CombatLogKind.Spell:
                    {
                        // Phase 15: a spell the engine cast plays its caster's cast motion (a cast line may name no target);
                        // that caster's spell-attack lines in the same batch are the spell, not a weapon swing.
                        var actorAnim = AnimOf(e.ActorId);
                        if (e.Kind == CombatLogKind.Spell && casters.Add(e.ActorId)) actorAnim?.Play(CombatMotion.Cast);
                        if (actor == null || target == null || actor == target) break;
                        var ranged = IsRanged(e); // from the squares the engine holds, not from where the bodies are drawn mid-walk
                        Face(actor, target.position);
                        if (actorAnim != null) { if (!casters.Contains(e.ActorId)) actorAnim.Play(MotionFor(e.Text, ranged)); } // the body performs it
                        else if (actor != (Player != null ? Player.transform : null)) CombatLunge.Play(actor.Find("Figure") ?? actor, target.position, ranged ? 0.05f : 0.35f);
                        if (ranged) CombatProjectile.Launch(actor.position + Vector3.up * 1.3f, target.position + Vector3.up * 1.1f, _materials.Get(e.Kind == CombatLogKind.Spell ? SpellColour(e.Text) : new Color(0.75f, 0.7f, 0.6f), 0.6f), e.Kind == CombatLogKind.Spell ? 0.35f : 0.1f, 0.35f);
                        delay = ranged ? 0.35f : 0.2f;
                        var miss = e.Text.IndexOf(": miss", System.StringComparison.OrdinalIgnoreCase) >= 0 || e.Text.EndsWith("miss.", System.StringComparison.OrdinalIgnoreCase);
                        var crit = e.Text.IndexOf("critical", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (e.Kind == CombatLogKind.Attack && miss)
                        {
                            Float(target, "Miss", new Color(0.8f, 0.8f, 0.8f), delay, 1f);
                            if (HasShield(e.TargetId)) AnimOf(e.TargetId)?.Play(CombatMotion.Block); // a shield turned it
                        }
                        else if (crit) Float(target, "Critical!", new Color(1f, 0.85f, 0.3f), delay, 1.3f);
                        break;
                    }

                    case CombatLogKind.Damage:
                        // The applied damage (the victim is the entry's actor, with no target); the dice lines before it are details.
                        if (!e.TargetId.IsEmpty) break;
                        target = actor;
                        if (target == null) break;
                        Float(target, "-" + e.Value.ToString(CultureInfo.InvariantCulture) + (e.Text.IndexOf("resistance", System.StringComparison.OrdinalIgnoreCase) >= 0 ? " resisted" : e.Text.IndexOf("immun", System.StringComparison.OrdinalIgnoreCase) >= 0 ? " immune" : string.Empty), new Color(0.95f, 0.35f, 0.25f), delay + 0.05f, 1.15f);
                        var hurt = AnimOf(e.ActorId); // the applied-damage entry names the victim as its actor
                        if (hurt != null) hurt.Play(CombatMotion.Hit);
                        else if (Player == null || target != Player.transform) CombatLunge.Flinch(target.Find("Figure") ?? target, delay);
                        break;
                    case CombatLogKind.Healing:
                        if (target == null) target = actor;
                        if (target != null) Float(target, "+" + e.Value.ToString(CultureInfo.InvariantCulture), new Color(0.45f, 0.9f, 0.45f), delay + 0.05f, 1.15f);
                        break;
                    case CombatLogKind.Save:
                        if (target == null) target = actor;
                        if (target != null) Float(target, e.Text.IndexOf("success", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Saved" : "Failed save", new Color(0.8f, 0.75f, 0.95f), delay, 0.9f);
                        break;
                    case CombatLogKind.ConditionApplied:
                        if (target == null) target = actor;
                        if (target != null) Float(target, ConditionWord(e.Text), new Color(0.95f, 0.75f, 0.4f), delay + 0.1f, 0.9f);
                        break;
                    case CombatLogKind.Downed:
                    case CombatLogKind.Died:
                        if (target == null) target = actor;
                        AnimOf(e.TargetId.IsEmpty ? e.ActorId : e.TargetId)?.SetDead(true); // Phase 15: death holds; nothing replays over it
                        if (target != null) Float(target, e.Kind == CombatLogKind.Died ? "Dies" : "Down", new Color(0.9f, 0.3f, 0.3f), delay + 0.15f, 1.1f);
                        break;
                    case CombatLogKind.Stabilized:
                        if (target != null) Float(target, "Stable", new Color(0.6f, 0.85f, 0.95f), delay, 0.9f);
                        break;
                }
            }

            _fxSeen = last;
        }

        private readonly Dictionary<Transform, int> _floatStack = new Dictionary<Transform, int>();

        private void Float(Transform at, string text, Color colour, float delay, float scale)
        {
            CombatFeedback.Add(text);
            var k = _floatStack.TryGetValue(at, out var n) ? n : 0; // several words for one body this refresh: stacked, not on top of each other
            _floatStack[at] = k + 1;
            CombatFloat.Spawn(at.position + Vector3.up * (2.2f + 0.45f * k), text, colour, delay, scale);
        }

        private static string ConditionWord(string text)
        {
            foreach (var name in System.Enum.GetNames(typeof(ConditionType)))
                if (text.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0) return name;
            return "Affected";
        }

        /// <summary>A spell's colour from its damage words (data, not one script per spell).</summary>
        private static Color SpellColour(string text)
        {
            var t = text.ToLowerInvariant();
            if (t.Contains("fire")) return new Color(1f, 0.45f, 0.15f);
            if (t.Contains("cold") || t.Contains("frost")) return new Color(0.5f, 0.8f, 1f);
            if (t.Contains("lightning") || t.Contains("thunder")) return new Color(0.85f, 0.85f, 1f);
            if (t.Contains("radiant") || t.Contains("heal") || t.Contains("cure")) return new Color(1f, 0.95f, 0.6f);
            if (t.Contains("necrotic") || t.Contains("poison")) return new Color(0.5f, 0.9f, 0.4f);
            if (t.Contains("force") || t.Contains("magic missile")) return new Color(0.7f, 0.5f, 1f);
            return new Color(0.85f, 0.75f, 1f);
        }
    }

    /// <summary>A rising, fading word or number above a body (damage, healing, a miss).</summary>
    public sealed class CombatFloat : MonoBehaviour
    {
        private float _delay;
        private float _age;
        private TextMesh _text;
        private Color _colour;

        public static GameObject Spawn(Vector3 at, string text, Color colour, float delay, float scale)
        {
            var go = new GameObject("Combat feedback: " + text);
            go.transform.position = at;
            var mesh = ExplorationController.WorldText(go, text, colour, 0.03f * scale);
            var f = go.AddComponent<CombatFloat>();
            f._delay = delay;
            f._text = mesh;
            f._colour = colour;
            go.AddComponent<FaceCamera>();
            mesh.color = new Color(colour.r, colour.g, colour.b, delay > 0f ? 0f : 1f);
            return go;
        }

        private void Update()
        {
            if (_delay > 0f) { _delay -= Time.deltaTime; return; }
            _age += Time.deltaTime;
            transform.position += Vector3.up * (0.7f * Time.deltaTime);
            _text.color = new Color(_colour.r, _colour.g, _colour.b, Mathf.Clamp01(1.4f - _age));
            if (_age > 1.4f) Destroy(gameObject);
        }
    }

    /// <summary>A small projectile (an arrow, a bolt, a spell's mote) flying from the attacker to the target, then gone.</summary>
    public sealed class CombatProjectile : MonoBehaviour
    {
        private Vector3 _from;
        private Vector3 _to;
        private float _time;
        private float _duration;

        public static GameObject Launch(Vector3 from, Vector3 to, Material material, float size, float duration)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Combat projectile";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * Mathf.Max(0.06f, size);
            go.GetComponent<Renderer>().sharedMaterial = material;
            var p = go.AddComponent<CombatProjectile>();
            p._from = from;
            p._to = to;
            p._duration = duration;
            go.transform.position = from;
            return go;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / _duration);
            transform.position = Vector3.Lerp(_from, _to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>A body's lunge towards what it strikes, and a target's flinch when hit (restrained: a few centimetres).</summary>
    public sealed class CombatLunge : MonoBehaviour
    {
        private Vector3 _home;
        private Vector3 _offset;
        private float _time;
        private float _delay;
        private float _duration;
        private bool _shake;

        public static void Play(Transform body, Vector3 towards, float distance)
        {
            if (body == null) return;
            var dir = towards - body.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Start(body, dir.normalized * distance, 0f, 0.3f, false);
        }

        public static void Flinch(Transform body, float delay)
        {
            if (body == null) return;
            Start(body, new Vector3(0.06f, 0f, 0.06f), delay, 0.25f, true);
        }

        private static void Start(Transform body, Vector3 offset, float delay, float duration, bool shake)
        {
            var l = body.GetComponent<CombatLunge>();
            if (l != null) { body.localPosition = l._home; Destroy(l); }
            l = body.gameObject.AddComponent<CombatLunge>();
            l._home = body.localPosition;
            l._offset = body.parent != null ? body.parent.InverseTransformVector(offset) : offset;
            l._delay = delay;
            l._duration = duration;
            l._shake = shake;
        }

        private void Update()
        {
            if (_delay > 0f) { _delay -= Time.deltaTime; return; }
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / _duration);
            var k = _shake ? Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) : Mathf.Sin(t * Mathf.PI);
            transform.localPosition = _home + _offset * k;
            if (t >= 1f) { transform.localPosition = _home; Destroy(this); }
        }
    }
}
