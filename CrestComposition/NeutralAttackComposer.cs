// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using Needleforge.Data;
using Silksong.FsmUtil;
using UnityEngine;

namespace CrestComposition
{
    // Clones each source's neutral, alternate, wall, taunt and charged attacks into the
    // custom moveset, routes the charged slash through the Nail Arts FSM (Silk Specials reads the
    // taunt from the ConfigGroup at runtime), and switches between them live.
    public sealed class NeutralAttackComposer
    {
        private sealed class CrestClones
        {
            public GameObject? Normal;
            public GameObject? Alternate;
            public GameObject? Wall;
            public GameObject? Taunt;
            public GameObject? Charge;
            public GameObject? RageNormal;
            public GameObject? RageAlternate;
            public GameObject? RageWall;
        }

        private static readonly string[] _stopStates = new[] { "Set Finished", "Regain Partial Control" };

        private static readonly HashSet<string> _clipNames =
            new HashSet<string>(VanillaCrestCatalog.NeutralAttackAnimationClips, StringComparer.Ordinal);

        private readonly CrestComposerContext _context;
        private readonly Dictionary<string, CrestClones> _clonesBySource =
            new Dictionary<string, CrestClones>(StringComparer.Ordinal);
        private readonly List<GameObject> _slotClones = new List<GameObject>();

        private string _activeNeutralAttack;

        public string ActiveNeutralAttack
        {
            get
            {
                return _activeNeutralAttack;
            }
        }

        public bool LastSelfCheckPassed { get; private set; }

        internal NeutralAttackComposer(CrestComposerContext context, string initialNeutralAttack)
        {
            _context = context;
            _activeNeutralAttack = initialNeutralAttack;
        }

        internal void InitializeOnHeroAwake()
        {
            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _context.CrestData.Moveset.ConfigGroup;
            HeroConfigNeedleforge? heroConfig = _context.CrestData.Moveset.HeroConfig;
            if (hc == null || customGroup == null || customGroup.ActiveRoot == null || heroConfig == null)
            {
                _context.Logger?.LogError(
                    $"[CrestComposition] Cannot initialize neutral-attack clones for '{_context.CrestData.name}': " +
                    "HeroController, ConfigGroup, ActiveRoot, or HeroConfig is null.");
                return;
            }

            _clonesBySource.Clear();
            _slotClones.Clear();

            CrestComposerContext.DestroyDefaultAttack(customGroup.NormalSlashObject);
            CrestComposerContext.DestroyDefaultAttack(customGroup.AlternateSlashObject);
            CrestComposerContext.DestroyDefaultAttack(customGroup.WallSlashObject);
            CrestComposerContext.DestroyDefaultAttack(customGroup.ChargeSlash);
            CrestComposerContext.DestroyDefaultAttack(customGroup.TauntSlash);
            customGroup.NormalSlashObject = null;
            customGroup.AlternateSlashObject = null;
            customGroup.WallSlashObject = null;
            customGroup.ChargeSlash = null;
            customGroup.TauntSlash = null;

            Transform? attacksRoot = hc.transform.Find("Attacks");
            foreach (AttackSource source in _context.Sources)
            {
                CloneSourceAttacks(hc, attacksRoot, source);
            }

            if (!_clonesBySource.ContainsKey(_activeNeutralAttack))
            {
                _context.Logger?.LogWarning(
                    $"[CrestComposition] Neutral-attack source '{_activeNeutralAttack}' is not available; using Hunter.");
                _activeNeutralAttack = VanillaCrestType.Hunter.ToString();
            }

            ApplyNeutralAttack(_activeNeutralAttack);
            heroConfig.ChargedSlashFsmEdit = _context.GuardFsmEdit("charged", BuildChargedSlashFsm);
        }

