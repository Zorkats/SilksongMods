// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CrestComposition;
using Needleforge;
using Needleforge.Data;
using TeamCherry.Localization;
using UnityEngine;

namespace CustomCrestMod
{
    public static class CustomCrestBuilder
    {
        public const string CustomCrestName = "CustomCrest";
        public const string LocalizationSheet = "Tools";
        public const string NameKey = "CREST_CUSTOM_NAME";
        public const string DescriptionKey = "CREST_CUSTOM_DESC";
        public const string PrefixKey = "CREST_CUSTOM_PREFIX";

        private static CrestData? _crestData;
        private static CrestMovesetComposer? _composer;

        public static CrestData? CrestData
        {
            get
            {
                return _crestData;
            }
        }

        public static CrestMovesetComposer? Composer
        {
            get
            {
                return _composer;
            }
        }

        public static bool IsInitialized
        {
            get
            {
                return _crestData != null && _composer != null;
            }
        }

        public static void RegisterWithNeedleforge()
        {
            if (_crestData != null)
            {
                return;
            }

            LocalisedString displayName = new LocalisedString
            {
                Sheet = LocalizationSheet,
                Key = NameKey
            };

            LocalisedString description = new LocalisedString
            {
                Sheet = LocalizationSheet,
                Key = DescriptionKey
            };

            _crestData = NeedleforgePlugin.AddCrest(
                CustomCrestName,
                displayName,
                description,
                Plugin.CustomCrestSprite,
                Plugin.CustomCrestSilhouetteSprite);

            _crestData.UnlockedAtStart = true;
            RebuildSlots(_crestData);

            // Neutral, up, dash and bind selections can name other mods' crests, so Plugin.Start
            // applies them once every plugin has registered its crests.
            _composer = new CrestMovesetComposer(
                _crestData,
                VanillaCrestType.Hunter,
                VanillaCrestType.Hunter,
                Plugin.DownAttackCrest.Value.ToVanillaCrestType(),
                Plugin.ModLogger);
            _composer.OnMovesetInitialized += OnMovesetInitialized;

            Plugin.ModLogger?.LogInfo($"Registered '{CustomCrestName}' with Needleforge.");
        }

        public static void ApplyNeutralAttackSelection(string sourceId)
        {
            _composer?.SetNeutralAttack(sourceId);
        }

        public static void ApplyUpAttackSelection(string sourceId)
        {
            _composer?.SetUpAttack(sourceId);
        }

        public static void ApplyDashAttackSelection(string sourceId)
        {
            _composer?.SetDashAttack(sourceId);
        }

        public static void ApplyBindOutcomeSelection(string outcomeId)
        {
            _composer?.SetBindOutcome(outcomeId);
        }

        public static void ApplyDownAttackSelection(CrestType crestType)
        {
            if (_composer == null)
            {
                return;
            }

            _composer.SetDownAttack(crestType.ToVanillaCrestType());
        }

        public static void RebuildSlots(CrestData crestData)
        {
            if (crestData == null)
            {
                return;
            }

            crestData.slots.Clear();
            ToolCrest.SlotInfo[] configuredSlots = BuildCustomSlots();
            foreach (ToolCrest.SlotInfo slot in configuredSlots)
            {
                crestData.slots.Add(slot);
            }
        }

        // Needleforge copies the slot list into the ToolCrest once, so a live change updates both.
        // Equipped tools are stored by slot index, so they move to matching slots of the new layout.
        public static void ApplySlotLayoutChange()
        {
            if (_crestData == null)
            {
                return;
            }

            ToolCrest? toolCrest = _crestData.ToolCrest;
            ToolCrest.SlotInfo[]? oldSlots = toolCrest != null ? toolCrest.slots : null;

            RebuildSlots(_crestData);
            if (toolCrest == null)
            {
                return;
            }

            toolCrest.slots = _crestData.slots.ToArray();

            if (oldSlots != null && HeroController.instance != null && PlayerData.instance != null)
            {
                RemapEquippedTools(oldSlots, toolCrest.slots);
            }
        }

