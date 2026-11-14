using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 24 (ADR-0114): a landmark in the country, built from the owned kits (stones and rocks for waystones and cairns,
    /// the props kit's fire pit, tripod, crates and barrels for camps, the village kit's walls for a ruined steading, a wagon on
    /// its side for a wreck, fence, banner and lantern for a watch post). The view holds no state: using it is a command.
    /// </summary>
    public sealed class LandmarkView : MonoBehaviour, IInteractable, IConditionalInteractable
    {
        public OverlandLandmark Landmark { get; private set; }
        public bool Searched { get; set; }
        public Action<LandmarkView> Handler { get; set; }

        public string Prompt
        {
            get
            {
                switch (Landmark.Kind)
                {
                    case LandmarkKind.Waystone: return "Read the waystone";
                    case LandmarkKind.Cairn: return "Climb the cairn and look out";
                    case LandmarkKind.TravellersCamp: return "Look round the travellers' camp (a safe place to rest)";
                    case LandmarkKind.WatchPost: return "Look at " + Landmark.Name.ToLowerInvariant();
                    default: return (Searched ? "Look over " : "Search ") + Landmark.Name.ToLowerInvariant();
                }
            }
        }

        public InteractionKind Kind => InteractionKind.StateChanging;
        public Vector3 InteractionPoint => transform.position + Vector3.up;
        public bool IsAvailable => true;

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            Handler?.Invoke(this);
            return new InteractionResponse(Landmark.Name, string.Empty, Kind);
        }

        public static LandmarkView Create(Transform parent, OverlandLandmark l, Realmkeeper.Domain.Geography.OverlandTerrain terrain, ArtCatalog catalog, Material stone, Material wood)
        {
            var go = new GameObject("Landmark — " + l.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(l.X, terrain.HeightAt(l.X, l.Z), l.Z);
            var hash = Math.Abs(l.Id.GetHashCode());
            go.transform.rotation = Quaternion.Euler(0f, hash % 360, 0f);
            var v = go.AddComponent<LandmarkView>();
            v.Landmark = l;
            var t = go.transform;
            switch (l.Kind)
            {
                case LandmarkKind.Waystone:
                    Box(t, new Vector3(0f, 0.9f, 0f), new Vector3(0.55f, 1.8f, 0.3f), stone);
                    Model(t, catalog, "Rock_Medium_1", new Vector3(0.5f, 0f, 0.2f), 0.6f);
                    Solid(go, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 1.8f, 0.4f));
                    break;
                case LandmarkKind.Cairn:
                    for (var i = 0; i < 5; i++)
                        Model(t, catalog, i % 2 == 0 ? "Rock_Medium_2" : "Rock_Medium_3", new Vector3((i % 3 - 1) * 0.5f, i * 0.45f, (i % 2) * 0.4f - 0.2f), 1.2f - i * 0.17f, i * 70f);
                    Solid(go, new Vector3(0f, 1f, 0f), new Vector3(1.8f, 2f, 1.8f));
                    break;
                case LandmarkKind.TravellersCamp:
                    Model(t, catalog, "FirePit", Vector3.zero, 1f);
                    Model(t, catalog, "CampfireTripod", Vector3.zero, 1f);
                    Model(t, catalog, "Crate_Wooden", new Vector3(2.2f, 0f, 1.2f), 1f, 20f);
                    Model(t, catalog, "Barrel", new Vector3(2.4f, 0f, -0.8f), 1f);
                    for (var i = 0; i < 4; i++) Model(t, catalog, "Rock_Medium_1", new Vector3(Mathf.Cos(i * 1.57f) * 3.2f, 0f, Mathf.Sin(i * 1.57f) * 3.2f), 0.45f, i * 40f);
                    break;
                case LandmarkKind.AbandonedCamp:
                    Model(t, catalog, "FirePit", Vector3.zero, 1f);
                    Model(t, catalog, "Crate_Wooden", new Vector3(1.6f, 0.3f, 0.6f), 1f, 35f, 25f);
                    Model(t, catalog, "Barrel", new Vector3(-1.4f, 0.4f, 0.8f), 1f, 10f, 90f);
                    Model(t, catalog, "Bucket_Wooden_1", new Vector3(0.6f, 0f, -1.4f), 1f, 0f, 70f);
                    break;
                case LandmarkKind.RuinedHomestead:
                    for (var i = 0; i < 3; i++)
                    {
                        var wall = Model(t, catalog, i == 1 ? "Wall_UnevenBrick_Window_Wide_Round" : "Wall_UnevenBrick_Straight", Vector3.zero, 1f) ?? Box(t, Vector3.zero, new Vector3(2f, 2.4f, 0.3f), stone);
                        wall.transform.localPosition = i == 0 ? new Vector3(-2f, 0f, 0f) : i == 1 ? new Vector3(0f, 0f, 2f) : new Vector3(2f, 0f, 0f);
                        wall.transform.localRotation = Quaternion.Euler(0f, i == 1 ? 0f : 90f, i == 2 ? 6f : 0f);
                    }

                    for (var i = 0; i < 4; i++) Model(t, catalog, "Rock_Medium_" + (1 + i % 3), new Vector3(-1f + i * 0.8f, 0f, -1.5f + (i % 2)), 0.55f, i * 50f);
                    Solid(go, new Vector3(0f, 1.2f, 2f), new Vector3(4f, 2.4f, 0.4f));
                    break;
                case LandmarkKind.WatchPost:
                    Model(t, catalog, "Prop_WoodenFence_Single", new Vector3(-1.5f, 0f, 0f), 1f);
                    Model(t, catalog, "Prop_WoodenFence_Single", new Vector3(1.5f, 0f, 0f), 1f);
                    Model(t, catalog, "PostLantern", new Vector3(0f, 0f, 1f), 1f);
                    (Model(t, catalog, "Banner_Vertical_1", new Vector3(0.8f, 0f, 1.2f), 1f) ?? Box(t, new Vector3(0.8f, 2f, 1.2f), new Vector3(0.1f, 4f, 0.1f), wood)).name = "Banner";
                    Model(t, catalog, "Crate_Wooden", new Vector3(-0.6f, 0f, 1.6f), 0.9f);
                    break;
                default: // a wreck
                    Model(t, catalog, "Prop_Wagon", new Vector3(0f, 0.6f, 0f), 1f, 0f, 75f);
                    Model(t, catalog, "Crate_Wooden", new Vector3(2.2f, 0f, 0.8f), 1f, 40f, 15f);
                    Model(t, catalog, "Barrel", new Vector3(-1.8f, 0.4f, -1.2f), 1f, 0f, 90f);
                    Model(t, catalog, "Crate_Wooden", new Vector3(1.2f, 0f, -1.8f), 0.8f, 70f);
                    Solid(go, new Vector3(0f, 0.8f, 0f), new Vector3(2.4f, 1.6f, 3.6f));
                    break;
            }

            return v;
        }

        private static GameObject Box(Transform parent, Vector3 local, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.localPosition = local;
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = m;
            return b;
        }

        /// <summary>A kit model (scenery or module), keeping the model's own export frame; its colliders removed.</summary>
        private static GameObject Model(Transform parent, ArtCatalog catalog, string id, Vector3 local, float scale, float yaw = 0f, float tilt = 0f)
        {
            var prefab = catalog?.SceneryModel(id) ?? catalog?.Module(id)?.Prefab;
            if (prefab == null) return null;
            var go = UnityEngine.Object.Instantiate(prefab, parent, false);
            go.name = id;
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, tilt) * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
            return go;
        }

        private static void Solid(GameObject go, Vector3 centre, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = centre;
            c.size = size;
        }
    }

    /// <summary>
    /// Phase 24 (ADR-0114): a travelling group of the simulation, seen on the road: its leader (the real person's looks), up to
    /// three companions, and a wagon for a caravan. It walks to where the simulation says the group is now; talking to it is a
    /// command. Pooled by the controller: bodies are made only for groups near the player.
    /// </summary>
    public sealed class TravellerGroupView : MonoBehaviour, IInteractable, IConditionalInteractable
    {
        private readonly List<Transform> _members = new List<Transform>();
        private Transform _wagon;
        private Vector3 _target;
        private float _yaw;
        private Realmkeeper.Domain.Geography.OverlandTerrain _terrain;

        public TravellerView Traveller { get; private set; }
        public bool Talked { get; set; }
        public Action<TravellerGroupView> Handler { get; set; }

        public string Prompt => Traveller.Hostile ? Traveller.Line
            : Talked && Traveller.Kind == TravelGroupKind.Refugees ? $"Give a ration to {Traveller.Name}"
            : $"Talk to {Traveller.Leader ?? Traveller.Name} ({Traveller.Line})";

        public InteractionKind Kind => InteractionKind.StateChanging;
        public Vector3 InteractionPoint => (_members.Count > 0 ? _members[0].position : transform.position) + Vector3.up;
        public bool IsAvailable => !Traveller.Hostile;

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            Handler?.Invoke(this);
            return new InteractionResponse(Traveller.Name, string.Empty, Kind);
        }

        public static TravellerGroupView Create(Transform parent, TravellerView t, Realmkeeper.Domain.Geography.OverlandTerrain terrain, ArtCatalog catalog, ExplorationMaterials materials)
        {
            var go = new GameObject("Travellers — " + t.GroupId);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TravellerGroupView>();
            v._terrain = terrain;
            v.Traveller = t;
            var role = t.Kind == TravelGroupKind.Caravan || t.Kind == TravelGroupKind.MerchantParty ? NpcRole.Merchant
                : t.Kind == TravelGroupKind.Patrol || t.Kind == TravelGroupKind.FactionPatrol || t.Kind == TravelGroupKind.Bandits ? NpcRole.Guard
                : t.Kind == TravelGroupKind.Refugees ? NpcRole.Labourer : NpcRole.Farmer;
            var count = Math.Min(4, Math.Max(1, t.Members));
            for (var i = 0; i < count; i++)
            {
                var id = i == 0 && !t.LeaderId.IsEmpty ? t.LeaderId : new NpcId(Guid.Parse(Hash32(t.GroupId + ":" + i)));
                var body = new GameObject(i == 0 ? "Leader" : "Companion " + i).transform;
                body.SetParent(go.transform, false);
                var look = NpcAppearanceResolver.Resolve(id, role, i == 0 ? t.Leader ?? t.Name : t.Name, null);
                var anim = HumanoidCharacterBuilder.Build(body, look, catalog);
                if (anim == null)
                {
                    var capsule = SettlementEnvironmentBuilder.Primitive(body, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.85f, 0.6f), materials.Get(new Color(0.45f, 0.38f, 0.30f)));
                    SettlementEnvironmentBuilder.RemoveCollider(capsule);
                }

                v._members.Add(body);
            }

            if (t.Kind == TravelGroupKind.Caravan || t.Kind == TravelGroupKind.MerchantParty)
            {
                var prefab = catalog?.SceneryModel("Prop_Wagon") ?? catalog?.Module("Prop_Wagon")?.Prefab;
                if (prefab != null)
                {
                    var w = Instantiate(prefab, go.transform, false);
                    w.transform.localRotation = prefab.transform.localRotation;
                    w.transform.localScale = prefab.transform.localScale;
                    foreach (var c in w.GetComponentsInChildren<Collider>()) Destroy(c);
                    v._wagon = w.transform;
                }
            }

            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.5f;
            v.Place(t, true);
            return v;
        }

        private static string Hash32(string s)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).ToString();
        }

        /// <summary>Where the simulation puts the group now; it walks there (or snaps on first sight).</summary>
        public void Place(TravellerView t, bool snap)
        {
            Traveller = t;
            _target = new Vector3(t.X, _terrain.HeightAt(t.X, t.Z), t.Z);
            _yaw = t.Yaw;
            if (snap) { transform.position = _target; transform.rotation = Quaternion.Euler(0f, _yaw, 0f); Arrange(); }
        }

        private void Update()
        {
            if (Traveller == null) return;
            var to = _target - transform.position;
            to.y = 0f;
            if (to.magnitude > 60f) { transform.position = _target; Arrange(); return; } // a jump in time: no running across the land
            var step = Mathf.Min(to.magnitude, 1.4f * Time.deltaTime);
            if (to.magnitude > 0.05f)
            {
                transform.position += to.normalized * step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 3f * Time.deltaTime);
            }

            Arrange();
        }

        /// <summary>Members in a short file along the road behind the leader, each on the ground; the wagon behind them.</summary>
        private void Arrange()
        {
            var back = -transform.forward;
            var side = transform.right;
            for (var i = 0; i < _members.Count; i++)
            {
                var p = transform.position + back * (i * 1.6f) + side * ((i % 2 == 0 ? 0.5f : -0.5f) * (i > 0 ? 1 : 0));
                p.y = _terrain.HeightAt(p.x, p.z);
                _members[i].position = p;
                _members[i].rotation = transform.rotation;
            }

            if (_wagon != null)
            {
                var p = transform.position + back * (_members.Count * 1.6f + 2.5f);
                p.y = _terrain.HeightAt(p.x, p.z);
                _wagon.position = p;
                _wagon.rotation = transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            }
        }
    }
}
