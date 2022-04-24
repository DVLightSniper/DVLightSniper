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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.GameObjects.Spawners;

using UnityEngine;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Component added to decorated game objects (objects which have decorations spawned on them)
    /// in order to allow decorations to be managed and removed and to avoid assignments of
    /// duplicate decorations.
    /// </summary>
    internal class DecorationsComponent : MonoBehaviour
    {
        internal enum Match
        {
            None,
            Group,
            Exact
        }

        private readonly IDictionary<string, DecorationSpawner> decorations = new Dictionary<string, DecorationSpawner>();

        internal int Count
        {
            get
            {
                return this.decorations.Count;
            }
        }

        internal void Add(DecorationSpawner decoration)
        {
            this.decorations[decoration.Id] = decoration;
        }

        internal void Remove(DecorationSpawner decoration)
        {
            this.decorations.Remove(decoration.Id);
        }

        public Match Has(string id)
        {
            if (this.decorations.ContainsKey(id))
            {
                return Match.Exact;
            }

            int pos = id.IndexOf(':');
            if (pos < 0)
            {
                return Match.None;
            }

            string group = id.Substring(0, pos + 1);

            foreach (string key in this.decorations.Keys)
            {
                if (key.StartsWith(group))
                {
                    return Match.Group;
                }
            }

            return Match.None;
        }

        public DecorationSpawner Find(string id)
        {
            if (this.decorations.ContainsKey(id))
            {
                return this.decorations[id];
            }

            int pos = id.IndexOf(':');
            if (pos < 0)
            {
                return null;
            }

            string group = id.Substring(0, pos + 1);

            foreach (string key in this.decorations.Keys)
            {
                if (key.StartsWith(group))
                {
                    return this.decorations[key];
                }
            }

            return null;
        }
    }
}
