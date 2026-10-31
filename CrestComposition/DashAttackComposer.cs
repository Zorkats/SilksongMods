// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Needleforge.Data;
using Silksong.FsmUtil;
using UnityEngine;

namespace CrestComposition
{
    // Clones each source's dash attack into the custom moveset, gives every source its own copy
    // of the branch it runs in the Sprint FSM, and switches between them live.
    public sealed class DashAttackComposer
    {
        private sealed class DashClones
        {
            public GameObject? Stab;
            public GameObject? StabAlt;
            public int Steps;
            public readonly List<GameObject> RouteObjects = new List<GameObject>();
        }

        private const string WandererComboEntryState = "To Wanderer Combo";

        private static readonly string[] _stopStates = new[]
        {
            "Regain Control Normal",
            "Regain Control To Idle",
            "Continue Sprint?",
            "Continue Sprint? 2"
        };

        private readonly CrestComposerContext _context;
        private readonly Dictionary<string, DashClones> _clonesBySource =
            new Dictionary<string, DashClones>(StringComparer.Ordinal);
        private readonly List<GameObject> _slotClones = new List<GameObject>();

        private string _activeDashAttack;
        private bool _wandererComboReady;

        public string ActiveDashAttack
        {
            get
            {
                return _activeDashAttack;
            }
        }

        public bool LastSelfCheckPassed { get; private set; }

        private string WandererComboEvent
        {
            get
            {
                return $"{_context.CrestData.name} WANDERER COMBO";
            }
        }

        internal DashAttackComposer(CrestComposerContext context, string initialDashAttack)
        {
            _context = context;
            _activeDashAttack = initialDashAttack;
        }

        internal void InitializeOnHeroAwake()
        {
            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _context.CrestData.Moveset.ConfigGroup;
            HeroConfigNeedleforge? heroConfig = _context.CrestData.Moveset.HeroConfig;
            if (hc == null || customGroup == null || customGroup.ActiveRoot == null || heroConfig == null)
            {
                _context.Logger?.LogError(
                    $"[CrestComposition] Cannot initialize dash clones for '{_context.CrestData.name}': " +
                    "HeroController, ConfigGroup, ActiveRoot, or HeroConfig is null.");
                return;
            }

            _clonesBySource.Clear();
            _slotClones.Clear();
            _wandererComboReady = false;

            CrestComposerContext.DestroyDefaultAttack(customGroup.DashStab);
            CrestComposerContext.DestroyDefaultAttack(customGroup.DashStabAlt);
            customGroup.DashStab = null;
            customGroup.DashStabAlt = null;

            foreach (AttackSource source in _context.Sources)
            {
                CloneSourceAttacks(hc, source);
            }

            if (!_clonesBySource.ContainsKey(_activeDashAttack))
            {
                _context.Logger?.LogWarning(
                    $"[CrestComposition] Dash source '{_activeDashAttack}' is not available; using Hunter.");
                _activeDashAttack = VanillaCrestType.Hunter.ToString();
            }

            ApplyDashAttack(_activeDashAttack);
            heroConfig.DashSlashFsmEdit = _context.GuardFsmEdit("dash", BuildDashSlashFsm);
        }

        public void ApplyDashAttack(string sourceId)
        {
            // Clones exist once the hero has loaded. Before that, InitializeOnHeroAwake checks the id.
            if (_clonesBySource.Count > 0 && !_clonesBySource.ContainsKey(sourceId))
            {
                _context.Logger?.LogWarning(
                    $"[CrestComposition] Dash source '{sourceId}' is not available; keeping '{_activeDashAttack}'.");
                return;
            }

            _activeDashAttack = sourceId;

            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _context.CrestData.Moveset.ConfigGroup;
            HeroConfigNeedleforge? heroConfig = _context.CrestData.Moveset.HeroConfig;
            if (hc == null ||
                customGroup == null ||
                heroConfig == null ||
                !_clonesBySource.TryGetValue(sourceId, out DashClones? clones))
            {
                return;
            }

            AttackSource? source = _context.FindSource(sourceId);
            if (source == null)
            {
                return;
            }

            // Shared Sprint states read the dash object from the group, so it must always
            // name the selection, not just the object the copied branch uses.
            customGroup.DashStab = clones.Stab;
            customGroup.DashStabAlt = clones.StabAlt;

            List<GameObject?> selected = new List<GameObject?> { clones.Stab, clones.StabAlt };
            selected.AddRange(clones.RouteObjects);
            _context.ActivateSelection(_slotClones, selected.ToArray());

            HeroControllerConfig? sourceConfig = source.FindConfigGroup(hc)?.Config;
            if (sourceConfig != null)
            {
                heroConfig.SetDashStabFields(
                    time: sourceConfig.DashStabTime,
                    speed: sourceConfig.DashStabSpeed,
                    bounceJumpSpeed: sourceConfig.DashStabBounceJumpSpeed,
                    forceShortBounce: sourceConfig.ForceShortDashStabBounce);
            }

            heroConfig.dashStabSteps = clones.Steps;

            _context.SetSlotClips(
                AttackSlot.Dash,
                CrestComposerContext.CollectClips(hc, source, GetRoute(source).ClipFilter));

            if (hc.didStart)
            {
                // Refreshing fires HC CONFIG UPDATED, which cancels a dash in flight
                // and rereads the dash object and timing into the Sprint FSM.
                _context.RefreshConfigGroup(hc);
                _context.Logger?.LogInfo(
                    $"[CrestComposition] Applied dash attack '{sourceId}' to '{_context.CrestData.name}'.");
            }
            else
            {
                _context.RebuildAnimations();
            }
        }

