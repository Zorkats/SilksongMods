// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HutongGames.PlayMaker;
using Needleforge.Data;
using Silksong.FsmUtil;
using UnityEngine;

namespace CrestComposition
{
    // Clones vanilla down-attack hierarchies into a Needleforge CrestData moveset,
    // copies and rebinds the five custom down-attack branches in Crest Attacks (crest_attacks),
    // and supports live runtime switching across all seven vanilla down attacks.
    public sealed class DownAttackComposer
    {
        private static readonly string[] _stopStates = new[] { "End", "Idle" };

        private static readonly string[] _downAttackClipKeywords = new[]
        {
            "down",
            "spike",
            "spinball",
            "drill",
            "grind",
            "pogo",
            "bounce",
            "rebound"
        };

        private readonly CrestComposerContext _context;
        private readonly CrestData _crestData;
        private readonly ManualLogSource? _logger;
        private readonly Dictionary<VanillaCrestType, Dictionary<string, GameObject>> _clonedObjectsByCrest =
            new Dictionary<VanillaCrestType, Dictionary<string, GameObject>>();
        private readonly Dictionary<VanillaCrestType, GameObject?> _primaryDownSlashByCrest =
            new Dictionary<VanillaCrestType, GameObject?>();
        private readonly Dictionary<VanillaCrestType, GameObject?> _altDownSlashByCrest =
            new Dictionary<VanillaCrestType, GameObject?>();
        private readonly List<GameObject> _allClonedObjects = new List<GameObject>();

        private VanillaCrestType _activeDownAttack;
        private bool _fsmInitialized;

        public VanillaCrestType ActiveDownAttack
        {
            get
            {
                return _activeDownAttack;
            }
        }

        public bool LastSelfCheckPassed { get; private set; }

        internal DownAttackComposer(CrestComposerContext context, VanillaCrestType initialDownAttack)
        {
            _context = context;
            _crestData = context.CrestData;
            _logger = context.Logger;
            _activeDownAttack = initialDownAttack;
        }

        internal void InitializeOnHeroAwake()
        {
            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _crestData.Moveset.ConfigGroup;
            HeroConfigNeedleforge? heroConfig = _crestData.Moveset.HeroConfig;

            if (hc == null || customGroup == null || customGroup.ActiveRoot == null || heroConfig == null)
            {
                _logger?.LogError(
                    $"[CrestComposition] Cannot initialize down-attack clones for '{_crestData.name}': " +
                    "HeroController, ConfigGroup, ActiveRoot, or HeroConfig is null.");
                return;
            }

            _fsmInitialized = false;
            _clonedObjectsByCrest.Clear();
            _primaryDownSlashByCrest.Clear();
            _altDownSlashByCrest.Clear();
            _allClonedObjects.Clear();

            CrestComposerContext.DestroyDefaultAttack(customGroup.DownSlashObject);
            CrestComposerContext.DestroyDefaultAttack(customGroup.AltDownSlashObject);
            customGroup.DownSlashObject = null;
            customGroup.AltDownSlashObject = null;

            // Setup() only assigns non-null objects, so the defaults' components would survive.
            customGroup.Downspike = null;
            customGroup.AltDownspike = null;
            customGroup.DownSlash = null;
            customGroup.AltDownSlash = null;
            customGroup.DownSlashDamager = null;
            customGroup.AltDownSlashDamager = null;

            foreach (VanillaCrestInfo info in VanillaCrestCatalog.All.Values)
            {
                CloneCrestDownAttackChildren(hc, customGroup.ActiveRoot.transform, info);
            }

            RebindShamanSlashEffect();

            if (_primaryDownSlashByCrest.TryGetValue(_activeDownAttack, out GameObject? initialPrimary))
            {
                customGroup.DownSlashObject = initialPrimary;
            }

            if (_altDownSlashByCrest.TryGetValue(_activeDownAttack, out GameObject? initialAlt))
            {
                customGroup.AltDownSlashObject = initialAlt;
            }

            heroConfig.SetCustomDownslash(
                GetCustomDownSlashEventName(),
                _context.GuardFsmEdit("down", BuildDownSlashFsm));
        }

