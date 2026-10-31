// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker;
using Needleforge.Data;
using UnityEngine;

namespace CrestComposition
{
    // Void Crest's bind depends on its own gate method and HUD spool, and the mod has no public API,
    // so they are found by name. Only the gate is patched: Void's retaliation, void mass and control
    // blocks all ask it whether the hero is focusing. Everything else of Void's stays with its crest.
    internal sealed class VoidBindBridge
    {
        public const string OutcomeId = "Void";

        private const string SpoolChildName = "Thread Spool";

        private static VoidBindBridge? _instance;

        private readonly FieldInfo _spoolField;

        public CrestData Crest { get; }
        public UniqueBindEvent SpecialBind { get; }

        private VoidBindBridge(CrestData crest, UniqueBindEvent specialBind, FieldInfo spoolField)
        {
            Crest = crest;
            SpecialBind = specialBind;
            _spoolField = spoolField;
        }

        // Call after all plugins have run Awake. Null when Void is missing or has changed.
        public static VoidBindBridge? Resolve(ManualLogSource? logger)
        {
            if (_instance != null)
            {
                return _instance;
            }

            CrestData? crest = AttackSourceCatalog.FindNeedleforgeCrest(OutcomeId);
            if (crest == null)
            {
                return null;
            }

            MethodInfo? focusGate = AccessTools.Method("VoidCrest.AddVoid:VoidFocusEnabled");
            Type? spoolType = AccessTools.TypeByName("VoidCrest.voidspool");
            FieldInfo? spoolField = spoolType != null ? AccessTools.Field(spoolType, "spoolClone") : null;
            UniqueBindEvent? specialBind = FindSpecialBind(crest);
            if (focusGate == null || spoolField == null || specialBind == null)
            {
                logger?.LogWarning(
                    "[CrestComposition] This version of Void Crest has changed, so its bind cannot be used.");
                return null;
            }

            CrestCompositionPatches.PatchVoidFocusGate(focusGate);
            _instance = new VoidBindBridge(crest, specialBind, spoolField);
            return _instance;
        }

        public void StartBind(FsmInt healAmount, FsmInt bindAmount, FsmFloat bindTime, PlayMakerFSM fsm)
        {
            // Void hides the spool whenever another crest is equipped, and that runs on every equip.
            SetSpoolVisible(true);
            Crest.BindEvent?.Invoke(healAmount, bindAmount, bindTime, fsm);
        }

        public void SetSpoolVisible(bool visible)
        {
            if (!(_spoolField.GetValue(null) is GameObject spool) || spool == null)
            {
                return;
            }

            spool.SetActive(visible);
            spool.transform.Find(SpoolChildName)?.gameObject.SetActive(visible);
        }

        public static bool IsDirectionPressed(HeroController hc, UniqueBindDirection direction)
        {
            switch (direction)
            {
                case UniqueBindDirection.UP:
                    return hc.inputHandler.inputActions.Up.IsPressed;
                case UniqueBindDirection.DOWN:
                    return hc.inputHandler.inputActions.Down.IsPressed;
                case UniqueBindDirection.LEFT:
                    return hc.inputHandler.inputActions.Left.IsPressed;
                case UniqueBindDirection.RIGHT:
                    return hc.inputHandler.inputActions.Right.IsPressed;
                default:
                    return false;
            }
        }

        // The getter indexes a dictionary, which throws for a crest that never set one.
        private static UniqueBindEvent? FindSpecialBind(CrestData crest)
        {
            try
            {
                return crest.uniqueBindEvent;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