        public void ApplyNeutralAttack(string sourceId)
        {
            _activeNeutralAttack = sourceId;

            HeroController? hc = HeroController.instance;
            HeroController.ConfigGroup? customGroup = _context.CrestData.Moveset.ConfigGroup;
            HeroConfigNeedleforge? heroConfig = _context.CrestData.Moveset.HeroConfig;
            if (hc == null ||
                customGroup == null ||
                heroConfig == null ||
                !_clonesBySource.TryGetValue(sourceId, out CrestClones? clones))
            {
                return;
            }

            AttackSource? source = _context.FindSource(sourceId);
            if (source == null)
            {
                return;
            }

            // A slash in flight must not outlive the object it is playing on.
            if (hc.didStart && _context.IsCustomCrestEquipped(hc))
            {
                hc.CancelAttack();
            }

            customGroup.NormalSlashObject = clones.Normal;
            customGroup.AlternateSlashObject = clones.Alternate;
            customGroup.WallSlashObject = clones.Wall;
            customGroup.TauntSlash = clones.Taunt;
            customGroup.ChargeSlash = clones.Charge;

            // Setup() only assigns non-null objects, so a stale alt would survive a switch.
            customGroup.NormalSlash = null;
            customGroup.NormalSlashDamager = null;
            customGroup.AlternateSlash = null;
            customGroup.AlternateSlashDamager = null;
            customGroup.WallSlash = null;
            customGroup.WallSlashDamager = null;

            _context.ActivateSelection(
                _slotClones,
                clones.Normal,
                clones.Alternate,
                clones.Wall,
                clones.Taunt,
                clones.Charge,
                clones.RageNormal,
                clones.RageAlternate,
                clones.RageWall);
            _context.SetRageNeutralAttacks(clones.RageNormal, clones.RageAlternate, clones.RageWall);

            ApplyAttackConfig(heroConfig, source.FindConfigGroup(hc)?.Config);

            _context.SetSlotClips(
                AttackSlot.Neutral,
                CrestComposerContext.CollectClips(hc, source, _clipNames.Contains));

            if (hc.didStart)
            {
                _context.RefreshConfigGroup(hc);
                _context.Logger?.LogInfo(
                    $"[CrestComposition] Applied neutral attack '{sourceId}' to '{_context.CrestData.name}'.");
            }
            else
            {
                _context.RebuildAnimations();
            }
        }

        private void CloneSourceAttacks(HeroController hc, Transform? attacksRoot, AttackSource source)
        {
            HeroController.ConfigGroup? sourceGroup = source.FindConfigGroup(hc);
            if (sourceGroup == null)
            {
                _context.Logger?.LogWarning(
                    $"[CrestComposition] Could not find a ConfigGroup for neutral-attack source '{source.Id}'.");
                return;
            }

            ChargedSlashRoute route = GetChargedSlashRoute(source);
            GameObject? chargeSource = sourceGroup.ChargeSlash;
            if (route.ChargeSlashObjectName != null)
            {
                chargeSource = attacksRoot?.Find(route.ChargeSlashObjectName)?.gameObject;
            }

            HeroController.ConfigGroup? rageGroup = source.VanillaType == VanillaCrestType.Warrior
                ? VanillaCrestCatalog.FindWarriorRageGroup(hc)
                : null;

            _clonesBySource[source.Id] = new CrestClones
            {
                Normal = CloneSlot(hc, source.Id, sourceGroup.NormalSlashObject, "Slash"),
                Alternate = CloneSlot(hc, source.Id, sourceGroup.AlternateSlashObject, "AltSlash"),
                Wall = CloneSlot(hc, source.Id, sourceGroup.WallSlashObject, "WallSlash"),
                Taunt = CloneSlot(hc, source.Id, sourceGroup.TauntSlash, "TauntSlash"),
                Charge = CloneSlot(hc, source.Id, chargeSource, "ChargeSlash"),
                RageNormal = CloneSlot(hc, source.Id, rageGroup?.NormalSlashObject, "RageSlash"),
                RageAlternate = CloneSlot(hc, source.Id, rageGroup?.AlternateSlashObject, "RageAltSlash"),
                RageWall = CloneSlot(hc, source.Id, rageGroup?.WallSlashObject, "RageWallSlash")
            };

            foreach (FsmAttackObjectBinding binding in route.Bindings)
            {
                CloneSlot(hc, source.Id, attacksRoot?.Find(binding.AttacksPath)?.gameObject, binding.AttacksPath);
            }
        }

