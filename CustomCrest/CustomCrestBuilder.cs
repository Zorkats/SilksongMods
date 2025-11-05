using System;
using System.Collections.Generic;
using System.Linq;
using GenericVariableExtension;
using UnityEngine;
using HarmonyLib;
using HutongGames.PlayMaker;
using Silksong.FsmUtil;
using TeamCherry.Localization;

namespace CustomCrestMod
{
    public static class CustomCrestBuilder
    {
        private static ToolCrest _customCrest;
        private static HeroController.ConfigGroup _customConfigGroup;
        private static bool _isInitialized = false;
        private static HeroControllerConfig _cachedHybridConfig;
        public static bool IsInitialized => _isInitialized && _customCrest != null;
        private static GameObject _storedReaperDownAttack = null;
        private static GameObject _storedReaperDownAttackNew = null;
        private static GameObject _storedReaperDashStab = null;
        private static GameObject _storedReaperDashStabAlt = null;
        private static GameObject _storedWandererDashStab = null;
        private static GameObject _storedWandererDashStabAlt = null;

        public static HeroControllerConfig GetHybridConfig() => _cachedHybridConfig;

        public static void Initialize(ToolItemManager toolItemManager)
        {
            if (_isInitialized)
            {
                var refreshedConfig = CreateHybridConfig();
                if (refreshedConfig != null && _customCrest != null)
                {
                    var configField = AccessTools.Field(typeof(ToolCrest), "heroConfig") ??
                                    AccessTools.Field(typeof(ToolCrest), "<HeroConfig>k__BackingField");
                    configField?.SetValue(_customCrest, refreshedConfig);
                }
                return;
            }

            var neutralCrest = GetCrestByType(Plugin.NeutralAttackCrest.Value);
            var upCrest = GetCrestByType(Plugin.UpAttackCrest.Value);
            var downCrest = GetCrestByType(Plugin.DownAttackCrest.Value);

            if (neutralCrest?.HeroConfig == null || upCrest?.HeroConfig == null || downCrest?.HeroConfig == null)
            {
                Plugin.ModLogger.LogError("Source crests not found or configs are null!");
                return;
            }

            _customCrest = CreateCustomToolCrest();
            if (_customCrest == null)
            {
                Plugin.ModLogger.LogError("Failed to create custom crest!");
                return;
            }

            var hybridConfig = CreateHybridConfig();
            if (hybridConfig == null)
            {
                Plugin.ModLogger.LogError("Failed to create hybrid config!");
                return;
            }

            var heroConfigField = AccessTools.Field(typeof(ToolCrest), "heroConfig") ??
                                AccessTools.Field(typeof(ToolCrest), "<HeroConfig>k__BackingField");

            if (heroConfigField != null)
            {
                heroConfigField.SetValue(_customCrest, hybridConfig);
            }
            else
            {
                Plugin.ModLogger.LogError("Could not set HeroConfig - field not found!");
                return;
            }

            RegisterCustomCrest(toolItemManager);
            _isInitialized = true;
            Plugin.ModLogger.LogInfo("✓ Custom crest created successfully");
        }

        public static void ResetInitialization()
        {
            _isInitialized = false;
            _customCrest = null;
            _customConfigGroup = null;
            _storedReaperDownAttack = null;
            _storedReaperDownAttackNew = null;
        }

        private static ToolCrest CreateCustomToolCrest()
        {
            var crest = ScriptableObject.CreateInstance<ToolCrest>();
            crest.name = "CustomCrest";
            Plugin.ModLogger.LogInfo("--- Assigning Custom Sprites ---");

            var iconField = AccessTools.Field(typeof(ToolCrest), "crestSprite");
            var glowField = AccessTools.Field(typeof(ToolCrest), "crestGlow");
            var silhouetteField = AccessTools.Field(typeof(ToolCrest), "crestSilhouette");
            Plugin.ModLogger.LogInfo($"Finding 'crestSprite' field. Found? {iconField != null}");
            if (Plugin.CustomCrestSprite != null && iconField != null)
            {
                iconField.SetValue(crest, Plugin.CustomCrestSprite);
                Plugin.ModLogger.LogInfo("-> Assigned custom icon sprite.");
            }

            if (Plugin.CustomCrestSprite != null)
            {
                // This will now find the field and set the value correctly
                iconField?.SetValue(crest, Plugin.CustomCrestSprite);
            }

            if (Plugin.CustomCrestSilhouetteSprite != null)
            {
                silhouetteField?.SetValue(crest, Plugin.CustomCrestSilhouetteSprite);
            }

            InjectLocalizedStrings();

            var displayNameField = AccessTools.Field(typeof(ToolCrest), "displayName");
            var descriptionField = AccessTools.Field(typeof(ToolCrest), "description");
            var itemNamePrefixField = AccessTools.Field(typeof(ToolCrest), "itemNamePrefix");

            displayNameField?.SetValue(crest, new LocalisedString { Sheet = "Tools", Key = "CREST_CUSTOM_NAME" });
            descriptionField?.SetValue(crest, new LocalisedString { Sheet = "Tools", Key = "CREST_CUSTOM_DESC" });
            itemNamePrefixField?.SetValue(crest, new LocalisedString { Sheet = "Tools", Key = "CREST_CUSTOM_PREFIX" });

            var slots = BuildCustomSlots();
            var slotsField = AccessTools.Field(typeof(ToolCrest), "slots");
            slotsField?.SetValue(crest, slots);

            var unlockMethod = AccessTools.Method(typeof(ToolCrest), "Unlock");
            unlockMethod?.Invoke(crest, null);

            var isUnlockedField = AccessTools.Field(typeof(ToolCrest), "isUnlocked") ??
                                AccessTools.Field(typeof(ToolCrest), "<IsUnlocked>k__BackingField");
            isUnlockedField?.SetValue(crest, true);

            return crest;
        }