        public void ApplyDownAttack(VanillaCrestType crestType)
        {
            _activeDownAttack = crestType;

            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _crestData.Moveset.ConfigGroup;
            HeroConfigNeedleforge? heroConfig = _crestData.Moveset.HeroConfig;

            if (!_fsmInitialized || hc == null || customGroup == null || heroConfig == null)
            {
                return;
            }

            VanillaCrestInfo info = VanillaCrestCatalog.Get(crestType);
            HeroController.ConfigGroup? sourceGroup = VanillaCrestCatalog.FindVanillaConfigGroup(hc, crestType);

            switch (info.DownSlashType)
            {
                case HeroControllerConfig.DownSlashTypes.DownSpike:
                    heroConfig.downSlashType = HeroControllerConfig.DownSlashTypes.DownSpike;
                    heroConfig.downSlashEvent = string.Empty;
                    ApplyHunterDownspikeParameters(heroConfig, sourceGroup?.Config);
                    break;

                case HeroControllerConfig.DownSlashTypes.Slash:
                    heroConfig.downSlashType = HeroControllerConfig.DownSlashTypes.Slash;
                    heroConfig.downSlashEvent = string.Empty;
                    break;

                default:
                    heroConfig.downSlashType = HeroControllerConfig.DownSlashTypes.Custom;
                    heroConfig.downSlashEvent = GetCustomDownSlashEventName();
                    break;
            }

            _primaryDownSlashByCrest.TryGetValue(crestType, out GameObject? primaryClone);
            _altDownSlashByCrest.TryGetValue(crestType, out GameObject? altClone);

            customGroup.DownSlashObject = primaryClone;
            customGroup.AltDownSlashObject = altClone;
            customGroup.Downspike = null;
            customGroup.AltDownspike = null;
            customGroup.DownSlash = null;
            customGroup.AltDownSlash = null;
            customGroup.DownSlashDamager = null;
            customGroup.AltDownSlashDamager = null;

            _context.SetSlotClips(AttackSlot.Down, CollectDownAttackClips(hc, crestType));
            _context.RefreshConfigGroup(hc);

            _logger?.LogInfo(
                $"[CrestComposition] Applied down attack '{crestType}' ({info.DownSlashType}) to '{_crestData.name}'.");
        }

        private void CloneCrestDownAttackChildren(
            HeroController hc,
            Transform customActiveRoot,
            VanillaCrestInfo info)
        {
            HeroController.ConfigGroup? vanillaGroup =
                VanillaCrestCatalog.FindVanillaConfigGroup(hc, info.CrestType);

            if (vanillaGroup == null || vanillaGroup.ActiveRoot == null)
            {
                _logger?.LogWarning(
                    $"[CrestComposition] Could not find vanilla ConfigGroup for '{info.CrestType}' " +
                    $"(expected ActiveRoot '{info.ActiveRootName}').");
                return;
            }

            Dictionary<string, GameObject> childMap =
                new Dictionary<string, GameObject>(StringComparer.Ordinal);
            _clonedObjectsByCrest[info.CrestType] = childMap;
            _primaryDownSlashByCrest[info.CrestType] = null;
            _altDownSlashByCrest[info.CrestType] = null;

            foreach (DownAttackChildBinding binding in info.DownAttackChildren)
            {
                Transform? sourceChild = vanillaGroup.ActiveRoot.transform.Find(binding.ChildName);
                if (sourceChild == null)
                {
                    _logger?.LogWarning(
                        $"[CrestComposition] Child '{binding.ChildName}' not found under vanilla root '{vanillaGroup.ActiveRoot.name}'.");
                    continue;
                }

                GameObject clone = UnityEngine.Object.Instantiate(sourceChild.gameObject, customActiveRoot);
                clone.name = $"{info.CrestType}_{binding.ChildName}";
                clone.transform.SetLocalPositionAndRotation(
                    sourceChild.localPosition,
                    sourceChild.localRotation);
                clone.transform.localScale = sourceChild.localScale;
                clone.SetActive(sourceChild.gameObject.activeSelf);

                CrestComposerContext.RewireChildFsmsToHero(clone, hc.gameObject);

                if (binding.IsPrimaryDownSlash)
                {
                    EnsureHeroDownAttackComponent(clone);
                    _primaryDownSlashByCrest[info.CrestType] = clone;
                }

                if (binding.IsAltDownSlash)
                {
                    EnsureHeroDownAttackComponent(clone);
                    _altDownSlashByCrest[info.CrestType] = clone;
                }

                childMap[binding.ChildName] = clone;
                _allClonedObjects.Add(clone);
            }
        }

        private void RebindShamanSlashEffect()
        {
            if (CrestComposerContext.ActivateOnSlashField == null)
            {
                return;
            }

            if (!_clonedObjectsByCrest.TryGetValue(
                    VanillaCrestType.Spell,
                    out Dictionary<string, GameObject>? shamanChildren))
            {
                return;
            }

            if (!shamanChildren.TryGetValue("DownSlash", out GameObject? clonedDownSlash) ||
                !shamanChildren.TryGetValue("Shaman_blade_cast_effect DownSlash", out GameObject? clonedEffect))
            {
                return;
            }

            NailAttackBase? nailAttack = clonedDownSlash.GetComponent<NailAttackBase>();
            if (nailAttack == null)
            {
                return;
            }

            CrestComposerContext.ActivateOnSlashField.SetValue(nailAttack, new[] { clonedEffect });
        }