        private GameObject? CloneSlot(
            HeroController hc,
            string sourceId,
            GameObject? source,
            string slotName)
        {
            if (source == null)
            {
                return null;
            }

            GameObject clone = _context.CloneOnce(hc, source, $"{sourceId}_{slotName}");
            if (!_slotClones.Contains(clone))
            {
                _slotClones.Add(clone);
            }

            return clone;
        }

        // Attack timing here is only the starting value: each swing sets the timing of its own
        // direction, see CrestMovesetComposer.ApplyAttackTiming. The rest stays config-wide.
        private static void ApplyAttackConfig(HeroConfigNeedleforge heroConfig, HeroControllerConfig? source)
        {
            if (source == null)
            {
                return;
            }

            CrestComposerContext.ApplyAttackTiming(heroConfig, source);
            heroConfig.canTurnWhileSlashing = source.canTurnWhileSlashing;
            heroConfig.wallSlashSlowdown = source.wallSlashSlowdown;
            heroConfig.SetChargedSlashFields(
                chain: source.chargeSlashChain,
                lungeSpeed: source.chargeSlashLungeSpeed,
                lungeDeceleration: source.chargeSlashLungeDeceleration,
                recoils: source.chargeSlashRecoils);
        }

        private void BuildChargedSlashFsm(
            PlayMakerFSM fsm,
            FsmState startState,
            out FsmState[] endStates)
        {
            // Needleforge adds FINISHED -> Set Finished here, the way out if no route matches.
            endStates = new[] { startState };

            HeroController hc = HeroController.instance;
            string crestName = _context.CrestData.name;
            List<FsmState> allCopiedStates = new List<FsmState>();

            foreach (AttackSource source in _context.Sources)
            {
                ChargedSlashRoute route = GetChargedSlashRoute(source);
                string targetState = route.EntryState;

                if (route.CopiesBranch)
                {
                    Dictionary<string, FsmGameObject> variableRemap =
                        _context.BuildVariableRemap(fsm, hc, source.Id, route.Bindings);

                    CopiedFsmBranch? copiedBranch = FsmBranchCopier.CopyBranch(
                        fsm,
                        route.EntryState,
                        $"{crestName}_{source.Id}",
                        _stopStates,
                        variableRemap,
                        _context.CrestData.ToolCrest);

                    if (copiedBranch == null)
                    {
                        _context.Logger?.LogError(
                            $"[CrestComposition] Failed to copy Nail Arts branch '{route.EntryState}' for '{source.Id}'.");
                        continue;
                    }

                    allCopiedStates.AddRange(copiedBranch.CopiedStates);
                    targetState = copiedBranch.EntryState.Name;
                }

                startState.AddTransition(GetDispatchEventName(source.Id), targetState);
            }

            startState.AddMethod(() =>
            {
                fsm.Fsm.Event(GetDispatchEventName(_activeNeutralAttack));
            });

            LastSelfCheckPassed = FsmBranchSelfCheck.Verify(
                crestName,
                allCopiedStates,
                _context.AllClones,
                _context.GetSourceAttackRoots(hc),
                _context.CrestData.ToolCrest,
                _context.Logger);
        }

        private static ChargedSlashRoute GetChargedSlashRoute(AttackSource source)
        {
            return source.VanillaType.HasValue
                ? VanillaCrestCatalog.GetChargedSlashRoute(source.VanillaType.Value)
                : VanillaCrestCatalog.CrossModChargedSlashRoute;
        }

        private static string GetDispatchEventName(string sourceId)
        {
            return $"TO_{sourceId}";
        }
    }
}
