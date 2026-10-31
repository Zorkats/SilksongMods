// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HutongGames.PlayMaker;
using Needleforge.Data;
using Silksong.FsmUtil;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace CrestComposition
{
    internal enum AttackSlot
    {
        Neutral,
        Up,
        Down,
        Dash,
        Bind
    }

    // State shared by the per-slot composers of one crest: the merged hero animation
    // override library, the attack clones keyed by their source object, and the
    // ConfigGroup refresh that makes a live slot switch take effect.
    internal sealed class CrestComposerContext
    {
        internal static readonly FieldInfo? ActivateOnSlashField =
            typeof(NailAttackBase).GetField(
                "activateOnSlash",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly List<tk2dSpriteAnimationClip> _baseOverrideClips = new List<tk2dSpriteAnimationClip>();
        private readonly Dictionary<AttackSlot, List<tk2dSpriteAnimationClip>> _slotClips =
            new Dictionary<AttackSlot, List<tk2dSpriteAnimationClip>>();
        private readonly Dictionary<GameObject, GameObject> _cloneBySource = new Dictionary<GameObject, GameObject>();
        private readonly Dictionary<GameObject, bool> _restActiveByClone = new Dictionary<GameObject, bool>();
        private readonly List<GameObject> _allClones = new List<GameObject>();

        private tk2dSpriteAnimation? _overrideAnimLibrary;
        private GameObject? _rageNormal;
        private GameObject? _rageAlternate;
        private GameObject? _rageWall;
        private GameObject? _rageUp;

        public CrestData CrestData { get; }
        public ManualLogSource? Logger { get; }

        // Everything this crest can borrow attacks from, resolved once per hero.
        public IReadOnlyList<AttackSource> Sources { get; private set; } = Array.Empty<AttackSource>();

        public IReadOnlyList<GameObject> AllClones
        {
            get
            {
                return _allClones;
            }
        }

        // The rage attacks of the slots that selected Warrior, or null when none did.
        public HeroController.ConfigGroup? RageAttacks { get; private set; }

        public CrestComposerContext(CrestData crestData, ManualLogSource? logger)
        {
            CrestData = crestData;
            Logger = logger;
        }

        public void BeginHeroAwake()
        {
            _cloneBySource.Clear();
            _restActiveByClone.Clear();
            _allClones.Clear();
            _rageNormal = null;
            _rageAlternate = null;
            _rageWall = null;
            _rageUp = null;
            RageAttacks = null;
            Sources = AttackSourceCatalog.GetAvailable(Logger);

            HeroController.ConfigGroup? customGroup = CrestData.Moveset.ConfigGroup;
            if (customGroup?.ActiveRoot != null)
            {
                // Clones must not wake before their references are rebound.
                customGroup.ActiveRoot.SetActive(false);
            }

            tk2dSpriteAnimation? currentLib = CrestData.Moveset.HeroConfig?.heroAnimOverrideLib;
            if (_baseOverrideClips.Count == 0 &&
                currentLib != null &&
                currentLib != _overrideAnimLibrary &&
                currentLib.clips != null)
            {
                AddValidClips(_baseOverrideClips, currentLib.clips);
            }
        }

        // Needleforge runs these edits inside HeroController.Start, so an exception would abort
        // the hero's setup and every later edit. The attack just ends right away instead.
        public HeroConfigNeedleforge.FsmEdit GuardFsmEdit(string slotName, HeroConfigNeedleforge.FsmEdit edit)
        {
            return (PlayMakerFSM fsm, FsmState startState, out FsmState[] endStates) =>
            {
                try
                {
                    edit(fsm, startState, out endStates);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(
                        $"[CrestComposition] Building the {slotName} FSM for '{CrestData.name}' failed: {ex}");
                    endStates = new[] { startState };
                }
            };
        }

        public AttackSource? FindSource(string sourceId)
        {
            foreach (AttackSource source in Sources)
            {
                if (string.Equals(source.Id, sourceId, StringComparison.Ordinal))
                {
                    return source;
                }
            }

            return null;
        }

        public void SetBaseOverrideClips(IEnumerable<tk2dSpriteAnimationClip>? baseClips)
        {
            _baseOverrideClips.Clear();
            if (baseClips != null)
            {
                AddValidClips(_baseOverrideClips, baseClips);
            }

            if (_overrideAnimLibrary != null)
            {
                RebuildAnimations();
            }
        }

        public void SetSlotClips(AttackSlot slot, List<tk2dSpriteAnimationClip> clips)
        {
            _slotClips[slot] = clips;
        }

        // Hunter contributes nothing: its clips are the hero's default library, so an empty
        // slot falls through to them without overriding what another slot selected.
        public static List<tk2dSpriteAnimationClip> CollectClips(
            HeroController hc,
            AttackSource source,
            Predicate<string> nameFilter)
        {
            List<tk2dSpriteAnimationClip> clips = new List<tk2dSpriteAnimationClip>();
            if (source.VanillaType == VanillaCrestType.Hunter)
            {
                return clips;
            }

            tk2dSpriteAnimation? sourceLib = source.FindConfigGroup(hc)?.Config?.heroAnimOverrideLib;
            if (sourceLib == null || sourceLib.clips == null)
            {
                return clips;
            }

            foreach (tk2dSpriteAnimationClip? clip in sourceLib.clips)
            {
                if (clip != null && !string.IsNullOrEmpty(clip.name) && nameFilter(clip.name))
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        public void RebuildAnimations()
        {
            HeroConfigNeedleforge? heroConfig = CrestData.Moveset.HeroConfig;
            if (heroConfig == null)
            {
                return;
            }

            EnsureOverrideAnimationLibrary();

            List<tk2dSpriteAnimationClip> mergedClips =
                new List<tk2dSpriteAnimationClip>(_baseOverrideClips);
            foreach (AttackSlot slot in new[] { AttackSlot.Neutral, AttackSlot.Up, AttackSlot.Down, AttackSlot.Dash, AttackSlot.Bind })
            {
                if (!_slotClips.TryGetValue(slot, out List<tk2dSpriteAnimationClip>? clips))
                {
                    continue;
                }

                foreach (tk2dSpriteAnimationClip clip in clips)
                {
                    int existingIndex = mergedClips.FindIndex(
                        c => c != null && string.Equals(c.name, clip.name, StringComparison.Ordinal));
                    if (existingIndex >= 0)
                    {
                        mergedClips[existingIndex] = clip;
                    }
                    else
                    {
                        mergedClips.Add(clip);
                    }
                }
            }

            _overrideAnimLibrary!.clips = mergedClips.ToArray();
            AnimationLibraryLookup.Refresh(_overrideAnimLibrary);
            heroConfig.heroAnimOverrideLib = _overrideAnimLibrary;
        }

        // Rebuilds the group's component references and pushes them into the hero if equipped.
        public void RefreshConfigGroup(HeroController hc)
        {
            HeroController.ConfigGroup? customGroup = CrestData.Moveset.ConfigGroup;
            HeroConfigNeedleforge? heroConfig = CrestData.Moveset.HeroConfig;
            if (customGroup == null || heroConfig == null)
            {
                return;
            }

            bool wasActive = customGroup.ActiveRoot != null && customGroup.ActiveRoot.activeSelf;
            customGroup.Setup();

            bool isEquipped = IsCustomCrestEquipped(hc);
            if (customGroup.ActiveRoot != null)
            {
                customGroup.ActiveRoot.SetActive(wasActive || isEquipped);
            }

            RebuildAnimations();

            if (isEquipped)
            {
                hc.crestConfig = heroConfig;
                hc.UpdateConfig();
                hc.AnimCtrl?.SetHeroControllerConfig(heroConfig);
            }
        }

        public void SetRageNeutralAttacks(GameObject? normal, GameObject? alternate, GameObject? wall)
        {
            _rageNormal = normal;
            _rageAlternate = alternate;
            _rageWall = wall;
            RebuildRageAttacks();
        }

        public void SetRageUpAttack(GameObject? up)
        {
            _rageUp = up;
            RebuildRageAttacks();
        }

        // Timing lives on the config, so the virtual getters are read: Warrior's report rage timing
        // whenever rage is on, whichever crest is equipped.
        public static void ApplyAttackTiming(HeroConfigNeedleforge heroConfig, HeroControllerConfig source)
        {
            heroConfig.SetAttackFields(
                time: source.AttackDuration,
                recovery: source.AttackRecoveryTime,
                cooldown: source.AttackCooldownTime,
                quickSpeedMult: source.QuickAttackSpeedMult,
                quickCooldown: source.QuickAttackCooldownTime);
        }

        public bool IsCustomCrestEquipped(HeroController hc)
        {
            if (CrestData.IsEquipped)
            {
                return true;
            }

            string? currentId = PlayerData.instance?.CurrentCrestID;
            if (string.Equals(currentId, CrestData.name, StringComparison.Ordinal))
            {
                return true;
            }

            return hc.CurrentConfigGroup != null &&
                   ReferenceEquals(hc.CurrentConfigGroup, CrestData.Moveset.ConfigGroup);
        }

        // Clones a vanilla attack object under the custom root, once per source, so objects
        // shared between slots or crests (the common taunt slash, Shaman's cast effect) stay shared.
        public GameObject CloneOnce(HeroController hc, GameObject source, string cloneName)
        {
            if (_cloneBySource.TryGetValue(source, out GameObject? existing) && existing != null)
            {
                return existing;
            }

            GameObject clone = UnityEngine.Object.Instantiate(
                source,
                CrestData.Moveset.ConfigGroup!.ActiveRoot.transform);
            clone.name = cloneName;
            clone.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            clone.transform.localScale = source.transform.localScale;

            _cloneBySource[source] = clone;
            _restActiveByClone[clone] = source.activeSelf;
            _allClones.Add(clone);

            BorrowedAttackEffects.CarryOver(source, clone, FindBorrowedRootTint(hc, source));
            RewireChildFsmsToHero(clone, hc.gameObject);
            RebindActivateOnSlashTargets(hc, clone);

            return clone;
        }

        // Resolves each binding to a clone and points a new crest-owned FSM variable at it, so a
        // copied branch can be remapped away from the vanilla variable of the same name.
        public Dictionary<string, FsmGameObject> BuildVariableRemap(
            PlayMakerFSM fsm,
            HeroController hc,
            string sourceId,
            IEnumerable<FsmAttackObjectBinding> bindings)
        {
            Dictionary<string, FsmGameObject> variableRemap =
                new Dictionary<string, FsmGameObject>(StringComparer.Ordinal);

            foreach (FsmAttackObjectBinding binding in bindings)
            {
                GameObject? target = CloneBinding(hc, sourceId, binding);
                if (target != null && binding.ChildPath != null)
                {
                    target = target.transform.Find(binding.ChildPath)?.gameObject;
                }

                if (target == null)
                {
                    Logger?.LogError(
                        $"[CrestComposition] Could not resolve '{binding.FsmVariableName}' for '{sourceId}'.");
                    continue;
                }

                variableRemap[binding.FsmVariableName] =
                    CreateBranchVariable(fsm, sourceId, binding.FsmVariableName, target);
            }

            return variableRemap;
        }

        public GameObject? CloneBinding(HeroController hc, string sourceId, FsmAttackObjectBinding binding)
        {
            GameObject? source = hc.transform.Find("Attacks")?.Find(binding.AttacksPath)?.gameObject;
            return source != null
                ? CloneOnce(hc, source, $"{sourceId}_{binding.AttacksPath.Replace('/', '_')}")
                : null;
        }

        public FsmGameObject CreateBranchVariable(
            PlayMakerFSM fsm,
            string sourceId,
            string variableName,
            GameObject? value)
        {
            FsmGameObject customVar = fsm.GetGameObjectVariable($"{CrestData.name}_{sourceId}_{variableName}");
            customVar.Value = value;
            return customVar;
        }

        // What a copied branch must not reach back into: every crest it could borrow from.
        public List<Transform> GetSourceAttackRoots(HeroController hc)
        {
            List<Transform> roots = VanillaCrestCatalog.GetAllVanillaAttackRoots(hc);
            foreach (AttackSource source in Sources)
            {
                GameObject? activeRoot = source.IsVanilla ? null : source.FindConfigGroup(hc)?.ActiveRoot;
                if (activeRoot != null)
                {
                    roots.Add(activeRoot.transform);
                }
            }

            return roots;
        }

        // Unselected clones stay inactive so idle slashes on one root do not all
        // answer FlippedSprite or run per-frame work.
        public void ActivateSelection(IEnumerable<GameObject> slotClones, params GameObject?[] selected)
        {
            foreach (GameObject clone in slotClones)
            {
                clone.SetActive(false);
            }

            foreach (GameObject? clone in selected)
            {
                if (clone != null && _restActiveByClone.TryGetValue(clone, out bool restActive))
                {
                    clone.SetActive(restActive);
                }
            }
        }

        // Detached first: HeroController.Start collects DamageEnemies from its children
        // and may run before the deferred Destroy takes effect.
        public static void DestroyDefaultAttack(GameObject? attack)
        {
            if (attack == null)
            {
                return;
            }

            attack.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(attack);
        }

        public static void RewireChildFsmsToHero(GameObject rootObject, GameObject heroObject)
        {
            PlayMakerFSM[] fsms = rootObject.GetComponentsInChildren<PlayMakerFSM>(true);
            foreach (PlayMakerFSM fsmComponent in fsms)
            {
                if (fsmComponent == null || fsmComponent.Fsm == null)
                {
                    continue;
                }

                FsmVariables variables = fsmComponent.FsmVariables;
                if (variables == null)
                {
                    continue;
                }

                FsmGameObject? heroVar = variables.FindFsmGameObject("Hero") ??
                                         variables.FindFsmGameObject("Player");
                if (heroVar != null)
                {
                    heroVar.Value = heroObject;
                }
            }
        }

        private TintRendererGroup? FindBorrowedRootTint(HeroController hc, GameObject source)
        {
            foreach (AttackSource attackSource in Sources)
            {
                GameObject? activeRoot = attackSource.IsVanilla ? null : attackSource.FindConfigGroup(hc)?.ActiveRoot;
                if (activeRoot != null && source.transform.IsChildOf(activeRoot.transform))
                {
                    return activeRoot.GetComponent<TintRendererGroup>();
                }
            }

            return null;
        }

        private void RebuildRageAttacks()
        {
            if (_rageNormal == null && _rageAlternate == null && _rageWall == null && _rageUp == null)
            {
                RageAttacks = null;
                return;
            }

            // Setup reads Config. SetConfigGroup only takes the attacks from an override group.
            RageAttacks = new HeroController.ConfigGroup
            {
                Config = CrestData.Moveset.HeroConfig,
                NormalSlashObject = _rageNormal,
                AlternateSlashObject = _rageAlternate,
                WallSlashObject = _rageWall,
                UpSlashObject = _rageUp
            };
            RageAttacks.Setup();
        }

        private void RebindActivateOnSlashTargets(HeroController hc, GameObject clone)
        {
            if (ActivateOnSlashField == null)
            {
                return;
            }

            foreach (NailAttackBase attack in clone.GetComponentsInChildren<NailAttackBase>(true))
            {
                if (!(ActivateOnSlashField.GetValue(attack) is GameObject[] targets))
                {
                    continue;
                }

                GameObject[] rebound = new GameObject[targets.Length];
                for (int i = 0; i < targets.Length; i++)
                {
                    GameObject target = targets[i];
                    if (target == null || target.transform.IsChildOf(clone.transform))
                    {
                        rebound[i] = target!;
                        continue;
                    }

                    GameObject targetClone = CloneOnce(hc, target, $"{clone.name}_{target.name}");
                    targetClone.SetActive(false);
                    _restActiveByClone[targetClone] = false;
                    rebound[i] = targetClone;
                }

                ActivateOnSlashField.SetValue(attack, rebound);
            }
        }

        private void EnsureOverrideAnimationLibrary()
        {
            if (_overrideAnimLibrary != null)
            {
                return;
            }

            GameObject holder = new GameObject($"{CrestData.name}_OverrideAnimations");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            _overrideAnimLibrary = holder.AddComponent<tk2dSpriteAnimation>();
            _overrideAnimLibrary.clips = Array.Empty<tk2dSpriteAnimationClip>();
        }

        private static void AddValidClips(
            List<tk2dSpriteAnimationClip> target,
            IEnumerable<tk2dSpriteAnimationClip> clips)
        {
            foreach (tk2dSpriteAnimationClip clip in clips)
            {
                if (clip != null && !string.IsNullOrEmpty(clip.name))
                {
                    target.Add(clip);
                }
            }
        }
    }
}
