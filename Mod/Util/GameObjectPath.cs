//------------------------------------------------------------------------------
// This file is part of DVLightSniper, licensed under the MIT License (MIT).
//
// Copyright (c) Mumfrey
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.Util
{
    /// <summary>
    /// Extension methods for GameObject (and Transform) which handle "unique" object paths, similar
    /// to the transform paths supported by unity directly eg. /Path/To/TransformName but including
    /// support for object name clashes.
    /// 
    /// Since object names in unity don't have to be unique, it's perfectly legal to have two
    /// objects parented to the same transform with the same name, this causes us problems since we
    /// want to be able to uniquely identify objects by some kind of path.
    /// 
    /// This class provides an augmented path syntax where conflicting transform path elements are
    /// disambiguated using indexes, stored in the path as ObjectName#nnn where nnn is an index
    /// into the parent transform's children representing the nth child with that name, for example
    /// the following objects:
    /// 
    /// <code>
    ///     /RootObject/SomeTransform/SomeChild
    ///     /RootObject/SomeTransform/AnotherChild
    ///     /RootObject/SomeTransform/SomeChild
    /// </code>
    /// 
    /// when converted to paths via GetObjectPath become:
    /// 
    /// <code>
    ///     /RootObject/SomeTransform/SomeChild#0
    ///     /RootObject/SomeTransform/AnotherChild
    ///     /RootObject/SomeTransform/SomeChild#1
    /// </code>
    /// 
    /// This also works for transforms in the middle of the hierarchy when resolving children of
    /// non-unique parents.
    /// 
    /// This makes some assumptions about the relationship of transforms, mainly that transforms
    /// will always appear in the hierarchy in the same order, and that it's unlikely that an object
    /// in the scene has a name which explicitly ends with #nnn. If either of these assumptions is
    /// incorrect then this will probably not work as intended.
    /// </summary>
    internal static class GameObjectPath
    {
        private static readonly Regex TRANSFORM_PATH_SPLITTER = new Regex(@"^(?<head>[^#]+?)/(?<part>[^#/]+?)#(?<index>[0-9]+)(?:/(?<tail>.+))?$");

        internal static GameObject Find(string name)
        {
            if (!name.Contains("#"))
            {
                // Fall back to regular GameObject.Find if there are no # symbols in the name
                return GameObject.Find(name);
            }

            return GameObjectPath.FindRecursive(name, null);
        }

        private static GameObject FindRecursive(string name, GameObject root)
        {
            Match match = GameObjectPath.TRANSFORM_PATH_SPLITTER.Match(name);
            if (!match.Success)
            {
                return root.FindDescendant(name);
            }

            GameObject parent = root.FindDescendant(match.Groups["head"].Value);
            if (parent == null)
            {
                return null;
            }

            GameObject part = parent.GetChild(match.Groups["part"].Value, int.Parse(match.Groups["index"].Value));
            return match.Groups["tail"].Success ? GameObjectPath.FindRecursive(match.Groups["tail"].Value, part) : part;

        }

        private static GameObject GetChild(this GameObject parent, string name, int index = 0)
        {
            for (int offset = 0, sibling = 0; offset < parent.transform.childCount; offset++)
            {
                Transform child = parent.transform.GetChild(offset);
                if (child.name == name && sibling++ == index)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindDescendant(this GameObject parent, string name)
        {
            if (parent == null || name.StartsWith("/"))
            {
                return GameObject.Find(name);
            }

            return parent.transform.Find(name)?.gameObject;
        }

        internal static string GetObjectPath(this Transform transform)
        {
            string transformName = transform.name;
            if (transform.parent != null)
            {
                int siblings = 0;
                int index = -1;
                for (int offset = 0; offset < transform.parent.childCount; offset++)
                {
                    if (transform.parent.GetChild(offset).name == transform.name)
                    {
                        if (object.ReferenceEquals(transform.parent.GetChild(offset), transform))
                        {
                            index = siblings;
                        }
                        siblings++;
                    }

                    if (siblings > 1)
                    {
                        transformName = transform.name + "#" + index;
                    }
                }
            }

            return (transform.parent != null ? transform.parent.GetObjectPath() : "") + "/" + transformName;
        }

        internal static string GetObjectPath(this GameObject gameObject)
        {
            return gameObject?.transform.GetObjectPath() ?? "";
        }

        internal static bool HasUniquePath(this GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            GameObject byPath = GameObjectPath.Find(gameObject.GetObjectPath());
            return object.ReferenceEquals(gameObject, byPath);
        }
    }
}