        private static void RewireAndInitFSMs(GameObject attackObject, HeroController heroController)
        {
            if (attackObject == null || heroController == null) return;

            var fsmComponents = attackObject.GetComponentsInChildren<PlayMakerFSM>(true); // Get the Unity components
            if (fsmComponents == null || fsmComponents.Length == 0) return;

            foreach (var fsmComponent in fsmComponents)
            {
                if (fsmComponent == null) continue;

                // --- CORRECTED ACCESS: Use the public .Fsm property ---
                Fsm fsmInstance = fsmComponent.Fsm; // Directly access the Fsm instance via the component's property
                if (fsmInstance == null) continue;
                // --- End Correction ---

                // --- IMPROVEMENT: Use FsmUtil to get/set variables ---
                // Try to get the "Hero" variable using the library's GetVariable method
                FsmGameObject? heroGoVar = fsmInstance.GetVariable<FsmGameObject>("Hero");
                if (heroGoVar == null)
                {
                    // If not found, try "Player"
                    heroGoVar = fsmInstance.GetVariable<FsmGameObject>("Player");
                }

                if (heroGoVar != null)
                {
                    // Use the library's SetVariable method
                    fsmInstance.SetVariable(heroGoVar.Name, heroController.gameObject);
                }
                else
                {
                    // Log if variable not found
                    Plugin.ModLogger.LogWarning($"Could not find 'Hero'/'Player' GameObject variable on FSM '{fsmComponent.FsmName}' in {attackObject.name}.");
                }
                // --- End Improvement ---

                // Initialize and Start (This part remains the same)
                if (!fsmInstance.Active)
                {
                    try
                    {
                        fsmInstance.Init(fsmComponent); // Still need to pass the component here
                        fsmInstance.Start();
                    }
                    catch (Exception ex)
                    {
                        Plugin.ModLogger.LogWarning($"Failed to init FSM on {attackObject.name}: {ex.Message}");
                    }
                }
            }
        }

