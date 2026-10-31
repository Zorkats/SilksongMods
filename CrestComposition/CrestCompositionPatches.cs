// Copyright (c) Zorkats. All rights reserved.

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;

namespace CrestComposition
{
    // Runs composition after Needleforge's hero patches have built every crest, so other mods'
    // attacks exist by the time they are cloned.
    [HarmonyPatch]
    internal static class CrestCompositionPatches
    {
        private const string HarmonyId = "com.zorkats.silksong.crestcomposition";
        private const string NeedleforgeHarmonyId = "io.github.needleforge";

        private static Harmony? _harmony;
        private static ManualLogSource? _logger;

        internal static void EnsureApplied(ManualLogSource? logger)
        {
            if (_harmony == null)
            {
                _logger = logger;
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(CrestCompositionPatches));
            }
        }

        // Patched by hand: Void's assembly is optional, and loads after this one.
        internal static void PatchVoidFocusGate(MethodInfo focusGate)
        {
            _harmony?.Patch(
                focusGate,
                postfix: new HarmonyMethod(typeof(CrestCompositionPatches), nameof(AfterVoidFocusEnabled)));
        }

        private static void AfterVoidFocusEnabled(ref bool __result)
        {
            __result = __result || CrestMovesetComposer.IsVoidBinding(HeroController.instance);
        }

        [HarmonyPatch(typeof(HeroController), "Awake")]
        [HarmonyPostfix]
        [HarmonyAfter(NeedleforgeHarmonyId)]
        private static void AfterHeroAwake()
        {
            CrestMovesetComposer.InitializeAllOnHeroAwake();
        }

        [HarmonyPatch(typeof(HeroController), "Start")]
        [HarmonyPrefix]
        [HarmonyAfter(NeedleforgeHarmonyId)]
        private static void BeforeHeroStart(HeroController __instance)
        {
            CrestMovesetComposer.InitializeAllOnHeroStart(__instance);
        }

        [HarmonyPatch(typeof(HeroController), "Attack", typeof(AttackDirection))]
        [HarmonyPrefix]
        private static void BeforeAttack(HeroController __instance, AttackDirection attackDir)
        {
            CrestMovesetComposer.ApplyAttackTiming(__instance, attackDir);
        }

        // Adds, just before the game's own Wanderer combo check:
        // if (CrestMovesetComposer.TryStartWandererCombo(this)) return;
        [HarmonyPatch(typeof(HeroController), "Attack", typeof(AttackDirection))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> AddWandererComboCheck(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            FieldInfo comboTimer = AccessTools.Field(typeof(HeroController), "wandererDashComboWindowTimer");
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int timerRead = code.FindIndex(instruction => instruction.LoadsField(comboTimer));
            if (timerRead < 1 || code[timerRead - 1].opcode != OpCodes.Ldarg_0)
            {
                _logger?.LogWarning(
                    "[CrestComposition] HeroController.Attack has changed; the Wanderer combo will not start.");
                return code;
            }

            CodeInstruction originalCheck = code[timerRead - 1];
            Label continueAttack = generator.DefineLabel();
            CodeInstruction loadHero = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(originalCheck);
            originalCheck.labels.Add(continueAttack);
            code.InsertRange(
                timerRead - 1,
                new[]
                {
                    loadHero,
                    new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(CrestMovesetComposer), nameof(CrestMovesetComposer.TryStartWandererCombo))),
                    new CodeInstruction(OpCodes.Brfalse, continueAttack),
                    new CodeInstruction(OpCodes.Ret)
                });
            return code;
        }

        [HarmonyPatch(typeof(HeroController), "UpdateConfig")]
        [HarmonyPostfix]
        private static void AfterUpdateConfig(HeroController __instance)
        {
            CrestMovesetComposer.ApplyRageAttacks(__instance);
        }

        [HarmonyPatch(typeof(HeroController), nameof(HeroController.IsWandererLucky), MethodType.Getter)]
        [HarmonyPostfix]
        private static void AfterIsWandererLucky(HeroController __instance, ref bool __result)
        {
            __result = __result || CrestMovesetComposer.HasWandererLuck(__instance);
        }

        [HarmonyPatch(typeof(HealthManager), "TakeDamage", typeof(HitInstance))]
        [HarmonyPrefix]
        private static void BeforeTakeDamage(HealthManager __instance, out int __state)
        {
            __state = GetDamageTarget(__instance).hp;
        }

        // Only a hit that took health counts: TakeDamage returns early for immune hits.
        [HarmonyPatch(typeof(HealthManager), "TakeDamage", typeof(HitInstance))]
        [HarmonyPostfix]
        private static void AfterTakeDamage(HealthManager __instance, HitInstance hitInstance, int __state)
        {
            if (GetDamageTarget(__instance).hp < __state)
            {
                CrestMovesetComposer.NotifyEnemyDamaged(__instance, hitInstance);
            }
        }

        // Some enemy parts pass their damage on to another HealthManager.
        private static HealthManager GetDamageTarget(HealthManager enemy)
        {
            return enemy.SendDamageTo != null ? enemy.SendDamageTo : enemy;
        }
    }
}
