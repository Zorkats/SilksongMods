using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GlobalSettings;
using UnityEngine;

namespace CustomCrestMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "com.zorkats.silksong.customcrest";
        private const string PluginName = "Custom Crest Builder";
        private const string PluginVersion = "3.0.0";
        public static Sprite CustomCrestSprite;
        public static Sprite CustomCrestSilhouetteSprite;

        public static ConfigEntry<CrestType> NeutralAttackCrest;
        public static ConfigEntry<CrestType> UpAttackCrest;
        public static ConfigEntry<CrestType> DownAttackCrest;
        public static ConfigEntry<CrestType> BindingActionCrest;
        public static ConfigEntry<bool> UseBindingCrestDash;
        public static ConfigEntry<int> NeutralAttackSlots;
        public static ConfigEntry<int> UpAttackSlots;
        public static ConfigEntry<int> DownAttackSlots;
        public static ConfigEntry<int> BlueSlots;
        public static ConfigEntry<int> YellowSlots;
        public static ConfigEntry<bool> UnlockAllSlots;
        public static ConfigEntry<bool> UpUseSkillSlots;
        public static ConfigEntry<bool> NeutralUseSkillSlots;
        public static ConfigEntry<bool> DownUseSkillSlots;

        public static ManualLogSource ModLogger;
        private static ToolItemManager _toolItemManager = null;
        private Harmony _harmony;

        private void Awake()
        {
            ModLogger = Logger;
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
            LoadEmbeddedSprites();

            SetupConfig();
            _harmony = new Harmony(PluginGuid);

            try
            {
                _harmony.Patch(
                    AccessTools.Method(typeof(ToolItemManager), "Awake"),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(ToolItemManager_Awake_Postfix)))
                );

                _harmony.Patch(
                    AccessTools.Method(typeof(HeroController), "Start"),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(HeroController_Start_Postfix)))
                );

                _harmony.Patch(
                    AccessTools.Method(typeof(ToolItemManager), "SetEquippedCrest"),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(SetEquippedCrest_Postfix)))
                );

                _harmony.PatchAll(typeof(HeroController_UpdateConfig_Patch));
                _harmony.PatchAll(typeof(Fsm_DoTransition_Patch));
                _harmony.PatchAll(typeof(HeroController_BindCompleted_CustomCrest_Patch));
                _harmony.PatchAll(typeof(ToolCrest_IsEquipped_Patch));
                _harmony.PatchAll(typeof(Gameplay_ReaperCrest_Patch));
                _harmony.PatchAll(typeof(Gameplay_WarriorCrest_Patch));
                _harmony.PatchAll(typeof(DamageEnemies_Trigger_SpoofWarrior_Patch));
                _harmony.PatchAll(typeof(HealthManager_TakeDamage_ReaperSilk_Patch));

                Logger.LogInfo("✓ Patches applied");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply patches: {ex}");
            }
        }

        private static void ToolItemManager_Awake_Postfix(ToolItemManager __instance)
        {
            _toolItemManager = __instance;
        }

        private static void HeroController_Start_Postfix(HeroController __instance)
        {
            try
            {
                if (GameManager.instance == null || _toolItemManager == null) return;

                var existingCrest = ToolItemManager.GetCrestByName("CustomCrest");
                var isEquipped = GameManager.instance?.playerData?.CurrentCrestID == "CustomCrest";

                if (existingCrest == null)
                {
                    CustomCrestBuilder.ResetInitialization();
                    CustomCrestBuilder.Initialize(_toolItemManager);
                }
                else
                {
                    CustomCrestBuilder.Initialize(_toolItemManager);
                }

                if (!CustomCrestBuilder.IsInitialized)
                {
                    Plugin.ModLogger.LogError("CustomCrestBuilder failed to initialize!");
                    return;
                }

                var customGroup = CustomCrestBuilder.GetCustomConfigGroup();
                if (customGroup == null || customGroup.ActiveRoot == null || !customGroup.ActiveRoot)
                {
                    CustomCrestBuilder.CreateCustomConfigGroup(__instance);
                }
                else
                {
                    for (int i = customGroup.ActiveRoot.transform.childCount - 1; i >= 0; i--)
                    {
                        var child = customGroup.ActiveRoot.transform.GetChild(i);
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                    CustomCrestBuilder.RefreshAttackGameObjects(__instance);
                }

                if (isEquipped)
                {
                    var updateConfigMethod = AccessTools.Method(typeof(HeroController), "UpdateConfig");
                    updateConfigMethod?.Invoke(__instance, null);
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLogger.LogError($"Error in HeroController.Start: {ex.Message}");
            }
        }

        private static void SetEquippedCrest_Postfix(string crestId)
        {
            if (crestId == "CustomCrest" && HeroController.instance != null)
            {
                var updateConfigMethod = AccessTools.Method(typeof(HeroController), "UpdateConfig");
                updateConfigMethod?.Invoke(HeroController.instance, null);
                CustomCrestBuilder.UpdateBindingFSM();
            }
        }


        [HarmonyPatch(typeof(HeroController), "UpdateConfig")]
        public static class HeroController_UpdateConfig_Patch
        {
            // Cache for private HeroController fields
            private static FieldInfo _wandererDashComboWindowTimerField = null;

            // Cache for private HeroControllerConfig fields
            private static FieldInfo _configWandererDashComboTimeField = null;

            static void Prefix(HeroController __instance)
            {
                try
                {
                    if (GameManager.instance?.playerData?.CurrentCrestID != "CustomCrest")
                        return;

                    var hybridConfig = CustomCrestBuilder.GetHybridConfig();
                    if (hybridConfig == null)
                    {
                        Plugin.ModLogger.LogError("Hybrid config is NULL!");
                        return;
                    }

                    var crestConfigField = AccessTools.Field(typeof(HeroController), "crestConfig");
                    crestConfigField?.SetValue(__instance, hybridConfig);

                    // --- THIS IS THE IMPROVEMENT (Fixes Wanderer Freeze) ---
                    // Your patch skips the original UpdateConfig, so we must
                    // set the private HeroController fields ourselves.

                    // 1. Check if we are using Wanderer Dash
                    if (Plugin.UseBindingCrestDash.Value && Plugin.BindingActionCrest.Value == CrestType.Wanderer)
                    {
                        // 2. Find the private field on HeroController (once)
                        if (_wandererDashComboWindowTimerField == null)
                        {
                            _wandererDashComboWindowTimerField = AccessTools.Field(typeof(HeroController), "wandererDashComboWindowTimer");
                            if (_wandererDashComboWindowTimerField == null)
                                Plugin.ModLogger.LogError("Could not find private field 'wandererDashComboWindowTimer' on HeroController!");
                        }

                        // 3. Find the matching data field on the *Config* object (once)
                        //    (This name is a guess, but it's the most likely one)
                        if (_configWandererDashComboTimeField == null)
                        {
                            _configWandererDashComboTimeField = AccessTools.Field(typeof(HeroControllerConfig), "wandererDashComboWindowTimer");
                            if (_configWandererDashComboTimeField == null)
                                 Plugin.ModLogger.LogError("Could not find field 'wandererDashComboTime' on HeroControllerConfig!");
                        }

                        // 4. If we found both, set the value
                        if (_wandererDashComboWindowTimerField != null && _configWandererDashComboTimeField != null)
                        {
                            // 5. Get the REAL Wanderer config to read its value
                            var wandererCrest = CustomCrestBuilder.GetCrestByType(CrestType.Wanderer);
                            if (wandererCrest?.HeroConfig != null)
                            {
                                // 6. Read the value from the config
                                float timerValue = (float)_configWandererDashComboTimeField.GetValue(wandererCrest.HeroConfig);

                                // 7. Set the private field on HeroController
                                _wandererDashComboWindowTimerField.SetValue(__instance, timerValue);
                                Plugin.ModLogger.LogInfo($"Set private 'wandererDashComboWindowTimer' to {timerValue}");
                            }
                        }
                    }
                    // --- End of Improvement ---

                    // This is your original code, it must run *after* our fix
                    var configsField = AccessTools.Field(typeof(HeroController), "configs");
                    var configs = configsField?.GetValue(__instance) as HeroController.ConfigGroup[];

                    var customGroup = configs?.FirstOrDefault(c =>
                        c?.ActiveRoot != null && c.ActiveRoot && c.ActiveRoot.name == "CustomCrest");

                    if (customGroup != null)
                    {
                        var configFieldOnGroup = AccessTools.Field(typeof(HeroController.ConfigGroup), "Config");
                        configFieldOnGroup?.SetValue(customGroup, hybridConfig);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLogger.LogError($"Error in UpdateConfig Prefix: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(HutongGames.PlayMaker.Fsm), "DoTransition")]
        public static class Fsm_DoTransition_Patch
        {
            static void Prefix(HutongGames.PlayMaker.Fsm __instance, HutongGames.PlayMaker.FsmTransition transition)
            {
                try
                {
                    if (__instance.Name == "Bind" &&
                        transition.ToState == "Bind Type" &&
                        GameManager.instance?.playerData?.CurrentCrestID == "CustomCrest")
                    {
                        var warriorVar = __instance.Variables.FindFsmBool("Is Warrior Equipped");
                        var witchVar = __instance.Variables.FindFsmBool("Is Witch Equipped");
                        var shamanVar = __instance.Variables.FindFsmBool("Is Shaman Equipped");

                        if (warriorVar != null) warriorVar.Value = false;
                        if (witchVar != null) witchVar.Value = false;
                        if (shamanVar != null) shamanVar.Value = false;

                        switch (Plugin.BindingActionCrest.Value)
                        {
                            case CrestType.Warrior:
                                if (warriorVar != null) warriorVar.Value = true;
                                break;
                            case CrestType.Witch:
                                if (witchVar != null) witchVar.Value = true;
                                break;
                            case CrestType.Spell:
                                if (shamanVar != null) shamanVar.Value = true;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLogger.LogError($"Error in DoTransition: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Gameplay_WarriorCrest_Patch
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                return typeof(Gameplay).GetProperty("WarriorCrest",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetGetMethod();
            }

            static void Postfix(ref ToolCrest __result)
            {
                if (GameManager.instance?.playerData?.CurrentCrestID == "CustomCrest" &&
                    Plugin.BindingActionCrest.Value == CrestType.Warrior)
                {
                    var warriorCrest = ToolItemManager.GetCrestByName("Warrior");
                    if (warriorCrest != null)
                        __result = warriorCrest;
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "BindCompleted")]
        [HarmonyPriority(Priority.First)]
        public static class HeroController_BindCompleted_CustomCrest_Patch
        {
            private static bool _isHandling = false;

            static void Prefix()
            {
                if (_isHandling || GameManager.instance?.playerData?.CurrentCrestID != "CustomCrest")
                    return;

                _isHandling = true;
            }

            static void Postfix(HeroController __instance)
            {
                if (!_isHandling)
                    return;

                _isHandling = false;

                if (GameManager.instance?.playerData?.CurrentCrestID != "CustomCrest")
                    return;

                try
                {
                    switch (Plugin.BindingActionCrest.Value)
                    {
                        case CrestType.Warrior:
                            __instance.StartCoroutine(ActivateWarriorRageDelayed(__instance));
                            break;
                        case CrestType.Reaper:
                            HandleReaperBinding(__instance);
                            break;
                        case CrestType.Wanderer:
                            HandleWandererBinding(__instance);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLogger.LogError($"Error in BindCompleted: {ex.Message}");
                }
            }

            static System.Collections.IEnumerator ActivateWarriorRageDelayed(HeroController instance)
            {
                yield return new WaitForSeconds(0.1f);

                try
                {
                    var warriorStateProp = AccessTools.Property(typeof(HeroController), "warriorState");
                    object warriorState = warriorStateProp?.GetValue(instance);

                    if (warriorState == null)
                    {
                        var warriorStateField = AccessTools.Field(typeof(HeroController), "warriorState");
                        warriorState = warriorStateField?.GetValue(instance);
                    }

                    if (warriorState == null)
                    {
                        Plugin.ModLogger.LogError("WarriorState is null!");
                        yield break;
                    }

                    var warriorStateType = warriorState.GetType();
                    var isInRageModeField = warriorStateType.GetField("IsInRageMode");
                    var rageTimeLeftField = warriorStateType.GetField("RageTimeLeft");

                    if (isInRageModeField != null && rageTimeLeftField != null)
                    {
                        isInRageModeField.SetValue(warriorState, true);
                        rageTimeLeftField.SetValue(warriorState, 10f);

                        if (warriorStateProp != null)
                            warriorStateProp.SetValue(instance, warriorState);
                        else
                        {
                            var warriorStateField = AccessTools.Field(typeof(HeroController), "warriorState");
                            warriorStateField?.SetValue(instance, warriorState);
                        }

                        var rageFSM = instance.GetComponents<PlayMakerFSM>()
                            .FirstOrDefault(fsm => fsm.FsmName == "Rage");
                        rageFSM?.SendEvent("RAGE GAIN");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLogger.LogError($"Error activating rage: {ex.Message}");
                }
            }

            static void Finalizer()
            {
                _isHandling = false;
            }

            private static void HandleReaperBinding(HeroController instance)
            {
                try
                {
                    var reaperStateField = AccessTools.Field(typeof(HeroController), "reaperState");
                    var reaperState = reaperStateField?.GetValue(instance);
                    if (reaperState == null) return;

                    var reaperStateType = reaperState.GetType();
                    var isInReaperModeField = reaperStateType.GetField("IsInReaperMode");
                    var durationField = reaperStateType.GetField("ReaperModeDurationLeft");

                    if (isInReaperModeField != null && durationField != null)
                    {
                        isInReaperModeField.SetValue(reaperState, true);
                        durationField.SetValue(reaperState, Gameplay.ReaperModeDuration);
                        reaperStateField.SetValue(instance, reaperState);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLogger.LogError($"Error in HandleReaperBinding: {ex.Message}");
                }
            }

            private static void HandleWandererBinding(HeroController instance)
            {
                try
                {
                    var wandererStateField = AccessTools.Field(typeof(HeroController), "<WandererState>k__BackingField") ??
                                           AccessTools.Field(typeof(HeroController), "WandererState");

                    var wandererState = wandererStateField?.GetValue(instance);
                    if (wandererState == null) return;

                    var queuedCritField = wandererState.GetType().GetField("QueuedNextHitCritical");
                    if (queuedCritField != null)
                    {
                        queuedCritField.SetValue(wandererState, true);
                        wandererStateField.SetValue(instance, wandererState);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLogger.LogError($"Error in HandleWandererBinding: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(ToolCrest), "get_IsEquipped")]
        public static class ToolCrest_IsEquipped_Patch
        {
            static void Postfix(ToolCrest __instance, ref bool __result)
            {
                if (GameManager.instance?.playerData?.CurrentCrestID == "CustomCrest")
                {
                    var bindingCrestName = GetInternalCrestName(Plugin.BindingActionCrest.Value);
                    var crestName = __instance.name;

                    if (crestName == bindingCrestName ||
                        crestName.Contains(bindingCrestName) ||
                        (bindingCrestName == "Default" && crestName.Contains("Hunter")))
                    {
                        __result = true;
                    }
                }
            }

            private static string GetInternalCrestName(CrestType crestType) => crestType switch
            {
                CrestType.Hunter => "Default",
                CrestType.Wanderer => "Wanderer",
                CrestType.Warrior => "Warrior",
                CrestType.Reaper => "Reaper",
                CrestType.Witch => "Witch",
                CrestType.Toolmaster => "Toolmaster",
                CrestType.Spell => "Spell",
                _ => crestType.ToString()
            };
        }

        [HarmonyPatch]
        public static class Gameplay_ReaperCrest_Patch
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                return typeof(Gameplay).GetProperty("ReaperCrest",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetGetMethod();
            }

            static void Postfix(ref ToolCrest __result)
            {
                if (GameManager.instance?.playerData?.CurrentCrestID == "CustomCrest" &&
                    Plugin.BindingActionCrest.Value == CrestType.Reaper &&
                    __result != null)
                {
                    // Reaper mode should be active via HandleReaperBinding
                }
            }
        }

        public static bool IsSpoofingCrestID = false;

        [HarmonyPatch(typeof(DamageEnemies), "OnTriggerEnter2D")]
        public static class DamageEnemies_Trigger_SpoofWarrior_Patch
        {
            static void Prefix()
            {
                if (GameManager.instance?.playerData?.CurrentCrestID == "CustomCrest" &&
                    Plugin.BindingActionCrest.Value == CrestType.Warrior &&
                    HeroController.instance != null)
                {
                    var warriorState = HeroController.instance.WarriorState;
                    var isInRageModeField = warriorState.GetType().GetField("IsInRageMode");

                    if (isInRageModeField != null && (bool)isInRageModeField.GetValue(warriorState))
                    {
                        IsSpoofingCrestID = true;
                        GameManager.instance.playerData.CurrentCrestID = "Warrior";
                    }
                }
            }

            static void Postfix()
            {
                if (IsSpoofingCrestID)
                {
                    GameManager.instance.playerData.CurrentCrestID = "CustomCrest";
                    IsSpoofingCrestID = false;
                }
            }

            static void Finalizer()
            {
                if (IsSpoofingCrestID)
                {
                    GameManager.instance.playerData.CurrentCrestID = "CustomCrest";
                    IsSpoofingCrestID = false;
                }
            }
        }

        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        public static class HealthManager_TakeDamage_ReaperSilk_Patch
        {
            static void Postfix(HealthManager __instance, HitInstance hitInstance)
            {
                if (GameManager.instance?.playerData?.CurrentCrestID != "CustomCrest" ||
                    Plugin.BindingActionCrest.Value != CrestType.Reaper ||
                    HeroController.instance == null)
                    return;

                if (hitInstance.SilkGeneration == HitSilkGeneration.None)
                    return;

                if (hitInstance.AttackType != AttackTypes.Nail &&
                    (hitInstance.AttackType != AttackTypes.Heavy || !hitInstance.IsNailTag))
                    return;

                var reaperStateField = AccessTools.Field(typeof(HeroController), "reaperState");
                var reaperState = reaperStateField?.GetValue(HeroController.instance);
                if (reaperState == null) return;

                var isInReaperModeField = reaperState.GetType().GetField("IsInReaperMode");
                if (isInReaperModeField == null || !(bool)isInReaperModeField.GetValue(reaperState))
                    return;

                if (__instance.EnemyType == HealthManager.EnemyTypes.Shade ||
                    __instance.EnemyType == HealthManager.EnemyTypes.Armoured ||
                    __instance.DoNotGiveSilk)
                    return;

                var getPayoutMethod = AccessTools.Method(typeof(HeroController), "GetReaperPayout");
                int payoutAmount = (int)getPayoutMethod?.Invoke(HeroController.instance, null);
                if (payoutAmount <= 0) return;

                int cardinalDirection = DirectionUtils.GetCardinalDirection(
                    hitInstance.GetActualDirection(__instance.transform, HitInstance.TargetType.Regular));

                float angleMin, angleMax;
                switch (cardinalDirection)
                {
                    case 0: angleMin = 315f; angleMax = 415f; break;
                    case 1: angleMin = 45f; angleMax = 135f; break;
                    case 2: angleMin = 125f; angleMax = 225f; break;
                    case 3: angleMin = 225f; angleMax = 315f; break;
                    default: angleMin = 0f; angleMax = 360f; break;
                }

                FlingUtils.SpawnAndFling(new FlingUtils.Config()
                {
                    Prefab = Gameplay.ReaperBundlePrefab,
                    AmountMin = payoutAmount,
                    AmountMax = payoutAmount,
                    SpeedMin = 25f,
                    SpeedMax = 50f,
                    AngleMin = angleMin,
                    AngleMax = angleMax
                }, __instance.transform, __instance.EffectOrigin);
            }
        }

        private void SetupConfig()
        {
            NeutralAttackCrest = Config.Bind("1. Attack Movesets", "NeutralAttack", CrestType.Hunter,
                new ConfigDescription("Crest to copy Neutral Attack moveset from",
                    new AcceptableEnumList<CrestType>(CrestType.Hunter, CrestType.Reaper, CrestType.Wanderer,
                        CrestType.Warrior, CrestType.Witch, CrestType.Toolmaster, CrestType.Spell)));

            UpAttackCrest = Config.Bind("1. Attack Movesets", "UpAttack", CrestType.Hunter,
                new ConfigDescription("Crest to copy Up Attack moveset from",
                    new AcceptableEnumList<CrestType>(CrestType.Hunter, CrestType.Reaper, CrestType.Wanderer,
                        CrestType.Warrior, CrestType.Witch, CrestType.Toolmaster, CrestType.Spell)));

            DownAttackCrest = Config.Bind("1. Attack Movesets", "DownAttack", CrestType.Hunter,
                new ConfigDescription("Crest to copy Down Attack moveset from",
                    new AcceptableEnumList<CrestType>(CrestType.Hunter, CrestType.Reaper, CrestType.Wanderer,
                        CrestType.Warrior, CrestType.Witch, CrestType.Toolmaster, CrestType.Spell)));

            BindingActionCrest = Config.Bind("1. Attack Movesets", "BindingAction", CrestType.Hunter,
                new ConfigDescription("Crest to copy Binding/Heal animations from",
                    new AcceptableEnumList<CrestType>(CrestType.Hunter, CrestType.Reaper, CrestType.Wanderer,
                        CrestType.Warrior, CrestType.Witch, CrestType.Toolmaster, CrestType.Spell)));

            UseBindingCrestDash = Config.Bind("1. Attack Movesets", "UseBindingCrestDash", true,
                "If true, uses the Binding Crest's dash attacks. Recommended for better compatibility (Wanderer's dash attack currently doesn't work.");

            NeutralAttackSlots = Config.Bind("2. Attack Slot Distribution", "NeutralSlots", 1,
                new ConfigDescription("Number of Neutral Attack slots", new AcceptableValueRange<int>(0, 1)));

            NeutralUseSkillSlots = Config.Bind("2. Attack Slot Distribution", "NeutralUseSkillSlots", false,
                "If true, Neutral attack slots will be white/Skill type instead of red");

            UpAttackSlots = Config.Bind("2. Attack Slot Distribution", "UpSlots", 1,
                new ConfigDescription("Number of Up Attack slots", new AcceptableValueRange<int>(0, 1)));

            UpUseSkillSlots = Config.Bind("2. Attack Slot Distribution", "UpUseSkillSlots", false,
                "If true, Up attack slots will be white/Skill type instead of red");

            DownAttackSlots = Config.Bind("2. Attack Slot Distribution", "DownSlots", 1,
                new ConfigDescription("Number of Down Attack slots", new AcceptableValueRange<int>(0, 1)));

            DownUseSkillSlots = Config.Bind("2. Attack Slot Distribution", "DownUseSkillSlots", false,
                "If true, Down attack slots will be white/Skill type instead of red");

            BlueSlots = Config.Bind("3. Passive & Utility Slots", "BlueSlots", 3,
                new ConfigDescription("Number of Blue (passive) tool slots", new AcceptableValueRange<int>(0, 10)));

            YellowSlots = Config.Bind("3. Passive & Utility Slots", "YellowSlots", 3,
                new ConfigDescription("Number of Yellow (utility) tool slots", new AcceptableValueRange<int>(0, 10)));

            UnlockAllSlots = Config.Bind("4. General Options", "UnlockAllSlots", true,
                "If true, all slots are unlocked by default");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
        private Sprite LoadSprite(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Logger.LogError($"Embedded resource not found: {resourceName}");
                        return null;
                    }

                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);

                    var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    texture.LoadImage(buffer);
                    texture.Apply();

                    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load sprite from resource {resourceName}: {ex}");
                return null;
            }
        }

        // This method now uses the helper to load all our required sprites.
        private void LoadEmbeddedSprites()
        {
            CustomCrestSprite = LoadSprite("CustomCrest.Custom_Crest.png");
            CustomCrestSilhouetteSprite = LoadSprite(resourceName: "CustomCrest.CustomCrestSilhouette.png");
            Logger.LogInfo($"Attempted to load Custom_Crest.png. Result is null? {CustomCrestSprite == null}");
            Logger.LogInfo($"Attempted to load CustomCrestSilhouette.png. Result is null? {CustomCrestSprite == null}");
            if (CustomCrestSprite != null) CustomCrestSprite.name = "CustomCrestIcon";
        }
    }
}