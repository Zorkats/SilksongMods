// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using GlobalEnums;
using Needleforge.Data;

namespace CrestComposition
{
    // Entry point for composing a Needleforge CrestData's moveset from other crests' attacks.
    // Neutral, up and dash attacks can come from any AttackSource; down attacks come from vanilla
    // crests only; bind outcomes come from vanilla crests or Void Crest.
    public sealed class CrestMovesetComposer
    {
        private static readonly List<CrestMovesetComposer> _composers = new List<CrestMovesetComposer>();

        private readonly CrestComposerContext _context;

        public CrestData CrestData { get; }
        public NeutralAttackComposer NeutralAttacks { get; }
        public UpAttackComposer UpAttacks { get; }
        public DownAttackComposer DownAttacks { get; }
        public DashAttackComposer DashAttacks { get; }
        public BindOutcomeComposer BindOutcome { get; }

        public bool LastSelfCheckPassed
        {
            get
            {
                return NeutralAttacks.LastSelfCheckPassed &&
                       DownAttacks.LastSelfCheckPassed &&
                       DashAttacks.LastSelfCheckPassed;
            }
        }

        public event Action? OnMovesetInitialized;

        public CrestMovesetComposer(
            CrestData crestData,
            VanillaCrestType initialNeutralAttack,
            VanillaCrestType initialUpAttack,
            VanillaCrestType initialDownAttack,
            ManualLogSource? logger = null)
        {
            CrestData = crestData ?? throw new ArgumentNullException(nameof(crestData));
            _context = new CrestComposerContext(crestData, logger);
            NeutralAttacks = new NeutralAttackComposer(_context, initialNeutralAttack.ToString());
            UpAttacks = new UpAttackComposer(_context, initialUpAttack.ToString());
            DownAttacks = new DownAttackComposer(_context, initialDownAttack);
            DashAttacks = new DashAttackComposer(_context, VanillaCrestType.Hunter.ToString());
            BindOutcome = new BindOutcomeComposer(_context, VanillaCrestType.Hunter);

            _composers.Add(this);
            CrestCompositionPatches.EnsureApplied(logger);
        }

        internal static bool IsComposed(CrestData crestData)
        {
            return _composers.Exists(composer => ReferenceEquals(composer.CrestData, crestData));
        }

        public void SetNeutralAttack(VanillaCrestType crestType)
        {
            SetNeutralAttack(crestType.ToString());
        }

        public void SetNeutralAttack(string sourceId)
        {
            NeutralAttacks.ApplyNeutralAttack(sourceId);
        }

        public void SetUpAttack(VanillaCrestType crestType)
        {
            SetUpAttack(crestType.ToString());
        }

        public void SetUpAttack(string sourceId)
        {
            UpAttacks.ApplyUpAttack(sourceId);
        }

        public void SetDownAttack(VanillaCrestType crestType)
        {
            DownAttacks.ApplyDownAttack(crestType);
        }

        public void SetDashAttack(VanillaCrestType crestType)
        {
            SetDashAttack(crestType.ToString());
        }

        public void SetDashAttack(string sourceId)
        {
            DashAttacks.ApplyDashAttack(sourceId);
        }

        public void SetBindOutcome(VanillaCrestType crestType)
        {
            BindOutcome.ApplyBindOutcome(crestType);
        }

        public void SetBindOutcome(string outcomeId)
        {
            BindOutcome.ApplyBindOutcome(outcomeId);
        }

        public void SetBaseOverrideClips(IEnumerable<tk2dSpriteAnimationClip>? baseClips)
        {
            _context.SetBaseOverrideClips(baseClips);
        }

        internal static void InitializeAllOnHeroAwake()
        {
            foreach (CrestMovesetComposer composer in _composers)
            {
                composer.RunGuarded("hero awake", composer.InitializeOnHeroAwake);
            }
        }

        internal static void InitializeAllOnHeroStart(HeroController hc)
        {
            foreach (CrestMovesetComposer composer in _composers)
            {
                composer.RunGuarded("hero start", () => composer.BindOutcome.InitializeOnHeroStart(hc));
            }
        }