        private void BuildDownSlashFsm(
            PlayMakerFSM fsm,
            FsmState startState,
            out FsmState[] endStates)
        {
            endStates = Array.Empty<FsmState>();
            List<FsmState> allCopiedStates = new List<FsmState>();

            foreach (VanillaCrestInfo info in VanillaCrestCatalog.All.Values)
            {
                if (info.DownSlashType != HeroControllerConfig.DownSlashTypes.Custom ||
                    string.IsNullOrEmpty(info.CrestAttacksEntryState))
                {
                    continue;
                }

                if (!_clonedObjectsByCrest.TryGetValue(
                        info.CrestType,
                        out Dictionary<string, GameObject>? childMap))
                {
                    continue;
                }

                Dictionary<string, FsmGameObject> variableRemap =
                    new Dictionary<string, FsmGameObject>(StringComparer.Ordinal);

                foreach (DownAttackChildBinding binding in info.DownAttackChildren)
                {
                    if (string.IsNullOrEmpty(binding.FsmVariableName))
                    {
                        continue;
                    }

                    if (!childMap.TryGetValue(binding.ChildName, out GameObject? clonedChild))
                    {
                        continue;
                    }

                    string customVarName =
                        $"{_crestData.name}_{info.CrestType}_{binding.FsmVariableName}";
                    FsmGameObject customVar = fsm.GetGameObjectVariable(customVarName);
                    customVar.Value = clonedChild;
                    variableRemap[binding.FsmVariableName!] = customVar;
                }

                string statePrefix = $"{_crestData.name}_{info.CrestType}";
                CopiedFsmBranch? copiedBranch = FsmBranchCopier.CopyBranch(
                    fsm,
                    info.CrestAttacksEntryState!,
                    statePrefix,
                    _stopStates,
                    variableRemap,
                    _crestData.ToolCrest);

                if (copiedBranch == null)
                {
                    _logger?.LogError(
                        $"[CrestComposition] Failed to copy Crest Attacks branch '{info.CrestAttacksEntryState}' for '{info.CrestType}'.");
                    continue;
                }

                allCopiedStates.AddRange(copiedBranch.CopiedStates);

                string dispatchEvent = GetDispatchEventName(info.CrestType);
                startState.AddTransition(dispatchEvent, copiedBranch.EntryState.Name);
            }

            startState.AddTransition("FINISHED", "End");
            startState.AddMethod(() =>
            {
                string dispatchEvent = GetDispatchEventName(_activeDownAttack);
                fsm.Fsm.Event(dispatchEvent);
            });

            RegisterWitchCancelCleanup(fsm);

            HeroController? hc = HeroController.instance;
            List<Transform> vanillaRoots = hc != null
                ? VanillaCrestCatalog.GetAllVanillaActiveRoots(hc)
                : new List<Transform>();

            LastSelfCheckPassed = FsmBranchSelfCheck.Verify(
                _crestData.name,
                allCopiedStates,
                _allClonedObjects,
                vanillaRoots,
                _crestData.ToolCrest,
                _logger);

            _fsmInitialized = true;
            ApplyDownAttack(_activeDownAttack);
        }

        private void RegisterWitchCancelCleanup(PlayMakerFSM fsm)
        {
            if (!_clonedObjectsByCrest.TryGetValue(
                    VanillaCrestType.Witch,
                    out Dictionary<string, GameObject>? witchChildren))
            {
                return;
            }

            if (!witchChildren.TryGetValue("Followup Slash", out GameObject? clonedFollowup) ||
                clonedFollowup == null)
            {
                return;
            }

            FsmState? cancelState = fsm.GetState("Cancel");
            if (cancelState == null)
            {
                return;
            }

            cancelState.AddMethod(() =>
            {
                if (clonedFollowup != null && clonedFollowup.activeSelf)
                {
                    clonedFollowup.SetActive(false);
                }
            });
        }

        private static void ApplyHunterDownspikeParameters(
            HeroConfigNeedleforge targetConfig,
            HeroControllerConfig? hunterConfig)
        {
            if (hunterConfig == null)
            {
                return;
            }

            float speed = hunterConfig.downspikeSpeed;
            targetConfig.SetDownspikeFields(
                anticTime: hunterConfig.downspikeAnticTime,
                time: hunterConfig.downspikeTime,
                recoveryTime: hunterConfig.downspikeRecoveryTime,
                doesThrust: hunterConfig.downspikeThrusts,
                speed: speed,
                velocity: new Vector2(-speed, -speed),
                acceleration: Vector2.zero,
                doesBurstEffect: hunterConfig.downspikeBurstEffect);
        }

        private static List<tk2dSpriteAnimationClip> CollectDownAttackClips(
            HeroController hc,
            VanillaCrestType crestType)
        {
            HashSet<string> explicitClipNames = new HashSet<string>(
                VanillaCrestCatalog.Get(crestType).DownAttackAnimationClips,
                StringComparer.OrdinalIgnoreCase);

            return CrestComposerContext.CollectClips(
                hc,
                new AttackSource(crestType),
                clipName => explicitClipNames.Contains(clipName) ||
                            MatchesAnyKeyword(clipName, _downAttackClipKeywords));
        }

        private static bool MatchesAnyKeyword(string clipName, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (clipName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureHeroDownAttackComponent(GameObject attackObject)
        {
            if (attackObject == null)
            {
                return;
            }

            if (attackObject.GetComponent<NailAttackBase>() != null &&
                attackObject.GetComponent<HeroDownAttack>() == null)
            {
                attackObject.AddComponent<HeroDownAttack>();
            }
        }

        private string GetCustomDownSlashEventName()
        {
            return $"{_crestData.name} DOWNSLASH";
        }

        private static string GetDispatchEventName(VanillaCrestType crestType)
        {
            return $"TO_{crestType}";
        }
    }
}