        private static void InjectLocalizedStrings()
        {
            var languageType = typeof(Language);
            var currentSheetsField = languageType.GetField("_currentEntrySheets",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (currentSheetsField?.GetValue(null) is System.Collections.IDictionary sheets &&
                sheets.Contains("Tools"))
            {
                var toolsDict = sheets["Tools"] as System.Collections.IDictionary;
                if (toolsDict != null)
                {
                    toolsDict["CREST_CUSTOM_NAME"] = "Custom Crest";
                    toolsDict["CREST_CUSTOM_DESC"] = "Behold, the power of customization!";
                    toolsDict["CREST_CUSTOM_PREFIX"] = "Bound the Custom Crest of";
                }
            }
        }

        private static ToolCrest.SlotInfo[] BuildCustomSlots()
        {
            var slots = new List<ToolCrest.SlotInfo>();
            const float spacing = 1.8f; // Slightly increased horizontal spacing for the outer rows

            // --- Define precise Y-positions based on the new crest design ---
            const float yBlueRow = 3.2f;       // Y-pos for blue slots (well above the crest)
            const float yUpAttack = 1.6f;      // Y-pos for the top hole
            const float yNeutralAttack = 0f;   // Y-pos for the center hole
            const float yDownAttack = -1.6f;   // Y-pos for the bottom hole
            const float yYellowRow = -3.2f;    // Y-pos for yellow slots (well below the crest)

            // --- TOP ROW: Blue Slots (outside the crest) ---
            float blueStartX = -((Plugin.BlueSlots.Value - 1) * spacing) / 2f;
            for (int i = 0; i < Plugin.BlueSlots.Value; i++)
            {
                slots.Add(CreateSlot(blueStartX + i * spacing, yBlueRow, ToolItemType.Blue));
            }

            // --- UP ATTACK SLOT: Placed in the top hole of the crest ---
            // The loop handles if the slot exists, and places it at X=0 to be centered.
            for (int i = 0; i < Plugin.UpAttackSlots.Value; i++)
            {
                slots.Add(CreateSlot(0, yUpAttack,
                    Plugin.UpUseSkillSlots.Value ? ToolItemType.Skill : ToolItemType.Red, AttackToolBinding.Up));
            }

            // --- NEUTRAL ATTACK SLOT: Placed in the center hole of the crest ---
            for (int i = 0; i < Plugin.NeutralAttackSlots.Value; i++)
            {
                slots.Add(CreateSlot(0, yNeutralAttack,
                    Plugin.NeutralUseSkillSlots.Value ? ToolItemType.Skill : ToolItemType.Red, AttackToolBinding.Neutral));
            }

            // --- DOWN ATTACK SLOT: Placed in the bottom hole of the crest ---
            for (int i = 0; i < Plugin.DownAttackSlots.Value; i++)
            {
                slots.Add(CreateSlot(0, yDownAttack,
                    Plugin.DownUseSkillSlots.Value ? ToolItemType.Skill : ToolItemType.Red, AttackToolBinding.Down));
            }

            // --- BOTTOM ROW: Yellow Slots (outside the crest) ---
            float yellowStartX = -((Plugin.YellowSlots.Value - 1) * spacing) / 2f;
            for (int i = 0; i < Plugin.YellowSlots.Value; i++)
            {
                slots.Add(CreateSlot(yellowStartX + i * spacing, yYellowRow, ToolItemType.Yellow));
            }

            return ToolSlotNavigationHelper.ToFixedNavigation(slots.ToArray());
        }

        private static ToolCrest.SlotInfo CreateSlot(float x, float y, ToolItemType type, AttackToolBinding? binding = null)
        {
            var slot = new ToolCrest.SlotInfo
            {
                Position = new Vector2(x, y),
                Type = type,
                IsLocked = !Plugin.UnlockAllSlots.Value,
                NavUpIndex = -1,
                NavDownIndex = -1,
                NavLeftIndex = -1,
                NavRightIndex = -1,
                NavUpFallbackIndex = -1,
                NavDownFallbackIndex = -1,
                NavLeftFallbackIndex = -1,
                NavRightFallbackIndex = -1
            };

            if (binding.HasValue)
                slot.AttackBinding = binding.Value;

            return slot;
        }

        private static HeroControllerConfig CreateHybridConfig()
        {
            var neutralCrest = GetCrestByType(Plugin.NeutralAttackCrest.Value);
            var upCrest = GetCrestByType(Plugin.UpAttackCrest.Value);
            var downCrest = GetCrestByType(Plugin.DownAttackCrest.Value);
            var bindingCrest = GetCrestByType(Plugin.BindingActionCrest.Value) ?? neutralCrest;

            if (neutralCrest?.HeroConfig == null || upCrest?.HeroConfig == null || downCrest?.HeroConfig == null || bindingCrest?.HeroConfig == null)
            {
                Plugin.ModLogger.LogError("Cannot create hybrid config - source crests or configs are null!");
                return null;
            }

            // --- Create the correct base instance ---
            HeroControllerConfig hybridConfig;
            if (Plugin.BindingActionCrest.Value == CrestType.Warrior)
            {
                Plugin.ModLogger.LogInfo("Warrior binding detected. Creating HeroControllerConfigWarrior instance.");
                hybridConfig = ScriptableObject.CreateInstance<HeroControllerConfigWarrior>(); //
                // Start with ALL Warrior data. This ensures overrides and potentially different base values are present.
                Plugin.ModLogger.LogInfo("Base config: Copying ALL fields from Warrior");
                CopyAllConfigFields(bindingCrest.HeroConfig, hybridConfig);
            }
            else
            {
                hybridConfig = ScriptableObject.CreateInstance<HeroControllerConfig>();
                hybridConfig.name = "CustomCrest_HybridConfig";
                // Start with ALL Neutral data otherwise.
                Plugin.ModLogger.LogInfo($"Base config: Copying ALL fields from {Plugin.NeutralAttackCrest.Value}");
                CopyAllConfigFields(neutralCrest.HeroConfig, hybridConfig);
            }

            // --- Overwrite specific sections based on user choice ---

            // 1. NEUTRAL Attack Timing: Apply Neutral Crest's timing unless base was Warrior.
            //    (If base was Warrior, keep Warrior timing for rage calculations).
            if (Plugin.BindingActionCrest.Value != CrestType.Warrior)
            {
                Plugin.ModLogger.LogInfo($"Neutral Attack: Copying core timing fields from {Plugin.NeutralAttackCrest.Value}");
                CopyConfigFields(neutralCrest.HeroConfig, hybridConfig, GetCoreAttackTimingFields());
            }

            // 2. UP Attack Timing: Apply Up Crest's timing.
            Plugin.ModLogger.LogInfo($"Up Attack: Copying core timing fields from {Plugin.UpAttackCrest.Value}");
            CopyConfigFields(upCrest.HeroConfig, hybridConfig, GetCoreAttackTimingFields());
            //    Copy other specific Up Attack fields (if any).
            CopyConfigFields(upCrest.HeroConfig, hybridConfig, GetUpAttackFields());

            // 3. DOWN Attack Timing: Apply Down Crest's timing.
            Plugin.ModLogger.LogInfo($"Down Attack: Copying core timing fields from {Plugin.DownAttackCrest.Value}");
            CopyConfigFields(downCrest.HeroConfig, hybridConfig, GetCoreAttackTimingFields());
            //    Copy other specific Down Attack fields.
            CopyConfigFields(downCrest.HeroConfig, hybridConfig, GetDownAttackFields());

            // 4. BINDING Animation Fields: Apply Binding Crest's animation fields.
            CopyConfigFields(bindingCrest.HeroConfig, hybridConfig, GetBindingFields());

            // 5. DASH Specific Fields: Apply Binding Crest's dash fields if using binding dash.
            //    (Unless it's Warrior, handled above).
            if (Plugin.UseBindingCrestDash.Value && Plugin.BindingActionCrest.Value != CrestType.Warrior)
            {
                 Plugin.ModLogger.LogInfo($"Using Binding Crest dash ({Plugin.BindingActionCrest.Value}). Copying ONLY dash-specific fields.");
                 CopyConfigFields(bindingCrest.HeroConfig, hybridConfig, GetDashAttackFields());
            }

            // 6. WARRIOR Private Rage Fields: Copy these into the Warrior instance.
            if (hybridConfig is HeroControllerConfigWarrior warriorHybridConfig)
            {
                Plugin.ModLogger.LogInfo("Copying private Warrior rage fields.");
                var rageFields = typeof(HeroControllerConfigWarrior).GetFields(
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                    .Where(f => f.Name.StartsWith("rage"))
                    .Select(f => f.Name)
                    .ToArray();
                CopyConfigFields(bindingCrest.HeroConfig, warriorHybridConfig, rageFields);
            }

            // --- Merge Animations ---
            MergeAnimationsSelective(
                neutralCrest.HeroConfig,
                upCrest.HeroConfig,
                downCrest.HeroConfig,
                bindingCrest.HeroConfig, // Pass non-null version
                hybridConfig
            );

            _cachedHybridConfig = hybridConfig;
            return hybridConfig;
        }

        // Keep these helpers defined:
        private static string[] GetCoreAttackTimingFields() => new[] {
            "attackDuration", "quickAttackSpeedMult", "attackRecoveryTime",
            "attackCooldownTime", "quickAttackCooldownTime"
        };

        private static string[] GetDashAttackFields() => new[] {
            "dashStabSpeed", "dashStabTime", "forceShortDashStabBounce",
            "dashStabBounceJumpSpeed", "dashStabSteps", "canHarpoonDash"
        };

        // --- Make sure you still have this helper method from the previous step ---
        private static string[] GetWandererDashFields() => new[] {
            "wandererDashComboWindowTimer", // The field you found!
            "wandererDashSpeed",
            "wandererDashTime",
            "wandererDashInvulnTime",
            "wandererDashEndSpeed",
            "wandererDashEffectPrefab",
            "wandererDashAfterEffectPrefab"
            // We are adding these other likely candidates to ensure the
            // dash functions correctly with speed, duration, and effects.
        };

        private static void MergeAnimationsSelective(
            HeroControllerConfig neutralConfig,
            HeroControllerConfig upConfig,
            HeroControllerConfig downConfig,
            HeroControllerConfig bindingConfig,
            HeroControllerConfig targetConfig)
        {
            var animLibField = AccessTools.Field(typeof(HeroControllerConfig), "heroAnimOverrideLib");

            var neutralAnimLib = animLibField?.GetValue(neutralConfig) as tk2dSpriteAnimation;
            var upAnimLib = animLibField?.GetValue(upConfig) as tk2dSpriteAnimation;
            var downAnimLib = animLibField?.GetValue(downConfig) as tk2dSpriteAnimation;
            var bindingAnimLib = bindingConfig != null ? animLibField?.GetValue(bindingConfig) as tk2dSpriteAnimation : null;

            if (neutralAnimLib == null)
            {
                var heroAnimator = HeroController.instance?.GetComponent<tk2dSpriteAnimator>();
                neutralAnimLib = heroAnimator?.Library ?? upAnimLib ?? downAnimLib;

                if (neutralAnimLib == null)
                {
                    Plugin.ModLogger.LogError("No animation library available!");
                    return;
                }
            }

            var mergedLib = UnityEngine.Object.Instantiate(neutralAnimLib);
            mergedLib.name = "CustomCrest_MergedAnimations";
            var clipsList = mergedLib.clips.ToList();

            // Merge animations
            if (upAnimLib != null && Plugin.UpAttackCrest.Value != Plugin.NeutralAttackCrest.Value)
            {
                var upKeywords = new[] { "up", "upslash", "upper" };
                MergeClipsFromSourceByKeywords(clipsList, upAnimLib, upKeywords);
            }

            if (downAnimLib != null && Plugin.DownAttackCrest.Value != Plugin.NeutralAttackCrest.Value)
            {
                var downSlashTypeField = AccessTools.Field(typeof(HeroControllerConfig), "downSlashType");
                var downSlashType = (HeroControllerConfig.DownSlashTypes)downSlashTypeField?.GetValue(downConfig);
                bool isFSMDriven = downSlashType == HeroControllerConfig.DownSlashTypes.Custom;

                if (isFSMDriven)
                    MergeAllClipsFromSource(clipsList, downAnimLib);
                else
                {
                    var downKeywords = new[] { "down", "spike", "drill", "grind", "recovery", "pogo", "bounce", "rebound" };
                    MergeClipsFromSourceByKeywords(clipsList, downAnimLib, downKeywords);
                }
            }

            if (bindingAnimLib != null && Plugin.BindingActionCrest.Value != Plugin.NeutralAttackCrest.Value)
            {
                if (Plugin.BindingActionCrest.Value == CrestType.Warrior)
                    MergeAllClipsFromSource(clipsList, bindingAnimLib);
                else
                {
                    var bindKeywords = new[] { "bind", "heal", "focus", "charge", "burst", "bindcharge" };
                    MergeClipsFromSourceByKeywords(clipsList, bindingAnimLib, bindKeywords);
                }
            }

            var newAnimLib = CreateNewAnimationLibrary(clipsList);
            if (newAnimLib != null)
                animLibField?.SetValue(targetConfig, newAnimLib);
            else
                Plugin.ModLogger.LogError("Failed to create new animation library!");
        }

        private static tk2dSpriteAnimation CreateNewAnimationLibrary(List<tk2dSpriteAnimationClip> clips)
        {
            try
            {
                var animLibObj = new GameObject("CustomCrest_AnimationLibrary");
                UnityEngine.Object.DontDestroyOnLoad(animLibObj);

                var newAnimLib = animLibObj.AddComponent<tk2dSpriteAnimation>();
                newAnimLib.clips = clips.ToArray();

                // Test initialization
                if (clips.Count > 0 && clips[0] != null)
                    newAnimLib.GetClipByName(clips[0].name);

                return newAnimLib;
            }
            catch (Exception ex)
            {
                Plugin.ModLogger.LogError($"Error creating animation library: {ex.Message}");
                return null;
            }
        }

        private static void UpdateAnimatorsToUseCustomLibrary(GameObject root, tk2dSpriteAnimation customLibrary)
        {
            if (root == null || customLibrary == null) return;

            var animators = root.GetComponentsInChildren<tk2dSpriteAnimator>(true);
            foreach (var animator in animators)
            {
                if (animator != null && animator.Library != customLibrary)
                    animator.Library = customLibrary;
            }
        }

        private static void MergeClipsFromSourceByKeywords(
            List<tk2dSpriteAnimationClip> targetClips,
            tk2dSpriteAnimation sourceLib,
            string[] keywords)
        {
            foreach (var clip in sourceLib.clips)
            {
                if (clip == null || string.IsNullOrEmpty(clip.name)) continue;

                bool matches = keywords.Any(kw => clip.name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!matches) continue;

                var existingIndex = targetClips.FindIndex(c => c != null && c.name == clip.name);
                if (existingIndex >= 0)
                    targetClips[existingIndex] = clip;
                else
                    targetClips.Add(clip);
            }
        }

        private static void MergeAllClipsFromSource(List<tk2dSpriteAnimationClip> targetClips, tk2dSpriteAnimation sourceLib)
        {
            foreach (var clip in sourceLib.clips)
            {
                if (clip == null || string.IsNullOrEmpty(clip.name)) continue;

                var existingIndex = targetClips.FindIndex(c => c != null && c.name == clip.name);
                if (existingIndex >= 0)
                    targetClips[existingIndex] = clip;
                else
                    targetClips.Add(clip);
            }
        }

        private static void CopyAllConfigFields(HeroControllerConfig source, HeroControllerConfig target)
        {
            var fields = typeof(HeroControllerConfig).GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.Name == "heroAnimOverrideLib") continue;

                try
                {
                    field.SetValue(target, field.GetValue(source));
                }
                catch { }
            }
        }

