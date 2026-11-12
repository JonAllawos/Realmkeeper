using System.Collections.Generic;
using System.IO;
using System.Linq;
using Realmkeeper.Exploration.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Realmkeeper.Editor
{
    /// <summary>
    /// Phase 15: builds the shared character animation set-up from the installed CC0 clips, so every animated body uses
    /// the same controllers. Run from the menu or in batch mode (<c>-executeMethod Realmkeeper.Editor.CharacterAnimationBuilder.Build</c>):
    /// <list type="number">
    /// <item>marks the looping KayKit clips (idle, walk, run, block, aim) as loops in their importer settings;</item>
    /// <item>writes <c>Art/Characters/Combatant.controller</c>: locomotion by speed, one-shot attack (one-handed,
    /// two-handed, unarmed, ranged, bow), cast, block, hit, interact and jump states, and a death that holds;</item>
    /// <item>writes <c>Art/Characters/SkeletonCombatant.overrideController</c>: the same with the skeletons' own idle,
    /// walk and death;</item>
    /// <item>adds Jump and Interact to <c>Art/Characters/Locomotion.controller</c> (exploration);</item>
    /// <item>fills the art catalog's creatures, props and controllers;</item>
    /// <item>when the git-ignored Quaternius Bestiary is installed on this machine, writes its local catalog and
    /// materials inside that folder (never referenced from the repository).</item>
    /// </list>
    /// Idempotent: running it again rebuilds the same assets.
    /// </summary>
    public static class CharacterAnimationBuilder
    {
        private const string KayKit = "Assets/Realmkeeper/Art/ThirdParty/KayKit/";
        private const string Anims = KayKit + "CharacterAnimations/Rig_Medium/";
        private const string Characters = "Assets/Realmkeeper/Art/Characters/";
        private const string Bestiary = "Assets/Realmkeeper/Art/ThirdParty/QuaterniusBestiary/";
        private const string Ual = "Assets/Realmkeeper/Art/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";

        private static readonly HashSet<string> Loops = new HashSet<string>
        {
            "Idle_A", "Idle_B", "Walking_A", "Walking_B", "Walking_C", "Running_A", "Running_B", "Skeletons_Idle", "Skeletons_Walking",
            "Melee_Blocking", "Melee_Unarmed_Idle", "Melee_2H_Idle", "Ranged_Bow_Idle", "Ranged_Bow_Aiming_Idle", "Jump_Idle",
        };

        [MenuItem("Realmkeeper/Build Character Animation")]
        public static void Build()
        {
            SetLoops();
            var clips = LoadClips();
            var combatant = BuildCombatant(clips);
            var skeleton = BuildSkeletonOverride(combatant, clips);
            var ual = AssetDatabase.LoadAllAssetsAtPath(Ual).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            var person = BuildOverride(combatant, "PersonCombatant", ual, new Dictionary<string, string>
            {
                // The Quaternius bodies (the player and people) use the library made for their rig where it has the motion.
                ["Idle_A"] = "Idle_Loop", ["Walking_A"] = "Walk_Loop", ["Running_A"] = "Jog_Fwd_Loop", ["Melee_1H_Attack_Chop"] = "Sword_Attack",
                ["Melee_Unarmed_Attack_Punch_A"] = "Punch_Jab", ["Ranged_1H_Shoot"] = "Pistol_Shoot", ["Ranged_Magic_Spellcasting"] = "Spell_Simple_Shoot",
                ["Hit_A"] = "Hit_Chest", ["Death_A"] = "Death01", ["Interact"] = "Interact", ["Jump_Full_Short"] = "Jump_Start",
            });
            ExtendLocomotion(ual);
            FillCatalog(combatant, skeleton, person);
            BuildLocalBestiary();
            AssetDatabase.SaveAssets();
            Debug.Log("Character animation built.");
        }

        private static void SetLoops()
        {
            foreach (var path in Directory.GetFiles(Anims, "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                var settings = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                var changed = false;
                foreach (var s in settings)
                {
                    var loop = Loops.Contains(s.name);
                    if (s.loopTime != loop) { s.loopTime = loop; changed = true; }
                }

                if (!changed && importer.clipAnimations.Length > 0) continue;
                importer.clipAnimations = settings;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            var map = new Dictionary<string, AnimationClip>();
            foreach (var path in Directory.GetFiles(Anims, "*.fbx").Select(p => p.Replace('\\', '/')))
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    if (!c.name.StartsWith("__preview__")) map[c.name] = c;
            return map;
        }

        private static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name)
        {
            if (!clips.TryGetValue(name, out var c)) throw new System.InvalidOperationException("Missing KayKit clip " + name);
            return c;
        }

        private static AnimatorController BuildCombatant(Dictionary<string, AnimationClip> clips)
        {
            var path = Characters + "Combatant.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Talking", AnimatorControllerParameterType.Bool); // read by the people's driver; unused here
            ac.AddParameter("Activity", AnimatorControllerParameterType.Int);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Attack1H", "Attack2H", "AttackUnarmed", "AttackRanged", "AttackBow", "Cast", "Block", "Hit", "Interact", "Jump" })
                ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ac);
            tree.AddChild(Clip(clips, "Idle_A"), 0f);
            tree.AddChild(Clip(clips, "Walking_A"), 1.5f);
            tree.AddChild(Clip(clips, "Running_A"), 4.5f);
            var loco = sm.AddState("Locomotion");
            loco.motion = tree;
            loco.tag = "Locomotion";
            sm.defaultState = loco;

            // Death first: it outranks everything and holds until the body is alive again.
            var death = sm.AddState("Death");
            death.motion = Clip(clips, "Death_A");
            death.tag = "Death";
            var toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            toDeath.canTransitionToSelf = false;
            toDeath.duration = 0.1f;
            var up = death.AddTransition(loco);
            up.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            up.duration = 0.25f;

            OneShot(sm, loco, "Attack1H", Clip(clips, "Melee_1H_Attack_Chop"), "Attack");
            OneShot(sm, loco, "Attack2H", Clip(clips, "Melee_2H_Attack_Chop"), "Attack");
            OneShot(sm, loco, "AttackUnarmed", Clip(clips, "Melee_Unarmed_Attack_Punch_A"), "Attack");
            OneShot(sm, loco, "AttackRanged", Clip(clips, "Ranged_1H_Shoot"), "Attack");
            OneShot(sm, loco, "AttackBow", Clip(clips, "Ranged_Bow_Release"), "Attack");
            OneShot(sm, loco, "Cast", Clip(clips, "Ranged_Magic_Spellcasting"), "Cast");
            OneShot(sm, loco, "Block", Clip(clips, "Melee_Block"), "Block");
            OneShot(sm, loco, "Hit", Clip(clips, "Hit_A"), "Hit");
            OneShot(sm, loco, "Interact", Clip(clips, "Interact"), "Interact");
            OneShot(sm, loco, "Jump", Clip(clips, "Jump_Full_Short"), "Jump");
            EditorUtility.SetDirty(ac);
            return ac;
        }

        /// <summary>A one-shot state entered on its trigger (never while dead) that returns to locomotion when done.</summary>
        private static void OneShot(AnimatorStateMachine sm, AnimatorState back, string trigger, AnimationClip clip, string tag)
        {
            var s = sm.AddState(trigger);
            s.motion = clip;
            s.tag = tag;
            var into = sm.AddAnyStateTransition(s);
            into.AddCondition(AnimatorConditionMode.If, 0, trigger);
            into.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            into.duration = 0.08f;
            into.canTransitionToSelf = true;
            var home = s.AddTransition(back);
            home.hasExitTime = true;
            home.exitTime = 0.9f;
            home.duration = 0.15f;
        }

        private static AnimatorOverrideController BuildSkeletonOverride(AnimatorController baseController, Dictionary<string, AnimationClip> clips)
        {
            var path = Characters + "SkeletonCombatant.overrideController";
            AssetDatabase.DeleteAsset(path);
            var oc = new AnimatorOverrideController(baseController) { name = "SkeletonCombatant" };
            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            oc.GetOverrides(list);
            for (var i = 0; i < list.Count; i++)
            {
                var name = list[i].Key.name;
                var replacement = name == "Idle_A" ? Clip(clips, "Skeletons_Idle") : name == "Walking_A" || name == "Running_A" ? Clip(clips, "Skeletons_Walking") : name == "Death_A" ? Clip(clips, "Skeletons_Death") : null;
                list[i] = new KeyValuePair<AnimationClip, AnimationClip>(list[i].Key, replacement);
            }

            oc.ApplyOverrides(list);
            AssetDatabase.CreateAsset(oc, path);
            return oc;
        }

        private static AnimatorOverrideController BuildOverride(AnimatorController baseController, string name, Dictionary<string, AnimationClip> source, Dictionary<string, string> map)
        {
            var path = Characters + name + ".overrideController";
            AssetDatabase.DeleteAsset(path);
            var oc = new AnimatorOverrideController(baseController) { name = name };
            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            oc.GetOverrides(list);
            for (var i = 0; i < list.Count; i++)
            {
                AnimationClip replacement = null;
                if (map.TryGetValue(list[i].Key.name, out var other) && !source.TryGetValue(other, out replacement)) throw new System.InvalidOperationException("Missing clip " + other);
                list[i] = new KeyValuePair<AnimationClip, AnimationClip>(list[i].Key, replacement);
            }

            oc.ApplyOverrides(list);
            AssetDatabase.CreateAsset(oc, path);
            return oc;
        }

        private static void ExtendLocomotion(Dictionary<string, AnimationClip> clips)
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Characters + "Locomotion.controller");
            if (ac == null) return;
            var sm = ac.layers[0].stateMachine;
            var loco = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion");
            if (loco == null) return;
            foreach (var (trigger, clip) in new[] { ("Jump", "Jump_Start"), ("Interact", "Interact") }) // the library made for these bodies
            {
                if (!ac.parameters.Any(p => p.name == trigger)) ac.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var existing = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == trigger);
                if (existing != null) sm.RemoveState(existing);
                var s = sm.AddState(trigger);
                s.motion = Clip(clips, clip);
                s.tag = trigger;
                var into = sm.AddAnyStateTransition(s);
                into.AddCondition(AnimatorConditionMode.If, 0, trigger);
                into.duration = 0.08f;
                var home = s.AddTransition(loco);
                home.hasExitTime = true;
                home.exitTime = 0.9f;
                home.duration = 0.15f;
            }

            EditorUtility.SetDirty(ac);
        }

        private static void FillCatalog(RuntimeAnimatorController combatant, RuntimeAnimatorController skeleton, RuntimeAnimatorController person)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/Realmkeeper/Art/Resources/RealmkeeperArtCatalog.asset");
            if (catalog == null) throw new System.InvalidOperationException("The art catalog is missing: run ArtSetup first.");
            catalog.Combatant = combatant;
            catalog.SkeletonCombatant = skeleton;
            catalog.PersonCombatant = person;
            catalog.Creatures = Entries(KayKit + "Skeletons/Characters/");
            catalog.Props = Entries(KayKit + "Skeletons/Accessories/");
            EditorUtility.SetDirty(catalog);
        }

        private static List<ArtCatalog.PrefabEntry> Entries(string folder) =>
            Directory.GetFiles(folder, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(p => new ArtCatalog.PrefabEntry { Id = Path.GetFileNameWithoutExtension(p), Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p) })
                .Where(e => e.Prefab != null).ToList();

        /// <summary>The local-only models: a catalog and materials inside the git-ignored folder, only when it exists.</summary>
        private static void BuildLocalBestiary()
        {
            if (!AssetDatabase.IsValidFolder(Bestiary.TrimEnd('/'))) return;
            var puglin = AssetDatabase.LoadAssetAtPath<GameObject>(Bestiary + "Models/Puglin.fbx");
            if (puglin == null) return;
            if (!AssetDatabase.IsValidFolder(Bestiary + "Materials")) AssetDatabase.CreateFolder(Bestiary.TrimEnd('/'), "Materials");
            if (!AssetDatabase.IsValidFolder(Bestiary + "Resources")) AssetDatabase.CreateFolder(Bestiary.TrimEnd('/'), "Resources");
            var material = Material("Puglin");
            var catalogPath = Bestiary + "Resources/" + LocalCreatureModels.ResourceName + ".asset";
            AssetDatabase.DeleteAsset(catalogPath);
            var local = ScriptableObject.CreateInstance<LocalCreatureModels>();
            // The Puglin is a small armed dungeon raider: it stands for goblins (small, armed raiders). The Imp is not used:
            // no imp exists among the creatures.
            local.Entries.Add(new LocalCreatureModels.Entry { CreatureId = "goblin", Prefab = puglin, Material = material, Scale = 1f });
            AssetDatabase.CreateAsset(local, catalogPath);
        }

        private static Material Material(string name)
        {
            var path = Bestiary + "Materials/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Bestiary + "Textures/T_" + name + "_BaseColor_1.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Bestiary + "Textures/T_" + name + "_Normal.png");
            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
