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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{

    /// <summary>
    /// Handle to an asset loaded (or to be loaded) from an assetbundle
    /// </summary>
    internal class AssetBundleAsset<TAsset> : IAsset<TAsset> where TAsset : Object
    {
        /// <summary>
        /// Assetbundle to load the asset from
        /// </summary>
        internal AssetBundleHandle AssetBundle { get; }

        /// <summary>
        /// Asset bundle name
        /// </summary>
        public string AssetBundleName => this.AssetBundle.AssetBundleName;

        /// <summary>
        /// Name of the asset to load
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// Loaded asset
        /// </summary>
        private Object asset;

        public TAsset Asset
        {
            get
            {
                if (this.asset == null)
                {
                    this.Load();
                }
                return this.asset as TAsset;
            }
        }

        internal AssetBundleAsset(AssetBundleHandle assetBundle, string assetName)
        {
            this.AssetBundle = assetBundle;
            this.AssetName = assetName;
        }

        public override string ToString()
        {
            return $"{this.AssetBundleName}:{this.AssetName}";
        }

        public bool Changed { get; private set; }
        public bool Valid { get; private set; }

        public AssetType Type
        {
            get
            {
                return AssetType.FromObject(this.asset);
            }
        }

        public void Reload()
        {
            this.asset = null;
            this.Valid = false;
            this.Load();
        }

        internal TAsset Load()
        {
            if (this.asset != null)
            {
                return this.asset as TAsset;
            }

            AssetBundle assetBundle = this.AssetBundle.AssetBundle;
            if (assetBundle == null)
            {
                AssetLoader.DebugMessages.AddMessage($"Failed loading assetbundle {this.AssetBundle}");
                return null;
            }

            if (string.IsNullOrEmpty(this.AssetName))
            {
                return null;
            }

            this.asset = assetBundle.LoadAsset<TAsset>(this.AssetName);
            if (this.asset == null)
            {
                AssetLoader.DebugMessages.AddMessage($"Failed to load asset {this.AssetName} from assetbundle {this.AssetBundle}");
                return null;
            }

            this.Valid = true;
            return (TAsset)this.asset;
        }
    }

}
