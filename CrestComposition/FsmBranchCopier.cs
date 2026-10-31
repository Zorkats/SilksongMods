// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Silksong.FsmUtil;
using Silksong.FsmUtil.Actions;

namespace CrestComposition
{
    // Result of copying and rebinding a state subgraph inside a PlayMakerFSM.
    public sealed class CopiedFsmBranch
    {
        public FsmState EntryState { get; }
        public IReadOnlyList<FsmState> CopiedStates { get; }
        public IReadOnlyDictionary<string, FsmState> StateMap { get; }

        public CopiedFsmBranch(
            FsmState entryState,
            List<FsmState> copiedStates,
            Dictionary<string, FsmState> stateMap)
        {
            EntryState = entryState;
            CopiedStates = copiedStates;
            StateMap = stateMap;
        }
    }

    // Copies a reachable PlayMaker state subgraph via BFS and rebinds GameObject variables
    // and crest equip checks so the copied branch operates exclusively on cloned objects.
    // By default every crest check becomes a check for the custom crest. Passing isSourceCrest
    // keeps only the checks for that branch's own crest true and makes all the others false,
    // for FSMs that test several crests inside one branch.
    public static class FsmBranchCopier
    {
        private const BindingFlags InstanceFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static CopiedFsmBranch? CopyBranch(
            PlayMakerFSM fsm,
            string entryStateName,
            string statePrefix,
            IReadOnlyCollection<string> stopStates,
            IReadOnlyDictionary<string, FsmGameObject> variableRemap,
            ToolCrest? customToolCrest,
            Predicate<ToolCrest>? isSourceCrest = null)
        {
            if (fsm == null || string.IsNullOrEmpty(entryStateName))
            {
                return null;
            }

            if (fsm.Fsm != null && !fsm.Fsm.preprocessed)
            {
                fsm.Preprocess();
            }

            FsmState? originEntry = fsm.GetState(entryStateName);
            if (originEntry == null)
            {
                return null;
            }

            List<string> reachableNames = DiscoverReachableStateNames(fsm, entryStateName, stopStates);
            Dictionary<string, FsmState> copiedByOriginalName =
                new Dictionary<string, FsmState>(StringComparer.Ordinal);
            List<FsmState> copiedStates = new List<FsmState>(reachableNames.Count);

            foreach (string originalName in reachableNames)
            {
                string copiedName = $"{statePrefix} {originalName}";
                FsmState? existing = fsm.GetState(copiedName);
                FsmState copied = existing ?? fsm.CopyState(originalName, copiedName);
                copiedByOriginalName[originalName] = copied;
                copiedStates.Add(copied);
            }

            foreach (FsmState copiedState in copiedStates)
            {
                RemapStateTransitions(fsm, copiedState, copiedByOriginalName);
                RemoveCopiedLambdas(copiedState);
                RebindStateActions(copiedState, variableRemap, customToolCrest, isSourceCrest);
            }

            FsmState copiedEntry = copiedByOriginalName[entryStateName];
            return new CopiedFsmBranch(copiedEntry, copiedStates, copiedByOriginalName);
        }