        private static void CopyConfigFields(HeroControllerConfig source, HeroControllerConfig target, string[] fieldNames)
        {
            foreach (var fieldName in fieldNames)
            {
                var field = AccessTools.Field(typeof(HeroControllerConfig), fieldName);
                try
                {
                    if (field != null)
                        field.SetValue(target, field.GetValue(source));
                }
                catch { }
            }
        }

        private static string[] GetNeutralAttackFields() => new[] {
            "attackDuration", "quickAttackSpeedMult", "attackRecoveryTime",
            "attackCooldownTime", "quickAttackCooldownTime", "canTurnWhileSlashing",
            "chargeSlashRecoils", "chargeSlashLungeSpeed", "chargeSlashLungeDeceleration",
            "chargeSlashChain", "wallSlashSlowdown", "dashStabSpeed", "dashStabTime",
            "forceShortDashStabBounce", "dashStabBounceJumpSpeed", "dashStabSteps", "canHarpoonDash"
        };

        private static string[] GetUpAttackFields() => new string[] { };

        private static string[] GetDownAttackFields() => new[] {
            "downSlashType", "downSlashEvent", "downspikeAnticTime", "downspikeTime",
            "downspikeSpeed", "downspikeRecoveryTime", "downspikeBurstEffect", "downspikeThrusts"
        };

