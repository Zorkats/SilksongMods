// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using GlobalSettings;
using HutongGames.PlayMaker;
using Silksong.FsmUtil;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace CrestComposition
{
    // Gives the custom crest the outcome of a vanilla crest's bind. Each one sets the Bind FSM's
    // own per-bind flags, which Can Bind? recomputes every time, so the shared tail states take
    // the vanilla path without any vanilla state being edited or any crest being reported equipped.
    // Void Crest's bind is the one outcome taken from another mod, see VoidBindBridge.
    public sealed class BindOutcomeComposer
    {
        private const string BindSharedState = "Bind Shared";
        private const string QuickCraftStartState = "Quick Craft Start";
        private const string EndBindState = "End Bind";

        private readonly CrestComposerContext _context;

        private VanillaCrestType _activeOutcome;
        private VoidBindBridge? _voidBind;

        // Hunter while Void's bind is selected: Void adds no vanilla outcome on top of its own.
        public VanillaCrestType ActiveOutcome
        {
            get
            {
                return _activeOutcome;
            }
        }

        public string ActiveOutcomeId
        {
            get
            {
                return _voidBind != null ? VoidBindBridge.OutcomeId : _activeOutcome.ToString();
            }
        }

        internal bool IsVoidActive
        {
            get
            {
                return _voidBind != null;
            }
        }

        private string RageBindEvent
        {
            get
            {
                return $"{_context.CrestData.name} RAGE BIND";
            }
        }

        private string QuickCraftEvent
        {
            get
            {
                return $"{_context.CrestData.name} QUICK CRAFT";
            }
        }

        private string VoidSpecialEvent
        {
            get
            {
                return $"{_context.CrestData.name} VOID SPECIAL";
            }
        }

        internal BindOutcomeComposer(CrestComposerContext context, VanillaCrestType initialOutcome)
        {
            _context = context;
            _activeOutcome = initialOutcome;
            _context.CrestData.BindEvent = HandleBindStarted;
            _context.CrestData.BindCompleteEvent = HandleBindCompleted;
        }

        internal void InitializeOnHeroAwake()
        {
            ApplyOutcome(_activeOutcome, _voidBind);
        }

        // Needleforge has built the crest's bind state by the time this runs.
        internal void InitializeOnHeroStart(HeroController hc)
        {
            PlayMakerFSM? bind = hc.gameObject.GetFsmPreprocessed("Bind");
            FsmState? crestBindState = bind?.GetState($"{_context.CrestData.name} Bind");
            FsmState? whichCrestState = bind?.GetState("Which Crest?");
            if (bind == null || crestBindState == null || whichCrestState == null)
            {
                _context.Logger?.LogError(
                    $"[CrestComposition] Could not find the bind states for '{_context.CrestData.name}'.");
                return;
            }

            crestBindState.AddTransition(RageBindEvent, BindSharedState);
            whichCrestState.AddTransition(QuickCraftEvent, QuickCraftStartState);
            AddVoidSpecialBind(bind, whichCrestState);
            if (_voidBind != null)
            {
                SyncVoidButtonHint();
            }

            FsmBool wasUpPressed = bind.GetBoolVariable("Was Up Pressed");
            FsmBool isBinding = bind.GetBoolVariable("Is Binding");
            whichCrestState.InsertMethod(
                () =>
                {
                    if (_voidBind != null &&
                        _context.CrestData.IsEquipped &&
                        VoidBindBridge.IsDirectionPressed(hc, _voidBind.SpecialBind.Direction))
                    {
                        bind.Fsm.Event(VoidSpecialEvent);
                    }
                    else if (_activeOutcome == VanillaCrestType.Toolmaster &&
                        _context.CrestData.IsEquipped &&
                        wasUpPressed.Value &&
                        ToolItemManager.TryReplenishTools(false, ToolItemManager.ReplenishMethod.QuickCraft))
                    {
                        // Quick Craft? would have set this on the way to the state we jump to.
                        isBinding.Value = true;
                        bind.Fsm.Event(QuickCraftEvent);
                    }
                },
                0);
        }

        // The seven vanilla outcomes, plus Void's when it is installed. Call after all plugins have
        // run Awake, or Void will be missing.
        public List<string> GetAvailableOutcomes()
        {
            List<string> outcomes = new List<string>();
            foreach (VanillaCrestType crestType in VanillaCrestCatalog.All.Keys)
            {
                outcomes.Add(crestType.ToString());
            }

            if (VoidBindBridge.Resolve(_context.Logger) != null)
            {
                outcomes.Add(VoidBindBridge.OutcomeId);
            }

            return outcomes;
        }

        public void ApplyBindOutcome(VanillaCrestType crestType)
        {
            ApplyOutcome(crestType, null);
        }

        // A vanilla crest name, or "Void". Call after all plugins have run Awake.
        public void ApplyBindOutcome(string outcomeId)
        {
            VoidBindBridge? voidBind = string.Equals(outcomeId, VoidBindBridge.OutcomeId, StringComparison.Ordinal)
                ? VoidBindBridge.Resolve(_context.Logger)
                : null;
            if (voidBind != null)
            {
                ApplyOutcome(VanillaCrestType.Hunter, voidBind);
            }
            else if (Enum.TryParse(outcomeId, out VanillaCrestType crestType) && VanillaCrestCatalog.All.ContainsKey(crestType))
            {
                ApplyOutcome(crestType, null);
            }
            else
            {
                _context.Logger?.LogWarning(
                    $"[CrestComposition] Bind outcome '{outcomeId}' is not available; using Hunter.");
                ApplyOutcome(VanillaCrestType.Hunter, null);
            }
        }

        private void ApplyOutcome(VanillaCrestType crestType, VoidBindBridge? voidBind)
        {
            bool changed = _activeOutcome != crestType || _voidBind != voidBind;
            VoidBindBridge? previousVoidBind = _voidBind;
            _activeOutcome = crestType;
            _voidBind = voidBind;

            HeroController? hc = HeroController.instance;
            if (hc == null)
            {
                return;
            }

            if (voidBind != null || previousVoidBind != null)
            {
                SyncVoidButtonHint();
            }

            // With Void's own crest equipped, its spool is its own to manage.
            if (voidBind == null && previousVoidBind != null && _context.IsCustomCrestEquipped(hc))
            {
                previousVoidBind.SetSpoolVisible(false);
            }

            _context.SetSlotClips(
                AttackSlot.Bind,
                CrestComposerContext.CollectClips(hc, new AttackSource(crestType), GetClipFilter(crestType)));

            if (!hc.didStart)
            {
                _context.RebuildAnimations();
                return;
            }

            // A rage or reaper mode already running would otherwise outlive its bind.
            if (changed && _context.IsCustomCrestEquipped(hc))
            {
                hc.ResetAllCrestState();
            }

            _context.RefreshConfigGroup(hc);
            _context.Logger?.LogInfo(
                $"[CrestComposition] Applied bind outcome '{ActiveOutcomeId}' to '{_context.CrestData.name}'.");
        }

        // The reaper mode's nail hit reward is keyed to the Reaper crest in HealthManager.TakeDamage.
        internal void HandleEnemyDamaged(HealthManager enemy, HitInstance hit)
        {
            HeroController? hc = HeroController.instance;
            if (_activeOutcome != VanillaCrestType.Reaper ||
                hc == null ||
                !_context.CrestData.IsEquipped ||
                !hc.ReaperState.IsInReaperMode ||
                hit.SilkGeneration == HitSilkGeneration.None ||
                enemy.DoNotGiveSilk ||
                enemy.enemyType == HealthManager.EnemyTypes.Shade ||
                enemy.enemyType == HealthManager.EnemyTypes.Armoured ||
                !(hit.AttackType == AttackTypes.Nail || (hit.AttackType == AttackTypes.Heavy && hit.IsNailTag)))
            {
                return;
            }

            int orbCount = GetReaperOrbCount(hc, enemy);
            if (orbCount > 0)
            {
                SpawnReaperOrbs(enemy, hit, orbCount);
            }
        }

        // Needleforge only reads a crest's special bind for that crest's own states, and sending the
        // bind there would skip Quick Craft? for every other outcome, so this crest has its own state.
        private void AddVoidSpecialBind(PlayMakerFSM bind, FsmState whichCrestState)
        {
            VoidBindBridge? voidBind = VoidBindBridge.Resolve(_context.Logger);
            if (voidBind == null)
            {
                return;
            }

            FsmState specialState = bind.AddState($"{_context.CrestData.name} Void Special");
            specialState.AddLambdaMethod(voidBind.SpecialBind.lambdaMethod);
            specialState.AddTransition("FINISHED", EndBindState);
            whichCrestState.AddTransition(VoidSpecialEvent, specialState.Name);
        }

        // The inventory prompt for the special bind. Read from Void's crest, which sets it after load.
        private void SyncVoidButtonHint()
        {
            ToolCrest? toolCrest = _context.CrestData.ToolCrest;
            if (toolCrest == null)
            {
                return;
            }

            ToolCrest? voidCrest = _voidBind?.Crest.ToolCrest;
            toolCrest.hasCustomAction = voidCrest != null && voidCrest.hasCustomAction;
            toolCrest.customButtonCombo = voidCrest != null ? voidCrest.customButtonCombo : default;
        }

        private void HandleBindStarted(FsmInt healAmount, FsmInt bindAmount, FsmFloat bindTime, PlayMakerFSM fsm)
        {
            if (_voidBind != null)
            {
                _voidBind.StartBind(healAmount, bindAmount, bindTime, fsm);
                return;
            }

            switch (_activeOutcome)
            {
                case VanillaCrestType.Warrior:
                    DeactivateFsmObjects(fsm, "Warrior_Bind_antic_silk", "Bind Silk", "Pt Charge");
                    healAmount.Value = 0;
                    bindAmount.Value = 1;
                    bindTime.Value = 0.2f;
                    fsm.GetFloatVariable("Quick Bind Effect Delay").Value = 0f;
                    fsm.GetBoolVariable("Is Warrior Equipped").Value = true;
                    // The rage bind skips Quick Bind?, as Rage Bind does.
                    fsm.Fsm.Event(RageBindEvent);
                    break;

                case VanillaCrestType.Witch:
                    DeactivateFsmObjects(
                        fsm,
                        "Whip_Bind_silk_antic",
                        "Witch Bind",
                        "Witch Bind Maggot Cleanse",
                        "Bind Silk",
                        "Pt Charge");
                    healAmount.Value = Gameplay.MultibindTool.IsEquipped ? 4 : 3;
                    bindAmount.Value = 1;
                    bindTime.Value = 0.2f;
                    fsm.GetFloatVariable("Quick Bind Effect Delay").Value = 0f;
                    fsm.GetBoolVariable("Is Witch Equipped").Value = true;
                    break;

                case VanillaCrestType.Spell:
                    DeactivateFsmObjects(fsm, "Shaman_Bind_antic_silk");
                    SetFsmObjectActive(fsm, "Shaman_Bind_antic_silk", true);
                    fsm.GetBoolVariable("Is Shaman Equipped").Value = true;
                    // Set Bind Anims ran with the flag still off, so undo what it does for Dazzle.
                    fsm.GetFloatVariable("Quick Bind Effect Delay").Value = 0.3f;
                    fsm.GetStringVariable("Air BindCharge Anim").Value = "BindCharge Air";
                    fsm.GetStringVariable("Ground BindCharge Anim").Value = "BindCharge Ground";
                    ApplyMultibind(healAmount, bindAmount, bindTime);
                    break;

                default:
                    ApplyMultibind(healAmount, bindAmount, bindTime);
                    break;
            }
        }

        private void HandleBindCompleted()
        {
            HeroController? hc = HeroController.instance;
            if (hc == null)
            {
                return;
            }

            if (_activeOutcome == VanillaCrestType.Warrior)
            {
                StartRage(hc);
            }
            else if (_activeOutcome == VanillaCrestType.Reaper)
            {
                StartReaperMode(hc);
            }
        }

        // Same effect as the Warrior block in HeroController.BindCompleted.
        private static void StartRage(HeroController hc)
        {
            if (!hc.warriorState.IsInRageMode)
            {
                hc.AddFrost(-15f);
                StatusVignette.AddStatus(StatusVignette.StatusTypes.InRageMode);
            }

            hc.warriorState = default;
            hc.warriorState.IsInRageMode = true;
            hc.warriorState.RageEffectTimeLeft = Gameplay.WarriorRageDuration;
            hc.warriorState.RageHealTimeLeft = Gameplay.WarriorRageDuration + Gameplay.WarriorRageGraceTime;
            hc.RestartWarriorRageEffect();

            BrightnessEffect brightness = hc.gm.cameraCtrl.GetComponent<BrightnessEffect>();
            brightness.ExtraEffectFadeTo(1.15f, 1.15f, 0f, 0f);
            brightness.ExtraEffectFadeTo(1f, 1f, 2f, 0.3f);
            EventRegister.SendEvent(EventRegisterEvents.WarriorRageStarted);
        }

        // Same effect as the Reaper block in HeroController.BindCompleted.
        private static void StartReaperMode(HeroController hc)
        {
            hc.reaperState.IsInReaperMode = true;
            hc.reaperState.ReaperModeDurationLeft = Gameplay.ReaperModeDuration;
            if (hc.reaperModeEffect)
            {
                hc.reaperModeEffect.gameObject.SetActive(false);
                hc.reaperModeEffect.gameObject.SetActive(true);
            }
        }

        private static int GetReaperOrbCount(HeroController hc, HealthManager enemy)
        {
            switch (enemy.reaperBundles)
            {
                case HealthManager.ReaperBundleTiers.Normal:
                    return hc.GetReaperPayout();
                case HealthManager.ReaperBundleTiers.Reduced:
                    return 1;
                default:
                    return 0;
            }
        }

        private static void SpawnReaperOrbs(HealthManager enemy, HitInstance hit, int orbCount)
        {
            float degrees;
            float angleMin;
            float angleMax;
            if (enemy.flingSilkOrbsAimObject != null)
            {
                Vector3 toAimObject = enemy.flingSilkOrbsAimObject.transform.position - enemy.transform.position;
                degrees = Mathf.Atan2(toAimObject.y, toAimObject.x) * Mathf.Rad2Deg;
                angleMin = degrees - 45f;
                angleMax = degrees + 45f;
            }
            else if (enemy.flingSilkOrbsDown)
            {
                degrees = 270f;
                angleMin = 225f;
                angleMax = 315f;
            }
            else
            {
                int direction = DirectionUtils.GetCardinalDirection(
                    hit.GetActualDirection(enemy.transform, HitInstance.TargetType.Regular));
                switch (direction)
                {
                    case 2:
                        degrees = 180f;
                        angleMin = 125f;
                        angleMax = 225f;
                        break;
                    case 1:
                        degrees = 90f;
                        angleMin = 45f;
                        angleMax = 135f;
                        break;
                    case 3:
                        degrees = 270f;
                        angleMin = 225f;
                        angleMax = 315f;
                        break;
                    default:
                        degrees = 0f;
                        angleMin = 315f;
                        angleMax = 415f;
                        break;
                }
            }

            FlingUtils.SpawnAndFling(
                new FlingUtils.Config
                {
                    Prefab = Gameplay.ReaperBundlePrefab,
                    AmountMin = orbCount,
                    AmountMax = orbCount,
                    SpeedMin = 25f,
                    SpeedMax = 50f,
                    AngleMin = angleMin,
                    AngleMax = angleMax
                },
                enemy.transform,
                enemy.effectOrigin);

            SpawnReapHitEffect(enemy, degrees);
        }

        private static void SpawnReapHitEffect(HealthManager enemy, float degrees)
        {
            GameObject prefab = Effects.ReapHitEffectPrefab;
            if (!prefab)
            {
                return;
            }

            Vector2 scale;
            float rotation;
            switch (DirectionUtils.GetCardinalDirection(degrees))
            {
                case 1:
                    scale = new Vector2(1f, 1f);
                    rotation = 90f;
                    break;
                case 2:
                    scale = new Vector2(-1f, 1f);
                    rotation = 0f;
                    break;
                case 3:
                    scale = new Vector2(1f, -1f);
                    rotation = 90f;
                    break;
                default:
                    scale = new Vector2(1f, 1f);
                    rotation = 0f;
                    break;
            }

            GameObject effect = prefab.Spawn(enemy.transform.TransformPoint(enemy.effectOrigin));
            effect.transform.SetRotation2D(rotation);
            effect.transform.localScale = scale.ToVector3(effect.transform.localScale.z);
        }

        // Normal bind numbers are already set by Needleforge, which leaves Multibind to the crest.
        private static void ApplyMultibind(FsmInt healAmount, FsmInt bindAmount, FsmFloat bindTime)
        {
            if (Gameplay.MultibindTool.IsEquipped)
            {
                healAmount.Value = 2;
                bindAmount.Value = 2;
                bindTime.Value = 0.8f;
            }
        }

        private static void DeactivateFsmObjects(PlayMakerFSM fsm, params string[] variableNames)
        {
            foreach (string variableName in variableNames)
            {
                SetFsmObjectActive(fsm, variableName, false);
            }
        }

        private static void SetFsmObjectActive(PlayMakerFSM fsm, string variableName, bool active)
        {
            GameObject? target = fsm.FsmVariables.FindFsmGameObject(variableName)?.Value;
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static Predicate<string> GetClipFilter(VanillaCrestType crestType)
        {
            switch (crestType)
            {
                case VanillaCrestType.Warrior:
                    return clipName => clipName.StartsWith("Rage Bind", StringComparison.Ordinal);
                case VanillaCrestType.Spell:
                    return clipName => clipName.StartsWith("BindCharge", StringComparison.Ordinal);
                default:
                    return _ => false;
            }
        }
    }
}