        private static List<string> DiscoverReachableStateNames(
            PlayMakerFSM fsm,
            string entryStateName,
            IReadOnlyCollection<string> stopStates)
        {
            HashSet<string> stopSet = new HashSet<string>(stopStates, StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> queue = new Queue<string>();
            List<string> ordered = new List<string>();

            visited.Add(entryStateName);
            queue.Enqueue(entryStateName);

            while (queue.Count > 0)
            {
                string currentName = queue.Dequeue();
                FsmState? currentState = fsm.GetState(currentName);
                if (currentState == null)
                {
                    continue;
                }

                ordered.Add(currentName);

                if (currentState.Transitions == null)
                {
                    continue;
                }

                foreach (FsmTransition transition in currentState.Transitions)
                {
                    if (transition == null || string.IsNullOrEmpty(transition.ToState))
                    {
                        continue;
                    }

                    string targetName = transition.ToState;
                    if (stopSet.Contains(targetName))
                    {
                        continue;
                    }

                    if (visited.Add(targetName))
                    {
                        queue.Enqueue(targetName);
                    }
                }
            }

            return ordered;
        }

        private static void RemapStateTransitions(
            PlayMakerFSM fsm,
            FsmState copiedState,
            IReadOnlyDictionary<string, FsmState> copiedByOriginalName)
        {
            if (copiedState.Transitions == null)
            {
                return;
            }

            foreach (FsmTransition transition in copiedState.Transitions)
            {
                if (transition == null || string.IsNullOrEmpty(transition.ToState))
                {
                    continue;
                }

                if (copiedByOriginalName.TryGetValue(transition.ToState, out FsmState? mappedTarget))
                {
                    transition.ToState = mappedTarget.Name;
                    transition.ToFsmState = mappedTarget;
                }
                else
                {
                    FsmState? sharedTarget = fsm.GetState(transition.ToState);
                    if (sharedTarget != null)
                    {
                        transition.ToFsmState = sharedTarget;
                    }
                }
            }
        }

        // A lambda's delegate does not survive CopyState's serialization, and the next save of any
        // state in the FSM throws on the resulting null delegate, so the copy must not keep it.
        private static void RemoveCopiedLambdas(FsmState copiedState)
        {
            FsmStateAction[] actions = copiedState.Actions;
            if (actions != null && Array.Exists(actions, action => action is LambdaAction))
            {
                copiedState.Actions = Array.FindAll(actions, action => !(action is LambdaAction));
                copiedState.SaveActions();
            }
        }

        private static void RebindStateActions(
            FsmState copiedState,
            IReadOnlyDictionary<string, FsmGameObject> variableRemap,
            ToolCrest? customToolCrest,
            Predicate<ToolCrest>? isSourceCrest)
        {
            if (copiedState.Actions == null)
            {
                return;
            }

            foreach (FsmStateAction? action in copiedState.Actions)
            {
                if (action == null)
                {
                    continue;
                }

                if (action is CheckIfCrestEquipped crestCheck && customToolCrest != null)
                {
                    // A check with no crest is never true.
                    bool keepCheck = isSourceCrest == null ||
                                     (crestCheck.Crest?.Value is ToolCrest checkedCrest && isSourceCrest(checkedCrest));
                    crestCheck.Crest = new FsmObject
                    {
                        Value = keepCheck ? customToolCrest : null
                    };
                }

                RebindObjectFields(action, variableRemap);
            }
        }

        private static void RebindObjectFields(
            object target,
            IReadOnlyDictionary<string, FsmGameObject> variableRemap)
        {
            Type? currentType = target.GetType();
            while (currentType != null && currentType != typeof(object) && currentType != typeof(FsmStateAction))
            {
                FieldInfo[] fields = currentType.GetFields(
                    InstanceFieldFlags | BindingFlags.DeclaredOnly);

                foreach (FieldInfo field in fields)
                {
                    object? value = field.GetValue(target);
                    if (value == null)
                    {
                        continue;
                    }

                    if (value is FsmGameObject fsmGo)
                    {
                        if (!string.IsNullOrEmpty(fsmGo.Name) &&
                            variableRemap.TryGetValue(fsmGo.Name, out FsmGameObject? replacementGo))
                        {
                            field.SetValue(target, replacementGo);
                        }
                    }
                    else if (value is FsmOwnerDefault ownerDefault)
                    {
                        if (ownerDefault.OwnerOption == OwnerDefaultOption.SpecifyGameObject &&
                            ownerDefault.GameObject != null &&
                            !string.IsNullOrEmpty(ownerDefault.GameObject.Name) &&
                            variableRemap.TryGetValue(ownerDefault.GameObject.Name, out FsmGameObject? replacementOwnerGo))
                        {
                            FsmOwnerDefault reboundOwner = new FsmOwnerDefault
                            {
                                OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                                GameObject = replacementOwnerGo
                            };
                            field.SetValue(target, reboundOwner);
                        }
                    }
                    else if (value is FsmGameObject[] fsmGoArray)
                    {
                        for (int i = 0; i < fsmGoArray.Length; i++)
                        {
                            FsmGameObject? item = fsmGoArray[i];
                            if (item != null &&
                                !string.IsNullOrEmpty(item.Name) &&
                                variableRemap.TryGetValue(item.Name, out FsmGameObject? mappedItem))
                            {
                                fsmGoArray[i] = mappedItem;
                            }
                        }
                    }
                }

                currentType = currentType.BaseType;
            }
        }
    }
}