        private static string[] GetBindingFields() => new[] {
            "canBind", "bindGainHealAmount", "bindOrbPrefab", "bindUnlockAnimTrigger",
            "bindChargeTime", "bindCooldownTime"
        };

        public static ToolCrest GetCrestByType(CrestType crestType)
        {
            string internalName = crestType.GetInternalName();

            if (crestType == CrestType.Hunter)
            {
                if (HeroController.instance != null)
                {
                    var configsField = AccessTools.Field(typeof(HeroController), "configs");
                    var configs = configsField?.GetValue(HeroController.instance) as HeroController.ConfigGroup[];

                    var hunterGroup = configs?.FirstOrDefault(g =>
                        g?.ActiveRoot != null &&
                        (g.ActiveRoot.name == "Default" || g.ActiveRoot.name.Contains("Hunter")));

                    if (hunterGroup?.Config != null)
                    {
                        var dummyHunter = ScriptableObject.CreateInstance<ToolCrest>();
                        dummyHunter.name = "Hunter_Dummy";
                        var heroConfigField = AccessTools.Field(typeof(ToolCrest), "heroConfig");
                        heroConfigField?.SetValue(dummyHunter, hunterGroup.Config);
                        return dummyHunter;
                    }
                }

                Plugin.ModLogger.LogError("Could not get Hunter config!");
                return null;
            }

            var crest = ToolItemManager.GetCrestByName(internalName) ?? ToolItemManager.GetCrestByName(crestType.ToString());

            // Try alternative names
            if (crest == null)
            {
                switch (crestType)
                {
                    case CrestType.Reaper:
                        crest = ToolItemManager.GetCrestByName("Scythe");
                        break;
                    case CrestType.Spell:
                        crest = ToolItemManager.GetCrestByName("Shaman");
                        break;
                    case CrestType.Toolmaster:
                        crest = ToolItemManager.GetCrestByName("Architect");
                        break;
                }
            }

            if (crest != null && crest.HeroConfig == null)
            {
                var configProp = AccessTools.Property(typeof(ToolCrest), "HeroConfig");
                configProp?.GetValue(crest);
            }

            return crest;
        }

