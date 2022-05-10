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

using DVLightSniper.Mod.GameObjects.Spawners.Properties;

namespace DVLightSniper.Mod.Components.Prefabs
{
    /// <summary>
    /// Attribute to connect a behaviour defined in this assembly to a prefab loaded from an asset
    /// bundle.
    /// </summary>
    internal class PrefabComponentAttribute : Attribute
    {
        /// <summary>
        /// ID of this component, used to assign components by ID instead of attaching by asset name
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Name of the asset bundle
        /// </summary>
        public string AssetBundleName { get; }

        /// <summary>
        /// Name of the asset
        /// </summary>
        public string AssetName { get; }

        internal PrefabComponentAttribute(string id, string assetBundleName = "", string assetName = "")
        {
            this.Id = id;
            this.AssetBundleName = assetBundleName;
            this.AssetName = assetName;
        }

        internal bool Matches(MeshProperties mesh)
        {
            return mesh.ComponentId == this.Id || (mesh.AssetBundleName == this.AssetBundleName && mesh.AssetName == this.AssetName);
        }
    }
}
