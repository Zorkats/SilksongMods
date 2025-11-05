using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomCrestMod
{
    public static class DownAttackModifier
    {
        private static bool _hasPatchedDownAttack;
        private static HeroControllerConfig _backupConfig;
        private static HeroController.ConfigGroup _backupConfigGroup;
        private static HeroController.ConfigGroup _sourceConfigGroup;

        private static readonly FieldInfo _heroAnimOverrideLibField = AccessTools.Field(typeof(HeroControllerConfig), "heroAnimOverrideLib");

        private static readonly string[] _configFields = {
            "downSlashType",
            "downSlashEvent",
            "downspikeAnticTime",
            "downspikeTime",
            "downspikeSpeed",
            "downspikeRecoveryTime",
            "downspikeBurstEffect",
            "downspikeThrusts",
        };

        private static readonly string[] _groupFields = { "DownSlashObject", "AltDownSlashObject" };

        public static void PatchDownAttackIfNecessary(HeroController heroController, HeroController.ConfigGroup hunterGroup)
        {
            if (_hasPatchedDownAttack)
                return;

            if (Plugin.DownAttackCrest.Value == CrestType.Hunter)
            {
                Plugin.ModLogger.LogInfo("Using Hunter's default down attack - no patching needed");
                return;
            }

            Plugin.ModLogger.LogInfo($"Patching down attack with {Plugin.DownAttackCrest.Value}");
            _hasPatchedDownAttack = true;

            var allConfigGroups = GetConfigGroups(heroController);
            var hunterConfigGroup = hunterGroup ?? GetHunterConfigGroup(heroController);

            if (hunterConfigGroup == null)
            {
                Plugin.ModLogger.LogError("Cannot find Hunter ConfigGroup");
                return;
            }

            _sourceConfigGroup = GetConfigGroupForCrest(allConfigGroups, Plugin.DownAttackCrest.Value);

            if (_sourceConfigGroup == null)
            {
                Plugin.ModLogger.LogError($"Cannot find ConfigGroup for {Plugin.DownAttackCrest.Value}");
                return;
            }

            BackupOriginalConfig(hunterConfigGroup);

            // Activate source to load components
            if (_sourceConfigGroup.ActiveRoot != null)
            {
                _sourceConfigGroup.ActiveRoot.SetActive(true);
                Plugin.ModLogger.LogInfo($"Activated source: {_sourceConfigGroup.ActiveRoot.name}");
            }

            OverwriteDownAttackMoveset(_sourceConfigGroup, hunterConfigGroup);

            Plugin.ModLogger.LogInfo("Down attack patch complete");
        }

        public static void RevertDownAttackPatch(HeroController heroController)
        {
            if (!_hasPatchedDownAttack)
                return;

            _hasPatchedDownAttack = false;

            if (_sourceConfigGroup != null && _sourceConfigGroup.ActiveRoot != null)
            {
                _sourceConfigGroup.ActiveRoot.SetActive(false);
            }

            if (_backupConfigGroup != null && _backupConfigGroup.Config != null)
            {
                var hunterGroup = GetHunterConfigGroup(heroController);
                if (hunterGroup != null)
                {
                    OverwriteDownAttackMoveset(_backupConfigGroup, hunterGroup);
                }
            }

            Plugin.ModLogger.LogInfo("Down attack patch reverted");
        }

        private static void BackupOriginalConfig(HeroController.ConfigGroup hunterGroup)
        {
            if (_backupConfig != null)
                return;

            _backupConfig = ScriptableObject.CreateInstance<HeroControllerConfig>();
            _backupConfigGroup = new HeroController.ConfigGroup { Config = _backupConfig };

            _heroAnimOverrideLibField.SetValue(_backupConfig, _heroAnimOverrideLibField.GetValue(hunterGroup.Config));
            CopyAndInitializeConfigGroup(hunterGroup, _backupConfigGroup);

            Plugin.ModLogger.LogInfo("Backed up Hunter's original down attack");
        }

        private static void OverwriteDownAttackMoveset(
            HeroController.ConfigGroup sourceGroup,
            HeroController.ConfigGroup targetGroup)
        {
            if (sourceGroup == null || targetGroup == null)
            {
                Plugin.ModLogger.LogError("Cannot modify - source or target is null");
                return;
            }

            // Copy config fields
            CopyFields(sourceGroup, targetGroup, _groupFields);

            if (sourceGroup.Config != null && targetGroup.Config != null)
            {
                CopyFields(sourceGroup.Config, targetGroup.Config, _configFields);
            }

            // Initialize components based on DownSlashType
            var downSlashType = sourceGroup.Config.DownSlashType;
            Plugin.ModLogger.LogInfo($"Initializing down attack for type: {downSlashType}");

            switch (downSlashType)
            {
                case HeroControllerConfig.DownSlashTypes.DownSpike:
                    if (sourceGroup.DownSlashObject != null)
                    {
                        var downspike = GetOrAddComponent<Downspike>(sourceGroup.DownSlashObject);
                        SetProperty(targetGroup, "Downspike", downspike);
                    }
                    if (sourceGroup.AltDownSlashObject != null)
                    {
                        var altDownspike = GetOrAddComponent<Downspike>(sourceGroup.AltDownSlashObject);
                        SetProperty(targetGroup, "AltDownspike", altDownspike);
                    }
                    goto case HeroControllerConfig.DownSlashTypes.Custom;

                case HeroControllerConfig.DownSlashTypes.Slash:
                    if (sourceGroup.DownSlashObject != null)
                    {
                        var downSlash = GetOrAddComponent<NailSlash>(sourceGroup.DownSlashObject);
                        SetProperty(targetGroup, "DownSlash", downSlash);
                    }
                    if (sourceGroup.AltDownSlashObject != null)
                    {
                        var altDownSlash = GetOrAddComponent<NailSlash>(sourceGroup.AltDownSlashObject);
                        SetProperty(targetGroup, "AltDownSlash", altDownSlash);
                    }
                    goto case HeroControllerConfig.DownSlashTypes.Custom;

                case HeroControllerConfig.DownSlashTypes.Custom:
                    if (sourceGroup.DownSlashObject != null)
                    {
                        var damager = GetOrAddComponent<DamageEnemies>(sourceGroup.DownSlashObject);
                        SetProperty(targetGroup, "DownSlashDamager", damager);
                    }
                    if (sourceGroup.AltDownSlashObject != null)
                    {
                        var altDamager = GetOrAddComponent<DamageEnemies>(sourceGroup.AltDownSlashObject);
                        SetProperty(targetGroup, "AltDownSlashDamager", altDamager);
                    }
                    break;
            }

            // Merge animations
            MergeDownAttackAnimations(sourceGroup, targetGroup);

            Plugin.ModLogger.LogInfo("Down attack moveset overwrite complete");
        }

        private static void CopyAndInitializeConfigGroup(
            HeroController.ConfigGroup source,
            HeroController.ConfigGroup target)
        {
            CopyFields(source, target, _groupFields);

            if (source.Config != null && target.Config != null)
            {
                CopyFields(source.Config, target.Config, _configFields);
            }
        }

        private static void MergeDownAttackAnimations(
            HeroController.ConfigGroup sourceGroup,
            HeroController.ConfigGroup targetGroup)
        {
            if (sourceGroup?.Config == null || targetGroup?.Config == null)
                return;

            var sourceAnimLib = _heroAnimOverrideLibField.GetValue(sourceGroup.Config) as tk2dSpriteAnimation;
            var targetAnimLib = _heroAnimOverrideLibField.GetValue(targetGroup.Config) as tk2dSpriteAnimation;

            if (sourceAnimLib == null || targetAnimLib == null)
            {
                Plugin.ModLogger.LogWarning("Animation libraries are null - skipping animation merge");
                return;
            }

            Plugin.ModLogger.LogInfo("Merging down attack animations");

            // For FSM-driven attacks, copy ALL clips
            bool isFSMDriven = sourceGroup.Config.DownSlashType == HeroControllerConfig.DownSlashTypes.Custom;

            if (isFSMDriven)
            {
                Plugin.ModLogger.LogInfo("FSM-driven attack detected - copying ALL animations");

                foreach (var clip in sourceAnimLib.clips)
                {
                    if (clip == null || string.IsNullOrEmpty(clip.name))
                        continue;

                    var existingIndex = Array.FindIndex(targetAnimLib.clips, c => c != null && c.name == clip.name);
                    if (existingIndex >= 0)
                    {
                        targetAnimLib.clips[existingIndex] = clip;
                    }
                    else
                    {
                        var newArray = new tk2dSpriteAnimationClip[targetAnimLib.clips.Length + 1];
                        targetAnimLib.clips.CopyTo(newArray, 0);
                        newArray[^1] = clip;
                        targetAnimLib.clips = newArray;
                    }
                }
            }
            else
            {
                // For normal attacks, only copy down-related animations
                string[] keywords = { "down", "spike", "drill", "grind", "recovery" };

                foreach (var clip in sourceAnimLib.clips)
                {
                    if (clip == null || string.IsNullOrEmpty(clip.name))
                        continue;

                    bool matches = false;
                    foreach (var keyword in keywords)
                    {
                        if (clip.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matches = true;
                            break;
                        }
                    }

                    if (!matches)
                        continue;

                    var existingIndex = Array.FindIndex(targetAnimLib.clips, c => c != null && c.name == clip.name);
                    if (existingIndex >= 0)
                    {
                        targetAnimLib.clips[existingIndex] = clip;
                        Plugin.ModLogger.LogInfo($"  Replaced: {clip.name}");
                    }
                    else
                    {
                        var newArray = new tk2dSpriteAnimationClip[targetAnimLib.clips.Length + 1];
                        targetAnimLib.clips.CopyTo(newArray, 0);
                        newArray[^1] = clip;
                        targetAnimLib.clips = newArray;
                        Plugin.ModLogger.LogInfo($"  Added: {clip.name}");
                    }
                }
            }

            AnimationLibraryHelper.RefreshAnimationLookup(targetAnimLib);
        }

        private static T GetOrAddComponent<T>(GameObject gameObj) where T : Component
        {
            var component = gameObj.GetComponent<T>();
            if (component == null)
                component = gameObj.AddComponent<T>();
            return component;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var prop = AccessTools.Property(target.GetType(), propertyName);
            if (prop != null)
            {
                var setter = prop.GetSetMethod(true);
                setter?.Invoke(target, new[] { value });
            }
            else
            {
                // Try backing field
                var field = AccessTools.Field(target.GetType(), $"<{propertyName}>k__BackingField");
                field?.SetValue(target, value);
            }
        }

        private static void CopyFields(object source, object target, string[] fieldNames)
        {
            foreach (var name in fieldNames)
            {
                var field = AccessTools.Field(source.GetType(), name);
                if (field != null)
                {
                    var value = field.GetValue(source);
                    field.SetValue(target, value);
                }
            }
        }

        private static HeroController.ConfigGroup[] GetConfigGroups(HeroController hc)
        {
            var field = AccessTools.Field(typeof(HeroController), "configs");
            return field?.GetValue(hc) as HeroController.ConfigGroup[];
        }

        private static HeroController.ConfigGroup GetHunterConfigGroup(HeroController hc)
        {
            var groups = GetConfigGroups(hc);
            if (groups == null)
                return null;

            foreach (var group in groups)
            {
                if (group?.ActiveRoot != null &&
                    (group.ActiveRoot.name.Contains("Hunter") || group.ActiveRoot.name.Contains("Default")))
                {
                    return group;
                }
            }
            return null;
        }

        private static HeroController.ConfigGroup GetConfigGroupForCrest(
            HeroController.ConfigGroup[] groups,
            CrestType crestType)
        {
            if (groups == null)
                return null;

            string internalName = crestType.GetInternalName();

            foreach (var group in groups)
            {
                if (group?.ActiveRoot != null &&
                    group.ActiveRoot.name.Equals(internalName, StringComparison.OrdinalIgnoreCase))
                {
                    return group;
                }
            }
            return null;
        }
    }
}