        public static void UpdateBindingFSM()
        {
            try
            {
                if (_customCrest == null || HeroController.instance == null) return;

                var bindFsm = HeroController.instance.gameObject.LocateMyFSM("Bind");
                if (bindFsm == null) return;

                var warriorVar = bindFsm.FsmVariables.FindFsmBool("Warrior");
                var witchVar = bindFsm.FsmVariables.FindFsmBool("Witch");
                var shamanVar = bindFsm.FsmVariables.FindFsmBool("Shaman");

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
            catch (Exception ex)
            {
                Plugin.ModLogger.LogError($"Error in UpdateBindingFSM: {ex.Message}");
            }
        }

        private static void RegisterCustomCrest(ToolItemManager toolItemManager)
        {
            try
            {
                var crestsField = AccessTools.Field(typeof(ToolItemManager), "crestList");
                var crestList = crestsField?.GetValue(toolItemManager);
                if (crestList == null) return;

                var listType = crestList.GetType();
                var addMethod = listType.GetMethod("Add");

                if (addMethod != null)
                {
                    addMethod.Invoke(crestList, new object[] { _customCrest });
                    return;
                }

                // Try array/list fallback
                var allFields = listType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in allFields)
                {
                    if (field.FieldType == typeof(ToolCrest[]))
                    {
                        var oldArray = (ToolCrest[])field.GetValue(crestList);
                        if (oldArray != null)
                        {
                            var newArray = new ToolCrest[oldArray.Length + 1];
                            Array.Copy(oldArray, newArray, oldArray.Length);
                            newArray[oldArray.Length] = _customCrest;
                            field.SetValue(crestList, newArray);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLogger.LogError($"Failed to register custom crest: {ex.Message}");
            }
        }

        public static void CreateCustomConfigGroup(HeroController heroController)
        {
            var configsField = AccessTools.Field(typeof(HeroController), "configs");
            var configs = configsField?.GetValue(heroController) as HeroController.ConfigGroup[];
            if (configs == null) return;

            var existingCustomGroup = configs.FirstOrDefault(g =>
                g?.ActiveRoot != null && g.ActiveRoot && g.ActiveRoot.name == "CustomCrest");

            if (existingCustomGroup?.ActiveRoot != null)
                UnityEngine.Object.Destroy(existingCustomGroup.ActiveRoot);

            var hunterGroup = configs.FirstOrDefault(g =>
                g?.ActiveRoot != null && (g.ActiveRoot.name.Contains("Hunter") || g.ActiveRoot.name.Contains("Default")));

            if (hunterGroup == null) return;

            var customRoot = new GameObject("CustomCrest");
            customRoot.transform.SetParent(hunterGroup.ActiveRoot.transform.parent);
            customRoot.transform.localPosition = Vector3.zero;
            customRoot.transform.localRotation = Quaternion.identity;
            customRoot.transform.localScale = Vector3.one;
            customRoot.SetActive(true);

            _customConfigGroup = new HeroController.ConfigGroup();
            var activeRootField = AccessTools.Field(typeof(HeroController.ConfigGroup), "ActiveRoot");
            var configField = AccessTools.Field(typeof(HeroController.ConfigGroup), "Config");
            activeRootField?.SetValue(_customConfigGroup, customRoot);
            configField?.SetValue(_customConfigGroup, _customCrest.HeroConfig);

            var newConfigs = new HeroController.ConfigGroup[configs.Length + 1];
            Array.Copy(configs, newConfigs, configs.Length);
            newConfigs[configs.Length] = _customConfigGroup;
            configsField?.SetValue(heroController, newConfigs);

            InstantiateAttackGameObjects(heroController);

            if (Plugin.DownAttackCrest.Value == CrestType.Reaper)
            {
                foreach (Transform child in _customConfigGroup.ActiveRoot.transform)
                {
                    if (child.name == "DownSlash New")
                    {
                        child.gameObject.SetActive(true);
                        var fsm = child.GetComponent<PlayMakerFSM>();
                        if (fsm?.Fsm != null && !fsm.Fsm.Active)
                        {
                            fsm.Fsm.Init(fsm);
                            fsm.Fsm.Start();
                        }
                        break;
                    }
                }
            }
        }

        private static void InstantiateAttackGameObjects(HeroController heroController)
        {
            var configs = AccessTools.Field(typeof(HeroController), "configs")
                ?.GetValue(heroController) as HeroController.ConfigGroup[];
            if (configs == null) return;

            var neutralGroup = FindConfigGroupForCrest(configs, Plugin.NeutralAttackCrest.Value);
            var upGroup = FindConfigGroupForCrest(configs, Plugin.UpAttackCrest.Value);
            var downGroup = FindConfigGroupForCrest(configs, Plugin.DownAttackCrest.Value);

            if (neutralGroup?.ActiveRoot != null)
            {
                neutralGroup.ActiveRoot.SetActive(true);
                InstantiateNeutralAttacks(neutralGroup, _customConfigGroup, heroController); // Pass heroController
            }

            if (upGroup?.ActiveRoot != null)
            {
                upGroup.ActiveRoot.SetActive(true);
                InstantiateUpAttacks(upGroup, _customConfigGroup, heroController); // Pass heroController
            }

            if (downGroup?.ActiveRoot != null)
            {
                downGroup.ActiveRoot.SetActive(true);
                InstantiateDownAttacks(downGroup, _customConfigGroup); // Your logic is unchanged
            }

            // Copy Warrior rage attacks if needed
            if (Plugin.BindingActionCrest.Value == CrestType.Warrior)
            {
                var warriorConfig = GetCrestConfig(CrestType.Warrior);
                if (warriorConfig?.ActiveRoot != null)
                {
                    warriorConfig.ActiveRoot.SetActive(true);
                    foreach (Transform child in warriorConfig.ActiveRoot.transform)
                    {
                        if (child.name.Contains("Rage") || child.name.Contains("Spin") || child.name == "DashSlash")
                        {
                            var copy = UnityEngine.Object.Instantiate(child.gameObject, _customConfigGroup.ActiveRoot.transform);
                            copy.name = child.name;
                            copy.transform.localPosition = Vector3.zero;
                            copy.transform.localRotation = Quaternion.identity;
                            copy.transform.localScale = Vector3.one;

                            RewireAndInitFSMs(copy, heroController); // Use new FSM re-wirer

                            copy.SetActive(child.gameObject.activeSelf);
                            if (child.name == "DashSlash")
                            {
                                var dashStabField = AccessTools.Field(typeof(HeroController.ConfigGroup), "DashStab");
                                dashStabField?.SetValue(_customConfigGroup, copy);
                            }
                        }
                    }
                }
            }
            else
            {
                // --- THIS IS THE IMPROVEMENT (Applying your cache pattern) ---
                HeroController.ConfigGroup dashSource = null;
                if (Plugin.UseBindingCrestDash.Value)
                    dashSource = GetCrestConfig(Plugin.BindingActionCrest.Value);
                dashSource = dashSource ?? neutralGroup;

                if (dashSource != null)
                {
                    dashSource.ActiveRoot?.SetActive(true);
                    CrestType dashType = GetCrestTypeFromGroup(dashSource);

                    // 1. Instantiate DashStab (from cache if applicable)
                    GameObject dashStabCopy = null;
                    GameObject dashStabPrefab = GetDashStabPrefab(dashType, dashSource.DashStab); // Use helper to get cache

                    if (dashStabPrefab != null)
                        dashStabCopy = InstantiateGameObject(dashStabPrefab, _customConfigGroup.ActiveRoot.transform);

                    if(dashStabCopy != null)
                    {
                        RewireAndInitFSMs(dashStabCopy, heroController); // Use new FSM re-wirer
                        var dashStabField = AccessTools.Field(typeof(HeroController.ConfigGroup), "DashStab");
                        dashStabField?.SetValue(_customConfigGroup, dashStabCopy);
                    }

                    // 2. Instantiate DashStabAlt (from cache if applicable)
                    GameObject dashStabAltCopy = null;
                    GameObject dashStabAltPrefab = GetDashStabAltPrefab(dashType, dashSource.DashStabAlt); // Use helper to get cache

                    if (dashStabAltPrefab != null)
                        dashStabAltCopy = InstantiateGameObject(dashStabAltPrefab, _customConfigGroup.ActiveRoot.transform);

                    if (dashStabAltCopy != null)
                    {
                        RewireAndInitFSMs(dashStabAltCopy, heroController); // Use new FSM re-wirer
                        var dashStabAltField = AccessTools.Field(typeof(HeroController.ConfigGroup), "DashStabAlt");
                        dashStabAltField?.SetValue(_customConfigGroup, dashStabAltCopy);
                    }
                }
            }

            // Update all animators... (your logic is unchanged)
            var hybridConfig = GetHybridConfig();
            if (hybridConfig != null)
            {
                var animLibField = typeof(HeroControllerConfig).GetField("heroAnimOverrideLib", // Use the field from your code
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (animLibField?.GetValue(hybridConfig) is tk2dSpriteAnimation animLib)
                    UpdateAnimatorsToUseCustomLibrary(_customConfigGroup.ActiveRoot, animLib);
            }
        }

        private static void InstantiateNeutralAttacks(HeroController.ConfigGroup sourceGroup, HeroController.ConfigGroup targetGroup, HeroController heroController)
        {
            var mergedLib = _cachedHybridConfig != null
                ? AccessTools.Field(typeof(HeroControllerConfig), "heroAnimOverrideLib")?.GetValue(_cachedHybridConfig) as tk2dSpriteAnimation
                : null;

            // Pass heroController to the re-wirer
            InstantiateAndSetAttack(sourceGroup.NormalSlashObject, targetGroup, "NormalSlashObject", "NormalSlash", "NormalSlashDamager", heroController);
            InstantiateAndSetAttack(sourceGroup.AlternateSlashObject, targetGroup, "AlternateSlashObject", "AlternateSlash", "AlternateSlashDamager", heroController);
            InstantiateAndSetAttack(sourceGroup.WallSlashObject, targetGroup, "WallSlashObject", "WallSlash", "WallSlashDamager", heroController);

            if (!Plugin.UseBindingCrestDash.Value)
            {
                // This is now handled by the main InstantiateAttackGameObjects method
            }

            if (sourceGroup.ChargeSlash != null)
            {
                var copy = InstantiateGameObject(sourceGroup.ChargeSlash, targetGroup.ActiveRoot.transform, false);
                RewireAndInitFSMs(copy, heroController); // Use new re-wirer
                var chargeSlashField = AccessTools.Field(typeof(HeroController.ConfigGroup), "ChargeSlash");
                chargeSlashField?.SetValue(targetGroup, copy);
            }

            if (sourceGroup.TauntSlash != null)
            {
                var copy = InstantiateGameObject(sourceGroup.TauntSlash, targetGroup.ActiveRoot.transform, false);
                RewireAndInitFSMs(copy, heroController); // Use new re-wirer
                var tauntSlashField = AccessTools.Field(typeof(HeroController.ConfigGroup), "TauntSlash");
                tauntSlashField?.SetValue(targetGroup, copy);
            }
        }

        private static void InstantiateUpAttacks(HeroController.ConfigGroup sourceGroup, HeroController.ConfigGroup targetGroup, HeroController heroController)
        {
            if (sourceGroup.UpSlashObject != null)
            {
                var copy = UnityEngine.Object.Instantiate(sourceGroup.UpSlashObject, targetGroup.ActiveRoot.transform);
                copy.name = sourceGroup.UpSlashObject.name;
                RewireAndInitFSMs(copy, heroController); // Use new re-wirer

                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), "UpSlashObject");
                field?.SetValue(targetGroup, copy);

                SetComponentProperties(copy, targetGroup, "UpSlash", "UpSlashDamager");
            }

            if (sourceGroup.AltUpSlashObject != null)
            {
                var copy = UnityEngine.Object.Instantiate(sourceGroup.AltUpSlashObject, targetGroup.ActiveRoot.transform);
                copy.name = sourceGroup.AltUpSlashObject.name;
                RewireAndInitFSMs(copy, heroController); // Use new re-wirer

                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), "AltUpSlashObject");
                field?.SetValue(targetGroup, copy);

                SetComponentProperties(copy, targetGroup, "AltUpSlash", "AltUpSlashDamager");
            }
        }
        private static void InstantiateDownAttacks(HeroController.ConfigGroup sourceGroup, HeroController.ConfigGroup targetGroup)
        {
            bool isReaper = sourceGroup.ActiveRoot.name.Contains("Scythe") || sourceGroup.ActiveRoot.name.Contains("Reaper");

            if (isReaper && _storedReaperDownAttack != null)
            {
                var copy = UnityEngine.Object.Instantiate(_storedReaperDownAttack, targetGroup.ActiveRoot.transform);
                copy.name = _storedReaperDownAttack.name;
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), "DownSlashObject");
                field?.SetValue(targetGroup, copy);
                SetDownAttackComponents(copy, targetGroup, false);