        // The config can change while the game is closed, leaving a saved tool at an index its slot
        // type no longer matches. Runs on every load and does nothing to a layout that still fits.
        public static void ReconcileSavedTools()
        {
            if (_crestData?.ToolCrest == null ||
                PlayerData.instance == null ||
                ManagerSingleton<ToolItemManager>.Instance == null)
            {
                return;
            }

            ToolCrestsData.Data data = PlayerData.instance.ToolEquips.GetData(CustomCrestName);
            if (data.Slots == null)
            {
                return;
            }

            ToolCrest.SlotInfo[] slots = _crestData.slots.ToArray();
            List<string> equippedTools = new List<string>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                equippedTools.Add(string.Empty);
            }

            List<ToolItem> displacedTools = new List<ToolItem>();
            bool changed = data.Slots.Count > slots.Length;
            for (int i = 0; i < data.Slots.Count; i++)
            {
                string toolName = data.Slots[i].EquippedTool;
                if (string.IsNullOrEmpty(toolName))
                {
                    continue;
                }

                // A tool the game doesn't know may belong to a mod that isn't loaded: leave it in place.
                ToolItem? tool = ToolItemManager.GetToolByName(toolName);
                if (tool == null)
                {
                    if (i < slots.Length)
                    {
                        equippedTools[i] = toolName;
                    }
                }
                else if (i < slots.Length && tool.Type == slots[i].Type)
                {
                    equippedTools[i] = toolName;
                }
                else
                {
                    displacedTools.Add(tool);
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            foreach (ToolItem tool in displacedTools)
            {
                // A tool with no slot of its type left is unequipped.
                int target = FindFreeSlot(slots, equippedTools, slot => slot.Type == tool.Type);
                if (target >= 0)
                {
                    equippedTools[target] = tool.name;
                }
            }

            WriteEquippedTools(equippedTools, slots.Length);
            Plugin.ModLogger?.LogInfo(
                $"Moved or unequipped {displacedTools.Count} saved tool(s) to match the current slot layout.");
        }

        private static void RemapEquippedTools(ToolCrest.SlotInfo[] oldSlots, ToolCrest.SlotInfo[] newSlots)
        {
            ToolCrestsData.Data data = PlayerData.instance.ToolEquips.GetData(CustomCrestName);
            if (data.Slots == null)
            {
                return;
            }

            List<string> equippedTools = new List<string>(newSlots.Length);
            for (int i = 0; i < newSlots.Length; i++)
            {
                equippedTools.Add(string.Empty);
            }

            for (int i = 0; i < oldSlots.Length && i < data.Slots.Count; i++)
            {
                string tool = data.Slots[i].EquippedTool;
                if (string.IsNullOrEmpty(tool))
                {
                    continue;
                }

                ToolCrest.SlotInfo oldSlot = oldSlots[i];
                Predicate<ToolCrest.SlotInfo> sameSlot =
                    slot => slot.Type == oldSlot.Type && slot.AttackBinding == oldSlot.AttackBinding;

                // Staying at the same index keeps deliberately empty slots empty.
                int target = i < newSlots.Length && equippedTools[i].Length == 0 && sameSlot(newSlots[i])
                    ? i
                    : FindFreeSlot(newSlots, equippedTools, sameSlot);
                if (target < 0)
                {
                    target = FindFreeSlot(newSlots, equippedTools, slot => slot.Type == oldSlot.Type);
                }

                // A tool with no slot of its type left is unequipped.
                if (target >= 0)
                {
                    equippedTools[target] = tool;
                }
            }

            WriteEquippedTools(equippedTools, newSlots.Length);
            ToolItemManager.SendEquippedChangedEvent(force: true);
        }

        private static void WriteEquippedTools(List<string> equippedTools, int slotCount)
        {
            ToolItemManager.SetEquippedTools(CustomCrestName, equippedTools);

            // SetEquippedTools never shrinks the saved list.
            ToolCrestsData.Data data = PlayerData.instance.ToolEquips.GetData(CustomCrestName);
            if (data.Slots != null && data.Slots.Count > slotCount)
            {
                data.Slots.RemoveRange(slotCount, data.Slots.Count - slotCount);
                PlayerData.instance.ToolEquips.SetData(CustomCrestName, data);
            }
        }

        private static int FindFreeSlot(
            ToolCrest.SlotInfo[] slots,
            List<string> equippedTools,
            Predicate<ToolCrest.SlotInfo> matches)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (equippedTools[i].Length == 0 && matches(slots[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        public static void InjectLocalizedStrings()
        {
            try
            {
                FieldInfo? currentSheetsField = typeof(Language).GetField(
                    "_currentEntrySheets",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (currentSheetsField?.GetValue(null) is IDictionary sheets &&
                    sheets.Contains(LocalizationSheet) &&
                    sheets[LocalizationSheet] is IDictionary toolsDict)
                {
                    toolsDict[NameKey] = "Custom Crest";
                    toolsDict[DescriptionKey] = "Behold, the power of customization!";
                    toolsDict[PrefixKey] = "Bound the Custom Crest of";
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLogger?.LogWarning($"Could not inject localized strings: {ex.Message}");
            }
        }

        private static void OnMovesetInitialized()
        {
            InjectLocalizedStrings();

            if (_crestData?.ToolCrest != null)
            {
                _crestData.ToolCrest.itemNamePrefix = new LocalisedString
                {
                    Sheet = LocalizationSheet,
                    Key = PrefixKey
                };
            }
        }

        private static ToolCrest.SlotInfo[] BuildCustomSlots()
        {
            List<ToolCrest.SlotInfo> slots = new List<ToolCrest.SlotInfo>();
            const float spacing = 1.8f;
            const float yBlueRow = 3.2f;
            const float yUpAttack = 1.6f;
            const float yNeutralAttack = 0f;
            const float yDownAttack = -1.6f;
            const float yYellowRow = -3.2f;

            float blueStartX = -((Plugin.BlueSlots.Value - 1) * spacing) / 2f;
            for (int i = 0; i < Plugin.BlueSlots.Value; i++)
            {
                slots.Add(CreateSlot(blueStartX + i * spacing, yBlueRow, ToolItemType.Blue));
            }

            ToolItemType upType = Plugin.UpUseSkillSlots.Value ? ToolItemType.Skill : ToolItemType.Red;
            for (int i = 0; i < Plugin.UpAttackSlots.Value; i++)
            {
                slots.Add(CreateSlot(0f, yUpAttack, upType, AttackToolBinding.Up));
            }

            ToolItemType neutralType = Plugin.NeutralUseSkillSlots.Value ? ToolItemType.Skill : ToolItemType.Red;
            for (int i = 0; i < Plugin.NeutralAttackSlots.Value; i++)
            {
                slots.Add(CreateSlot(0f, yNeutralAttack, neutralType, AttackToolBinding.Neutral));
            }

            ToolItemType downType = Plugin.DownUseSkillSlots.Value ? ToolItemType.Skill : ToolItemType.Red;
            for (int i = 0; i < Plugin.DownAttackSlots.Value; i++)
            {
                slots.Add(CreateSlot(0f, yDownAttack, downType, AttackToolBinding.Down));
            }

            float yellowStartX = -((Plugin.YellowSlots.Value - 1) * spacing) / 2f;
            for (int i = 0; i < Plugin.YellowSlots.Value; i++)
            {
                slots.Add(CreateSlot(yellowStartX + i * spacing, yYellowRow, ToolItemType.Yellow));
            }

            return ToolSlotNavigationHelper.ToFixedNavigation(slots.ToArray());
        }

        private static ToolCrest.SlotInfo CreateSlot(
            float x,
            float y,
            ToolItemType type,
            AttackToolBinding? binding = null)
        {
            ToolCrest.SlotInfo slot = new ToolCrest.SlotInfo
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
            {
                slot.AttackBinding = binding.Value;
            }

            return slot;
        }
    }
}