        internal static void ApplyAttackTiming(HeroController hc, AttackDirection direction)
        {
            foreach (CrestMovesetComposer composer in _composers)
            {
                if (ReferenceEquals(hc.crestConfig, composer.CrestData.Moveset.HeroConfig))
                {
                    composer.RunGuarded("attack timing", () => composer.ApplyDirectionTiming(hc, direction));
                }
            }
        }

        // Layered the way the game layers Warrior's own rage group over its normal one.
        internal static void ApplyRageAttacks(HeroController hc)
        {
            if (!hc.WarriorState.IsInRageMode)
            {
                return;
            }

            foreach (CrestMovesetComposer composer in _composers)
            {
                HeroController.ConfigGroup? customGroup = composer.CrestData.Moveset.ConfigGroup;
                HeroController.ConfigGroup? rageAttacks = composer._context.RageAttacks;
                if (rageAttacks != null && customGroup != null && ReferenceEquals(hc.CurrentConfigGroup, customGroup))
                {
                    hc.SetConfigGroup(customGroup, rageAttacks);
                }
            }
        }

        // Same conditions as the getter, with the bind outcome standing in for the equipped crest.
        internal static bool HasWandererLuck(HeroController hc)
        {
            return !hc.cState.isMaggoted &&
                   hc.playerData.silk >= 9 &&
                   _composers.Exists(composer =>
                       composer.CrestData.IsEquipped &&
                       composer.BindOutcome.ActiveOutcome == VanillaCrestType.Wanderer);
        }

        internal static bool IsVoidBinding(HeroController? hc)
        {
            return hc != null &&
                   hc.cState.isBinding &&
                   _composers.Exists(composer => composer.BindOutcome.IsVoidActive && composer.CrestData.IsEquipped);
        }

        internal static bool TryStartWandererCombo(HeroController hc)
        {
            foreach (CrestMovesetComposer composer in _composers)
            {
                if (composer.DashAttacks.TryStartWandererCombo(hc))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void NotifyEnemyDamaged(HealthManager enemy, HitInstance hit)
        {
            foreach (CrestMovesetComposer composer in _composers)
            {
                composer.RunGuarded("enemy hit", () => composer.BindOutcome.HandleEnemyDamaged(enemy, hit));
            }
        }

        private void ApplyDirectionTiming(HeroController hc, AttackDirection direction)
        {
            HeroConfigNeedleforge? heroConfig = CrestData.Moveset.HeroConfig;
            HeroControllerConfig? source;
            switch (direction)
            {
                case AttackDirection.upward:
                    source = _context.FindSource(UpAttacks.ActiveUpAttack)?.FindConfigGroup(hc)?.Config;
                    break;
                case AttackDirection.downward:
                    source = VanillaCrestCatalog.FindVanillaConfigGroup(hc, DownAttacks.ActiveDownAttack)?.Config;
                    break;
                default:
                    source = _context.FindSource(NeutralAttacks.ActiveNeutralAttack)?.FindConfigGroup(hc)?.Config;
                    break;
            }

            if (heroConfig != null && source != null)
            {
                CrestComposerContext.ApplyAttackTiming(heroConfig, source);
            }
        }

        // Needleforge's Setup() already ran on the default attacks replaced here, so it runs again.
        private void InitializeOnHeroAwake()
        {
            OnMovesetInitialized?.Invoke();
            _context.BeginHeroAwake();
            NeutralAttacks.InitializeOnHeroAwake();
            UpAttacks.InitializeOnHeroAwake();
            DownAttacks.InitializeOnHeroAwake();
            DashAttacks.InitializeOnHeroAwake();
            BindOutcome.InitializeOnHeroAwake();
            CrestData.Moveset.ConfigGroup?.Setup();
        }

        // One crest failing to compose must not stop the hero or the other crests from loading.
        private void RunGuarded(string stage, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _context.Logger?.LogError(
                    $"[CrestComposition] Composing '{CrestData.name}' failed at {stage}: {ex}");
            }
        }
    }
}