        // HeroController.Attack keys this follow-up to the Wanderer crest, so a composed crest sends
        // its own event at the same point in that method. Returns whether the swing became the combo.
        internal bool TryStartWandererCombo(HeroController hc)
        {
            if (!_wandererComboReady ||
                !string.Equals(_activeDashAttack, VanillaCrestType.Wanderer.ToString(), StringComparison.Ordinal) ||
                hc.wandererDashComboWindowTimer <= 0f ||
                !_context.CrestData.IsEquipped)
            {
                return false;
            }

            hc.sprintFSM.SendEvent(WandererComboEvent);
            return true;
        }

        private void CloneSourceAttacks(HeroController hc, AttackSource source)
        {
            if (!source.HasPlayableDashAttack)
            {
                _context.Logger?.LogInfo(
                    $"[CrestComposition] '{source.Id}' has no playable dash attack to borrow.");
                return;
            }

            HeroController.ConfigGroup? sourceGroup = source.FindConfigGroup(hc);
            if (sourceGroup == null)
            {
                _context.Logger?.LogWarning(
                    $"[CrestComposition] Could not find a ConfigGroup for dash source '{source.Id}'.");
                return;
            }

            DashClones clones = new DashClones
            {
                Stab = CloneSlot(hc, source, sourceGroup.DashStab, "DashStab"),
                StabAlt = CloneSlot(hc, source, sourceGroup.DashStabAlt, "DashStabAlt")
            };

            // Needleforge crests keep one child per step and ignore the config's step count.
            clones.Steps = source.IsVanilla
                ? sourceGroup.Config.DashStabSteps
                : Mathf.Max(1, sourceGroup.DashStab != null ? sourceGroup.DashStab.transform.childCount : 1);

            foreach (FsmAttackObjectBinding binding in GetRoute(source).Bindings)
            {
                GameObject? routeObject = _context.CloneBinding(hc, source.Id, binding);
                if (routeObject != null)
                {
                    clones.RouteObjects.Add(routeObject);
                    AddSlotClone(routeObject);
                }
            }

            _clonesBySource[source.Id] = clones;
        }

        private GameObject? CloneSlot(HeroController hc, AttackSource source, GameObject? original, string slotName)
        {
            if (original == null)
            {
                return null;
            }

            GameObject clone = _context.CloneOnce(hc, original, $"{source.Id}_{slotName}");
            AddSlotClone(clone);
            return clone;
        }

        private void AddSlotClone(GameObject clone)
        {
            if (!_slotClones.Contains(clone))
            {
                _slotClones.Add(clone);
            }
        }

        private void BuildDashSlashFsm(PlayMakerFSM fsm, FsmState startState, out FsmState[] endStates)
        {
            // Needleforge adds FINISHED -> Regain Control Normal here, the way out if no route matches.
            endStates = new[] { startState };

            HeroController hc = HeroController.instance;
            string crestName = _context.CrestData.name;
            List<FsmState> allCopiedStates = new List<FsmState>();

            foreach (AttackSource source in _context.Sources)
            {
                if (!_clonesBySource.TryGetValue(source.Id, out DashClones? clones))
                {
                    continue;
                }

                DashAttackRoute route = GetRoute(source);
                Dictionary<string, FsmGameObject> variableRemap =
                    _context.BuildVariableRemap(fsm, hc, source.Id, route.Bindings);
                variableRemap["Dash Stab"] = _context.CreateBranchVariable(fsm, source.Id, "Dash Stab", clones.Stab);
                variableRemap["Dash Stab Alt"] = _context.CreateBranchVariable(fsm, source.Id, "Dash Stab Alt", clones.StabAlt);

                // The shared Hunter and Witch branch tests for Witch, Cursed and Cloakless along
                // the way, so only the branch's own crest may stay true.
                CopiedFsmBranch? copiedBranch = FsmBranchCopier.CopyBranch(
                    fsm,
                    route.EntryState,
                    $"{crestName}_{source.Id}",
                    _stopStates,
                    variableRemap,
                    _context.CrestData.ToolCrest,
                    crest => IsSourceCrest(source, crest));

                if (copiedBranch == null)
                {
                    _context.Logger?.LogError(
                        $"[CrestComposition] Failed to copy Sprint branch '{route.EntryState}' for '{source.Id}'.");
                    continue;
                }

                if (!source.IsVanilla)
                {
                    AddNeedleforgeStepLookups(fsm, copiedBranch, clones);
                }

                allCopiedStates.AddRange(copiedBranch.CopiedStates);
                startState.AddTransition(GetDispatchEventName(source.Id), copiedBranch.EntryState.Name);

                if (source.VanillaType == VanillaCrestType.Wanderer)
                {
                    AddWandererCombo(fsm, source, variableRemap, allCopiedStates);
                }
            }

            FsmInt attackStep = fsm.GetIntVariable("Attack Step");
            FsmInt attackSteps = fsm.GetIntVariable("Attack Steps");
            FsmFloat attackSpeed = fsm.GetFloatVariable("Attack Speed");
            FsmFloat attackTime = fsm.GetFloatVariable("Attack Time");
            // This edit is only ever invoked through the config it was assigned to.
            HeroConfigNeedleforge heroConfig = _context.CrestData.Moveset.HeroConfig!;
            startState.AddMethod(() =>
            {
                // Start Attack would have set these after the point where Needleforge branches off.
                attackStep.Value = 1;
                attackSteps.Value = heroConfig.dashStabSteps;
                attackSpeed.Value = heroConfig.DashStabSpeed;
                attackTime.Value = heroConfig.DashStabTime;
                fsm.Fsm.Event(GetDispatchEventName(_activeDashAttack));
            });

            LastSelfCheckPassed = FsmBranchSelfCheck.Verify(
                crestName,
                allCopiedStates,
                _context.AllClones,
                _context.GetSourceAttackRoots(hc),
                _context.CrestData.ToolCrest,
                _context.Logger);
        }

