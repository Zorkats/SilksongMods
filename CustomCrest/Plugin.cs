// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CrestComposition;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;

namespace CustomCrestMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("io.github.needleforge", "0.9.0")]
    [BepInDependency("org.silksong-modding.fsmutil", "0.3.16")]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "com.zorkats.silksong.customcrest";
        private const string PluginName = "Custom Crest Builder";
        private const string PluginVersion = "4.0.0";

        public static Sprite? CustomCrestSprite;
        public static Sprite? CustomCrestSilhouetteSprite;

        // Bound in Start: their allowed values include crests other mods register in Awake.
        public static ConfigEntry<string> NeutralAttackCrest = null!;
        public static ConfigEntry<string> UpAttackCrest = null!;
        public static ConfigEntry<string> DashAttackCrest = null!;
        public static ConfigEntry<string> BindingActionCrest = null!;
        public static ConfigEntry<CrestType> DownAttackCrest = null!;
        public static ConfigEntry<int> NeutralAttackSlots = null!;
        public static ConfigEntry<int> UpAttackSlots = null!;
        public static ConfigEntry<int> DownAttackSlots = null!;
        public static ConfigEntry<int> BlueSlots = null!;
        public static ConfigEntry<int> YellowSlots = null!;
        public static ConfigEntry<bool> UnlockAllSlots = null!;
        public static ConfigEntry<bool> UpUseSkillSlots = null!;
        public static ConfigEntry<bool> NeutralUseSkillSlots = null!;
        public static ConfigEntry<bool> DownUseSkillSlots = null!;

        public static ManualLogSource? ModLogger;
        private readonly HashSet<ConfigEntryBase> _slotLayoutEntries = new HashSet<ConfigEntryBase>();
        private Harmony? _harmony;

        private void Awake()
        {
            ModLogger = Logger;
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");

            LoadEmbeddedSprites();
            SetupConfig();

            DownAttackCrest.SettingChanged += OnDownAttackSettingChanged;
            Config.SettingChanged += OnConfigSettingChanged;
            CustomCrestBuilder.RegisterWithNeedleforge();

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(Language_Get_Localization_Patch));
                _harmony.PatchAll(typeof(HeroController_Start_ReconcileTools_Patch));
                Logger.LogInfo("Patches applied");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply patches: {ex}");
            }
        }

        private void Start()
        {
            List<AttackSource> sources = AttackSourceCatalog.GetAvailable(Logger);
            string[] attackSources = sources.Select(source => source.Id).ToArray();
            string[] dashSources = sources
                .Where(source => source.HasPlayableDashAttack)
                .Select(source => source.Id)
                .ToArray();
            Logger.LogInfo($"Attack sources available: {string.Join(", ", attackSources)}");
            Logger.LogInfo($"Dash attack sources available: {string.Join(", ", dashSources)}");

            NeutralAttackCrest = BindAttackSource("NeutralAttack", "Crest to copy Neutral Attack moveset from", attackSources);
            UpAttackCrest = BindAttackSource("UpAttack", "Crest to copy Up Attack moveset from", attackSources);
            DashAttackCrest = BindAttackSource("DashAttack", "Crest to copy Dash Attack moveset from", dashSources);

            NeutralAttackCrest.SettingChanged += OnNeutralAttackSettingChanged;
            UpAttackCrest.SettingChanged += OnUpAttackSettingChanged;
            DashAttackCrest.SettingChanged += OnDashAttackSettingChanged;

            CustomCrestBuilder.ApplyNeutralAttackSelection(NeutralAttackCrest.Value);
            CustomCrestBuilder.ApplyUpAttackSelection(UpAttackCrest.Value);
            CustomCrestBuilder.ApplyDashAttackSelection(DashAttackCrest.Value);

            string[] bindOutcomes = CustomCrestBuilder.Composer != null
                ? CustomCrestBuilder.Composer.BindOutcome.GetAvailableOutcomes().ToArray()
                : new[] { VanillaCrestType.Hunter.ToString() };
            BindingActionCrest = Config.Bind(
                "1. Attack Movesets",
                "BindingAction",
                VanillaCrestType.Hunter.ToString(),
                new ConfigDescription(
                    "Crest whose bind outcome to use: Warrior enters rage, Witch binds with tentacles, " +
                    "Reaper enters reaper mode, Spell charges, Toolmaster quick crafts when binding " +
                    "with Up held, Wanderer heals normally and gives lucky hits while silk is at 9 or more, " +
                    "Hunter heals normally, and Void, when Void Crest is installed, focuses and uses " +
                    "its sphere when binding with Up held",
                    new AcceptableValueList<string>(bindOutcomes)));
            BindingActionCrest.SettingChanged += OnBindingActionSettingChanged;
            CustomCrestBuilder.ApplyBindOutcomeSelection(BindingActionCrest.Value);
        }

        private ConfigEntry<string> BindAttackSource(string key, string description, string[] attackSources)
        {
            return Config.Bind(
                "1. Attack Movesets",
                key,
                VanillaCrestType.Hunter.ToString(),
                new ConfigDescription(description, new AcceptableValueList<string>(attackSources)));
        }

        private void OnDestroy()
        {
            if (NeutralAttackCrest != null)
            {
                NeutralAttackCrest.SettingChanged -= OnNeutralAttackSettingChanged;
            }

            if (UpAttackCrest != null)
            {
                UpAttackCrest.SettingChanged -= OnUpAttackSettingChanged;
            }

            if (DownAttackCrest != null)
            {
                DownAttackCrest.SettingChanged -= OnDownAttackSettingChanged;
            }

            if (DashAttackCrest != null)
            {
                DashAttackCrest.SettingChanged -= OnDashAttackSettingChanged;
            }

            if (BindingActionCrest != null)
            {
                BindingActionCrest.SettingChanged -= OnBindingActionSettingChanged;
            }

            Config.SettingChanged -= OnConfigSettingChanged;

            _harmony?.UnpatchSelf();
        }

        private static void OnNeutralAttackSettingChanged(object sender, EventArgs args)
        {
            CustomCrestBuilder.ApplyNeutralAttackSelection(NeutralAttackCrest.Value);
        }

        private static void OnUpAttackSettingChanged(object sender, EventArgs args)
        {
            CustomCrestBuilder.ApplyUpAttackSelection(UpAttackCrest.Value);
        }

        private static void OnDownAttackSettingChanged(object sender, EventArgs args)
        {
            CustomCrestBuilder.ApplyDownAttackSelection(DownAttackCrest.Value);
        }

        private static void OnDashAttackSettingChanged(object sender, EventArgs args)
        {
            CustomCrestBuilder.ApplyDashAttackSelection(DashAttackCrest.Value);
        }

        private static void OnBindingActionSettingChanged(object sender, EventArgs args)
        {
            CustomCrestBuilder.ApplyBindOutcomeSelection(BindingActionCrest.Value);
        }

        private void OnConfigSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (_slotLayoutEntries.Contains(args.ChangedSetting))
            {
                CustomCrestBuilder.ApplySlotLayoutChange();
            }
        }

        // The hero is spawned once per load, after the save is in place and before it reads its tools.
        [HarmonyPatch(typeof(HeroController), "Start")]
        public static class HeroController_Start_ReconcileTools_Patch
        {
            private static void Prefix()
            {
                try
                {
                    CustomCrestBuilder.ReconcileSavedTools();
                }
                catch (Exception ex)
                {
                    ModLogger?.LogError($"Could not match saved tools to the slot layout: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(Language), nameof(Language.Get), new[] { typeof(string), typeof(string) })]
        public static class Language_Get_Localization_Patch
        {
            private static void Postfix(string key, string sheetTitle, ref string __result)
            {
                if (!string.Equals(sheetTitle, CustomCrestBuilder.LocalizationSheet, StringComparison.Ordinal))
                {
                    return;
                }

                switch (key)
                {
                    case CustomCrestBuilder.NameKey:
                        __result = "Custom Crest";
                        break;
                    case CustomCrestBuilder.DescriptionKey:
                        __result = "Behold, the power of customization!";
                        break;
                    case CustomCrestBuilder.PrefixKey:
                        __result = "Bound the Custom Crest of";
                        break;
                }
            }
        }

        private void SetupConfig()
        {
            DownAttackCrest = Config.Bind(
                "1. Attack Movesets",
                "DownAttack",
                CrestType.Hunter,
                new ConfigDescription(
                    "Crest to copy Down Attack moveset from",
                    new AcceptableEnumList<CrestType>(
                        CrestType.Hunter,
                        CrestType.Reaper,
                        CrestType.Wanderer,
                        CrestType.Warrior,
                        CrestType.Witch,
                        CrestType.Toolmaster,
                        CrestType.Spell)));

            NeutralAttackSlots = Config.Bind(
                "2. Attack Slot Distribution",
                "NeutralSlots",
                1,
                new ConfigDescription("Number of Neutral Attack slots", new AcceptableValueRange<int>(0, 1)));

            NeutralUseSkillSlots = Config.Bind(
                "2. Attack Slot Distribution",
                "NeutralUseSkillSlots",
                false,
                "If true, Neutral attack slots will be white/Skill type instead of red");

            UpAttackSlots = Config.Bind(
                "2. Attack Slot Distribution",
                "UpSlots",
                1,
                new ConfigDescription("Number of Up Attack slots", new AcceptableValueRange<int>(0, 1)));

            UpUseSkillSlots = Config.Bind(
                "2. Attack Slot Distribution",
                "UpUseSkillSlots",
                false,
                "If true, Up attack slots will be white/Skill type instead of red");

            DownAttackSlots = Config.Bind(
                "2. Attack Slot Distribution",
                "DownSlots",
                1,
                new ConfigDescription("Number of Down Attack slots", new AcceptableValueRange<int>(0, 1)));

            DownUseSkillSlots = Config.Bind(
                "2. Attack Slot Distribution",
                "DownUseSkillSlots",
                false,
                "If true, Down attack slots will be white/Skill type instead of red");

            BlueSlots = Config.Bind(
                "3. Passive & Utility Slots",
                "BlueSlots",
                3,
                new ConfigDescription("Number of Blue (passive) tool slots", new AcceptableValueRange<int>(0, 10)));

            YellowSlots = Config.Bind(
                "3. Passive & Utility Slots",
                "YellowSlots",
                3,
                new ConfigDescription("Number of Yellow (utility) tool slots", new AcceptableValueRange<int>(0, 10)));

            UnlockAllSlots = Config.Bind(
                "4. General Options",
                "UnlockAllSlots",
                true,
                "If true, all slots are unlocked by default");

            _slotLayoutEntries.UnionWith(new ConfigEntryBase[]
            {
                NeutralAttackSlots,
                NeutralUseSkillSlots,
                UpAttackSlots,
                UpUseSkillSlots,
                DownAttackSlots,
                DownUseSkillSlots,
                BlueSlots,
                YellowSlots,
                UnlockAllSlots
            });
        }

        private Sprite? LoadSprite(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            try
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Logger.LogError($"Embedded resource not found: {resourceName}");
                    return null;
                }

                byte[] buffer = new byte[stream.Length];
                int offset = 0;
                while (offset < buffer.Length)
                {
                    int read = stream.Read(buffer, offset, buffer.Length - offset);
                    if (read <= 0)
                    {
                        break;
                    }

                    offset += read;
                }

                Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(texture, buffer);
                texture.Apply();

                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100.0f);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load sprite from resource {resourceName}: {ex}");
                return null;
            }
        }

        private void LoadEmbeddedSprites()
        {
            CustomCrestSprite = LoadSprite("CustomCrest.Custom_Crest.png");
            CustomCrestSilhouetteSprite = LoadSprite("CustomCrest.CustomCrestSilhouette.png");

            if (CustomCrestSprite != null)
            {
                CustomCrestSprite.name = "CustomCrestIcon";
            }
        }
    }
}