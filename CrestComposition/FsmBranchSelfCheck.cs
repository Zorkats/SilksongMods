// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace CrestComposition
{
    // Audits copied PlayMaker FSM states and cloned attack GameObjects at startup to verify
    // that no action or component still references a vanilla crest's ActiveRoot or ToolCrest.
    public static class FsmBranchSelfCheck
    {
        private const BindingFlags InstanceFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo? _activateOnSlashField = CrestComposerContext.ActivateOnSlashField;

        public static bool Verify(
            string crestName,
            IReadOnlyList<FsmState> copiedStates,
            IReadOnlyList<GameObject> clonedAttackObjects,
            IReadOnlyList<Transform> vanillaActiveRoots,
            ToolCrest? customToolCrest,
            ManualLogSource? logger)
        {
            List<string> violations = new List<string>();
            IReadOnlyCollection<string> forbiddenVarNames = VanillaCrestCatalog.AllVanillaCrestAttackVariables;
            HashSet<string> forbiddenVarSet = new HashSet<string>(forbiddenVarNames, StringComparer.Ordinal);

            foreach (FsmState state in copiedStates)
            {
                if (state == null || state.Actions == null)
                {
                    continue;
                }

                for (int actionIndex = 0; actionIndex < state.Actions.Length; actionIndex++)
                {
                    FsmStateAction? action = state.Actions[actionIndex];
                    if (action == null || !action.Enabled)
                    {
                        continue;
                    }

                    AuditAction(
                        state.Name,
                        actionIndex,
                        action,
                        forbiddenVarSet,
                        vanillaActiveRoots,
                        customToolCrest,
                        violations);
                }
            }

            foreach (GameObject clone in clonedAttackObjects)
            {
                if (clone == null)
                {
                    continue;
                }

                AuditClonedAttackObject(clone, vanillaActiveRoots, violations);
            }

            if (violations.Count == 0)
            {
                logger?.LogInfo(
                    $"[CrestComposition] Self-check passed for '{crestName}': " +
                    $"{copiedStates.Count} copied FSM states and {clonedAttackObjects.Count} " +
                    "cloned attack objects verified with 0 vanilla references.");
                return true;
            }

            logger?.LogError(
                $"[CrestComposition] Self-check FAILED for '{crestName}' with {violations.Count} violation(s):");
            foreach (string violation in violations)
            {
                logger?.LogError($"  - {violation}");
            }

            return false;
        }

        private static void AuditAction(
            string stateName,
            int actionIndex,
            FsmStateAction action,
            HashSet<string> forbiddenVarSet,
            IReadOnlyList<Transform> vanillaActiveRoots,
            ToolCrest? customToolCrest,
            List<string> violations)
        {
            Type actionType = action.GetType();
            string context = $"State '{stateName}' action[{actionIndex}] ({actionType.Name})";

            if (action is CheckIfCrestEquipped crestCheck)
            {
                ToolCrest? checkedCrest = crestCheck.Crest?.Value as ToolCrest;
                if (checkedCrest != null && !ReferenceEquals(checkedCrest, customToolCrest))
                {
                    violations.Add(
                        $"{context} checks vanilla ToolCrest '{checkedCrest.name}' instead of '{customToolCrest?.name}'.");
                }
            }

            Type? currentType = actionType;
            while (currentType != null && currentType != typeof(object) && currentType != typeof(FsmStateAction))
            {
                FieldInfo[] fields = currentType.GetFields(InstanceFieldFlags | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    object? value = field.GetValue(action);
                    if (value == null)
                    {
                        continue;
                    }

                    if (value is FsmGameObject fsmGo)
                    {
                        AuditFsmGameObject(
                            context,
                            field.Name,
                            fsmGo,
                            forbiddenVarSet,
                            vanillaActiveRoots,
                            violations);
                    }
                    else if (value is FsmOwnerDefault ownerDefault &&
                             ownerDefault.OwnerOption == OwnerDefaultOption.SpecifyGameObject &&
                             ownerDefault.GameObject != null)
                    {
                        AuditFsmGameObject(
                            context,
                            $"{field.Name}.GameObject",
                            ownerDefault.GameObject,
                            forbiddenVarSet,
                            vanillaActiveRoots,
                            violations);
                    }
                    else if (value is GameObject directGo)
                    {
                        if (directGo != null && IsUnderAnyVanillaRoot(directGo.transform, vanillaActiveRoots, out string rootName))
                        {
                            violations.Add(
                                $"{context} field '{field.Name}' points directly to GameObject '{directGo.name}' under vanilla root '{rootName}'.");
                        }
                    }
                    else if (value is Component directComponent)
                    {
                        if (directComponent != null &&
                            IsUnderAnyVanillaRoot(directComponent.transform, vanillaActiveRoots, out string rootName))
                        {
                            violations.Add(
                                $"{context} field '{field.Name}' points directly to Component '{directComponent.GetType().Name}' under vanilla root '{rootName}'.");
                        }
                    }
                }

                currentType = currentType.BaseType;
            }
        }

        private static void AuditFsmGameObject(
            string context,
            string fieldName,
            FsmGameObject fsmGo,
            HashSet<string> forbiddenVarSet,
            IReadOnlyList<Transform> vanillaActiveRoots,
            List<string> violations)
        {
            if (!string.IsNullOrEmpty(fsmGo.Name) && forbiddenVarSet.Contains(fsmGo.Name))
            {
                violations.Add(
                    $"{context} field '{fieldName}' still references vanilla FSM variable '{fsmGo.Name}'.");
            }

            if (fsmGo.Value != null &&
                IsUnderAnyVanillaRoot(fsmGo.Value.transform, vanillaActiveRoots, out string rootName))
            {
                violations.Add(
                    $"{context} field '{fieldName}' (var '{fsmGo.Name}') points to GameObject '{fsmGo.Value.name}' under vanilla root '{rootName}'.");
            }
        }

        private static void AuditClonedAttackObject(
            GameObject clone,
            IReadOnlyList<Transform> vanillaActiveRoots,
            List<string> violations)
        {
            if (_activateOnSlashField == null)
            {
                return;
            }

            NailAttackBase[] attacks = clone.GetComponentsInChildren<NailAttackBase>(true);
            foreach (NailAttackBase attack in attacks)
            {
                if (attack == null)
                {
                    continue;
                }

                if (_activateOnSlashField.GetValue(attack) is GameObject[] activateList)
                {
                    for (int i = 0; i < activateList.Length; i++)
                    {
                        GameObject? target = activateList[i];
                        if (target != null &&
                            IsUnderAnyVanillaRoot(target.transform, vanillaActiveRoots, out string rootName))
                        {
                            violations.Add(
                                $"Cloned attack '{clone.name}' has activateOnSlash[{i}] pointing to '{target.name}' under vanilla root '{rootName}'.");
                        }
                    }
                }
            }
        }

        private static bool IsUnderAnyVanillaRoot(
            Transform? candidate,
            IReadOnlyList<Transform> vanillaActiveRoots,
            out string matchedRootName)
        {
            matchedRootName = string.Empty;
            if (candidate == null)
            {
                return false;
            }

            foreach (Transform vanillaRoot in vanillaActiveRoots)
            {
                if (vanillaRoot != null && candidate.IsChildOf(vanillaRoot))
                {
                    matchedRootName = vanillaRoot.name;
                    return true;
                }
            }

            return false;
        }
    }
}