        // Vanilla reaches this branch through a global transition, so a copy needs its own.
        private void AddWandererCombo(
            PlayMakerFSM fsm,
            AttackSource source,
            Dictionary<string, FsmGameObject> variableRemap,
            List<FsmState> allCopiedStates)
        {
            CopiedFsmBranch? comboBranch = FsmBranchCopier.CopyBranch(
                fsm,
                WandererComboEntryState,
                $"{_context.CrestData.name}_{source.Id}_Combo",
                _stopStates,
                variableRemap,
                _context.CrestData.ToolCrest,
                crest => IsSourceCrest(source, crest));

            if (comboBranch == null)
            {
                _context.Logger?.LogError(
                    $"[CrestComposition] Failed to copy Sprint branch '{WandererComboEntryState}'.");
                return;
            }

            allCopiedStates.AddRange(comboBranch.CopiedStates);
            fsm.AddGlobalTransition(WandererComboEvent, comboBranch.EntryState.Name);
            _wandererComboReady = true;
        }

        // A Needleforge dash names its steps freely and can carry its own sound on each one,
        // which is what the Needleforge lambdas left out of the copy looked up.
        private static void AddNeedleforgeStepLookups(PlayMakerFSM fsm, CopiedFsmBranch branch, DashClones clones)
        {
            FsmInt attackStep = fsm.GetIntVariable("Attack Step");
            FsmString attackChildName = fsm.GetStringVariable("Attack Child Name");
            FsmGameObject currentStab = fsm.GetGameObjectVariable("Dash Stab Crt");
            FsmObject clip = fsm.FsmVariables.FindFsmObject("Clip");

            if (branch.StateMap.TryGetValue("Set Attack Multiple", out FsmState? setAttackState))
            {
                setAttackState.InsertMethod(
                    () =>
                    {
                        Transform? parent = clones.Stab != null ? clones.Stab.transform : null;
                        int stepIndex = attackStep.Value - 1;
                        if (parent != null && stepIndex >= 0 && stepIndex < parent.childCount)
                        {
                            attackChildName.Value = parent.GetChild(stepIndex).name;
                        }
                    },
                    IndexOfAction<FindChild>(setAttackState));
            }

            if (branch.StateMap.TryGetValue("Attack Dash Start", out FsmState? dashStartState))
            {
                dashStartState.InsertMethod(
                    () =>
                    {
                        AudioSource? stepAudio = currentStab.Value != null
                            ? currentStab.Value.GetComponent<AudioSource>()
                            : null;
                        if (stepAudio != null && stepAudio.clip != null)
                        {
                            clip.Value = stepAudio.clip;
                        }
                    },
                    IndexOfAction<PlayAudioEvent>(dashStartState));
            }
        }

        private static int IndexOfAction<TAction>(FsmState state)
            where TAction : FsmStateAction
        {
            int index = Array.FindIndex(state.Actions, action => action is TAction);
            return index >= 0 ? index : state.Actions.Length;
        }

        private static bool IsSourceCrest(AttackSource source, ToolCrest crest)
        {
            if (!source.VanillaType.HasValue)
            {
                return false;
            }

            VanillaCrestInfo info = VanillaCrestCatalog.Get(source.VanillaType.Value);
            foreach (string crestName in info.AlternateToolCrestNames)
            {
                if (string.Equals(crest.name, crestName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static DashAttackRoute GetRoute(AttackSource source)
        {
            return source.VanillaType.HasValue
                ? VanillaCrestCatalog.GetDashAttackRoute(source.VanillaType.Value)
                : VanillaCrestCatalog.CrossModDashAttackRoute;
        }

        private static string GetDispatchEventName(string sourceId)
        {
            return $"TO_{sourceId}";
        }
    }
}
