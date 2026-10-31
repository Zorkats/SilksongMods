// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using Needleforge.Attacks;
using Needleforge.Components;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace CrestComposition
{
    // Instantiate keeps a borrowed attack's serialized components but not what is wired up at runtime:
    // the tint and screen effects subscribed to AttackStarting, the link between a charged attack and
    // its trigger, and a tint group on the source crest's root, which sits above the cloned object.
    internal static class BorrowedAttackEffects
    {
        private static readonly FieldInfo? _attackStartingField =
            typeof(NailAttackBase).GetField(
                nameof(NailAttackBase.AttackStarting),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public static void CarryOver(GameObject source, GameObject clone, TintRendererGroup? rootTint)
        {
            NailAttackBase[] sourceAttacks = source.GetComponentsInChildren<NailAttackBase>(true);
            NailAttackBase[] cloneAttacks = clone.GetComponentsInChildren<NailAttackBase>(true);
            for (int i = 0; i < sourceAttacks.Length && i < cloneAttacks.Length; i++)
            {
                foreach (AttackBase attack in FindNeedleforgeAttacks(sourceAttacks[i]))
                {
                    AddStartEffects(cloneAttacks[i], attack);
                }
            }

            ChargedAttackAutoTrigger? trigger = source.GetComponent<ChargedAttackAutoTrigger>();
            if (trigger != null && trigger.attack != null)
            {
                clone.AddComponent<BorrowedChargedTrigger>().Source = trigger.attack;
            }

            if (rootTint != null)
            {
                clone.AddComponent<BorrowedRootTint>().Source = rootTint;
            }
        }

        // Needleforge keeps no list of the attacks it built, but each one subscribes to its own slash.
        private static HashSet<AttackBase> FindNeedleforgeAttacks(NailAttackBase sourceAttack)
        {
            HashSet<AttackBase> attacks = new HashSet<AttackBase>();
            if (_attackStartingField?.GetValue(sourceAttack) is Delegate subscribers)
            {
                foreach (Delegate subscriber in subscribers.GetInvocationList())
                {
                    if (subscriber.Target is AttackBase attack)
                    {
                        attacks.Add(attack);
                    }
                }
            }

            return attacks;
        }

        private static void AddStartEffects(NailAttackBase cloneAttack, AttackBase attack)
        {
            DamageEnemies? damager = cloneAttack.GetComponent<DamageEnemies>();
            tk2dSprite? sprite = cloneAttack.GetComponent<tk2dSprite>();
            cloneAttack.AttackStarting += () =>
            {
                if (damager != null && sprite != null && damager.NailElement == NailElements.None)
                {
                    sprite.color = attack.Color;
                }
            };

            if (attack is ChargedAttack.Step step)
            {
                Transform parent = cloneAttack.transform.parent;
                cloneAttack.AttackStarting += () => SendScreenEffects(parent, step);
            }
        }

        private static void SendScreenEffects(Transform parent, ChargedAttack.Step step)
        {
            if (step.CameraShakeIndex.HasValue)
            {
                parent.SendMessage(
                    nameof(CameraShakeAnimator.DoCameraShake),
                    step.CameraShakeIndex.Value,
                    SendMessageOptions.DontRequireReceiver);
            }

            if (step.ScreenFlashIndex.HasValue)
            {
                parent.SendMessage(
                    nameof(ScreenFlashAnimator.DoScreenFlash),
                    step.ScreenFlashIndex.Value,
                    SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    // Plays the cloned steps of a charged attack when its object activates. The source's flags are
    // read live, so changes its mod makes while the game runs still apply.
    internal sealed class BorrowedChargedTrigger : MonoBehaviour
    {
        public ChargedAttack? Source;

        private void OnEnable()
        {
            if (Source != null && Source.PlayOnActivation && Source.Steps != null && Source.Steps.Length > 0)
            {
                StartCoroutine(PlaySteps(Source));
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator PlaySteps(ChargedAttack attack)
        {
            yield return null;

            foreach (ChargedAttack.Step step in attack.Steps)
            {
                NailSlash? slash = FindClonedStep(step);
                if (slash == null)
                {
                    continue;
                }

                slash.StartSlash();
                yield return null;

                tk2dSpriteAnimator? animator = slash.GetComponent<tk2dSpriteAnimator>();
                while (animator != null && animator.Playing && animator.CurrentClip != null)
                {
                    yield return null;
                }

                if (!attack.PlayStepsInSequence)
                {
                    break;
                }
            }

            if (attack.DisableAfterLastStep)
            {
                gameObject.SetActive(false);
            }
        }

        private NailSlash? FindClonedStep(ChargedAttack.Step step)
        {
            if (step.GameObject == null)
            {
                return null;
            }

            int index = step.GameObject.transform.GetSiblingIndex();
            return index < transform.childCount ? transform.GetChild(index).GetComponent<NailSlash>() : null;
        }
    }

    // Applies the source root's tint color to one clone, except while the nail is imbued: an
    // imbuement sets its own color. Runs every frame because the game resets a slash's color when
    // the slash ends. Alpha is left alone so fades keep working.
    internal sealed class BorrowedRootTint : MonoBehaviour
    {
        public TintRendererGroup? Source;

        private SpriteRenderer[] _sprites = Array.Empty<SpriteRenderer>();
        private IVertexColor[] _vertexColors = Array.Empty<IVertexColor>();

        private void OnEnable()
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _vertexColors = GetComponentsInChildren<IVertexColor>(true);
        }

        private void LateUpdate()
        {
            HeroController? hc = HeroController.instance;
            if (Source == null || hc == null || hc.NailImbuement.CurrentElement != NailElements.None)
            {
                return;
            }

            Color color = Source.Color;
            foreach (SpriteRenderer sprite in _sprites)
            {
                color.a = sprite.color.a;
                sprite.color = color;
            }

            foreach (IVertexColor vertexColor in _vertexColors)
            {
                color.a = vertexColor.VertexColor.a;
                vertexColor.VertexColor = color;
            }
        }
    }
}