                if (_storedReaperDownAttackNew != null)
                    UnityEngine.Object.Instantiate(_storedReaperDownAttackNew, targetGroup.ActiveRoot.transform);

                return;
            }

            bool sourceWasActive = sourceGroup.ActiveRoot.activeSelf;
            if (!sourceWasActive)
                sourceGroup.ActiveRoot.SetActive(true);

            Transform newDownSlash = null;
            for (int i = 0; i < sourceGroup.ActiveRoot.transform.childCount; i++)
            {
                var child = sourceGroup.ActiveRoot.transform.GetChild(i);
                if (child.name == "DownSlash New")
                {
                    newDownSlash = child;
                    break;
                }
            }

            if (sourceGroup.DownSlashObject != null)
            {
                var copy = InstantiateGameObject(sourceGroup.DownSlashObject, targetGroup.ActiveRoot.transform);
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), "DownSlashObject");
                field?.SetValue(targetGroup, copy);
                SetDownAttackComponents(copy, targetGroup, false);

                if (copy.GetComponent<PlayMakerFSM>() != null)
                    ConfigureFSMDownAttack(copy);

                if (isReaper && _storedReaperDownAttack == null)
                    _storedReaperDownAttack = sourceGroup.DownSlashObject;
            }

            if (newDownSlash != null)
            {
                var newCopy = InstantiateGameObject(newDownSlash.gameObject, targetGroup.ActiveRoot.transform);
                if (newCopy.GetComponent<PlayMakerFSM>() != null)
                {
                    ConfigureFSMDownAttack(newCopy);
                    newCopy.GetComponent<PlayMakerFSM>()?.Fsm.Start();
                }
            }

            if (sourceGroup.AltDownSlashObject != null)
            {
                var copy = InstantiateGameObject(sourceGroup.AltDownSlashObject, targetGroup.ActiveRoot.transform);
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), "AltDownSlashObject");
                field?.SetValue(targetGroup, copy);
                SetDownAttackComponents(copy, targetGroup, true);

                if (copy.GetComponent<PlayMakerFSM>() != null)
                    ConfigureFSMDownAttack(copy);
            }

            if (!sourceWasActive)
                sourceGroup.ActiveRoot.SetActive(false);
        }

        private static GameObject InstantiateGameObject(GameObject source, Transform parent, bool active = true)
        {
            var copy = UnityEngine.Object.Instantiate(source, parent);
            copy.name = source.name;
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            copy.SetActive(active);
            return copy;
        }

        private static void InstantiateAndSetAttack(GameObject source, HeroController.ConfigGroup targetGroup,
            string fieldName, string slashProp, string damagerProp, HeroController heroController)
        {
            if (source == null) return;

            var copy = InstantiateGameObject(source, targetGroup.ActiveRoot.transform);
            RewireAndInitFSMs(copy, heroController); // Use new re-wirer
            var field = AccessTools.Field(typeof(HeroController.ConfigGroup), fieldName);
            field?.SetValue(targetGroup, copy);
            SetComponentProperties(copy, targetGroup, slashProp, damagerProp);
        }

        private static void UpdateAnimatorLibrary(GameObject obj, tk2dSpriteAnimation library, bool includeChildren = false)
        {
            if (library == null) return;

            var animator = obj.GetComponent<tk2dSpriteAnimator>();
            if (animator != null)
                animator.Library = library;

            if (includeChildren)
            {
                foreach (Transform child in obj.transform)
                {
                    var childAnimator = child.GetComponent<tk2dSpriteAnimator>();
                    if (childAnimator != null)
                        childAnimator.Library = library;
                }
            }
        }

        private static void ConfigureFSMDownAttack(GameObject downAttackObj)
        {
            var fsms = downAttackObj.GetComponents<PlayMakerFSM>();
            foreach (var fsm in fsms)
            {
                if (fsm == null) continue;
                if (!fsm.enabled) fsm.enabled = true;
                if (fsm.Fsm != null && !fsm.Fsm.Active)
                {
                    try { fsm.Fsm.Init(fsm); }
                    catch { }
                }
            }
        }

        private static void SetComponentProperties(GameObject obj, HeroController.ConfigGroup targetGroup, string slashProp, string damagerProp)
        {
            var nailSlash = obj.GetComponent<NailSlash>();
            var damager = obj.GetComponent<DamageEnemies>();

            if (nailSlash != null)
            {
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), $"<{slashProp}>k__BackingField");
                field?.SetValue(targetGroup, nailSlash);
            }

            if (damager != null)
            {
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), $"<{damagerProp}>k__BackingField");
                field?.SetValue(targetGroup, damager);
            }
        }

        private static void SetDownAttackComponents(GameObject obj, HeroController.ConfigGroup targetGroup, bool isAlt)
        {
            var downspike = obj.GetComponent<Downspike>();
            var nailSlash = obj.GetComponent<NailSlash>();
            var damager = obj.GetComponent<DamageEnemies>();

            string prefix = isAlt ? "Alt" : "";

            if (downspike != null)
            {
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), $"<{prefix}Downspike>k__BackingField");
                field?.SetValue(targetGroup, downspike);
            }

            if (nailSlash != null)
            {
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), $"<{prefix}DownSlash>k__BackingField");
                field?.SetValue(targetGroup, nailSlash);
            }

            if (damager != null)
            {
                var field = AccessTools.Field(typeof(HeroController.ConfigGroup), $"<{prefix}DownSlashDamager>k__BackingField");
                field?.SetValue(targetGroup, damager);
            }
        }

        private static HeroController.ConfigGroup FindConfigGroupForCrest(HeroController.ConfigGroup[] groups, CrestType crestType)
        {
            string internalName = crestType.GetInternalName();
            return groups.FirstOrDefault(g =>
                g?.ActiveRoot != null &&
                g.ActiveRoot.name.Equals(internalName, StringComparison.OrdinalIgnoreCase));
        }

        public static ToolCrest GetCustomCrest() => _customCrest;
        public static HeroController.ConfigGroup GetCustomConfigGroup() => _customConfigGroup;

        public static HeroController.ConfigGroup GetCrestConfig(CrestType crestType)
        {
            if (HeroController.instance == null) return null;

            var configsField = AccessTools.Field(typeof(HeroController), "configs");
            var configs = configsField?.GetValue(HeroController.instance) as HeroController.ConfigGroup[];
            if (configs == null) return null;

            string targetName = GetInternalCrestName(crestType);
            var result = configs.FirstOrDefault(config =>
                config?.ActiveRoot?.name != null &&
                (config.ActiveRoot.name == targetName || config.ActiveRoot.name.Contains(targetName)));

            if (result == null)
            {
                var altNames = GetAlternativeCrestNames(crestType);
                foreach (var altName in altNames)
                {
                    result = configs.FirstOrDefault(config =>
                        config?.ActiveRoot?.name != null && config.ActiveRoot.name.Contains(altName));
                    if (result != null) break;
                }
            }

            return result;
        }

        private static CrestType GetCrestTypeFromGroup(HeroController.ConfigGroup group)
        {
            if (group.ActiveRoot.name.Contains("Scythe") || group.ActiveRoot.name.Contains("Reaper")) return CrestType.Reaper;
            if (group.ActiveRoot.name.Contains("Wanderer")) return CrestType.Wanderer;
            // Add other crests as needed
            return CrestType.Hunter;
        }

        public static void RefreshAttackGameObjects(HeroController heroController)
        {
            if (_customConfigGroup?.ActiveRoot == null) return;

            for (int i = _customConfigGroup.ActiveRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = _customConfigGroup.ActiveRoot.transform.GetChild(i);
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            InstantiateAttackGameObjects(heroController);
        }

        private static GameObject GetDashStabPrefab(CrestType type, GameObject liveObject)
        {
            if (type == CrestType.Reaper)
            {
                if (_storedReaperDashStab != null) return _storedReaperDashStab;
                if (liveObject != null) _storedReaperDashStab = liveObject;
                return _storedReaperDashStab;
            }
            if (type == CrestType.Wanderer)
            {
                if (_storedWandererDashStab != null) return _storedWandererDashStab;
                if (liveObject != null) _storedWandererDashStab = liveObject;
                return _storedWandererDashStab;
            }
            return liveObject; // Default for Hunter, etc.
        }

        private static GameObject GetDashStabAltPrefab(CrestType type, GameObject liveObject)
        {
            if (type == CrestType.Reaper)
            {
                if (_storedReaperDashStabAlt != null) return _storedReaperDashStabAlt;
                if (liveObject != null) _storedReaperDashStabAlt = liveObject;
                return _storedReaperDashStabAlt;
            }
            if (type == CrestType.Wanderer)
            {
                if (_storedWandererDashStabAlt != null) return _storedWandererDashStabAlt;
                if (liveObject != null) _storedWandererDashStabAlt = liveObject;
                return _storedWandererDashStabAlt;
            }
            return liveObject; // Default for Hunter, etc.
        }

        private static string GetInternalCrestName(CrestType crestType) => crestType switch
        {
            CrestType.Hunter => "Default",
            CrestType.Wanderer => "Wanderer",
            CrestType.Warrior => "Warrior",
            CrestType.Reaper => "Scythe",
            CrestType.Witch => "Witch",
            CrestType.Toolmaster => "Toolmaster",
            CrestType.Spell => "Spell",
            _ => crestType.ToString()
        };

        private static string[] GetAlternativeCrestNames(CrestType crestType) => crestType switch
        {
            CrestType.Hunter => new[] { "Default", "Hunter" },
            CrestType.Reaper => new[] { "Scythe", "Reaper" },
            CrestType.Spell => new[] { "Shaman", "Spell" },
            CrestType.Toolmaster => new[] { "Architect", "Toolmaster" },
            _ => new[] { crestType.ToString() }
        };

    }


}