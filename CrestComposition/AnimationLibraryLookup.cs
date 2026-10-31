// Copyright (c) Zorkats. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CrestComposition
{
    // Forces a tk2dSpriteAnimation to rebuild its clip lookup after its clips array is replaced.
    public static class AnimationLibraryLookup
    {
        private const BindingFlags InstanceFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo? _animLookupField =
            typeof(tk2dSpriteAnimation).GetField("lookup", InstanceFieldFlags);

        private static readonly FieldInfo? _animIsValidField =
            typeof(tk2dSpriteAnimation).GetField("isValid", InstanceFieldFlags);

        private static readonly Type? _animInfoType =
            typeof(tk2dSpriteAnimation).GetNestedType("AnimationInfo", BindingFlags.NonPublic);

        public static void Refresh(tk2dSpriteAnimation? animLib)
        {
            if (animLib == null)
            {
                return;
            }

            if (_animLookupField != null && _animInfoType != null)
            {
                Type dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), _animInfoType);
                object? emptyLookup = Activator.CreateInstance(dictType);
                if (emptyLookup is IDictionary)
                {
                    _animLookupField.SetValue(animLib, emptyLookup);
                }
            }

            if (_animIsValidField != null)
            {
                _animIsValidField.SetValue(animLib, false);
            }

            animLib.ValidateLookup();
        }
    }
}
