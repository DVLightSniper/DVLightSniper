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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.GameObjects.Library;

using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// A tracking property for an asset
    /// </summary>
    internal class AssetProperty<T> : TrackingProperty<T> where T : Object
    {
        /// <summary>
        /// Asset loader to attempt to load assets from
        /// </summary>
        private readonly AssetLoader assetLoader;

        /// <summary>
        /// Asset bundle to load assets from
        /// </summary>
        private readonly string assetBundleName;

        /// <summary>
        /// Prefix to apply to the property value
        /// </summary>
        private readonly string assetPrefix;

        internal AssetProperty(string key, string defaultPropertyValue, T defaultValue, AssetLoader assetLoader, string assetBundleName, string assetPrefix = "")
            : this(GlobalProperties.Instance, key, defaultPropertyValue, defaultValue, assetLoader, assetBundleName, assetPrefix)
        {
        }

        internal AssetProperty(IProperties properties, string key, string defaultPropertyValue, T defaultValue, AssetLoader assetLoader, string assetBundleName, string assetPrefix = "")
            : base(properties, key, defaultPropertyValue, defaultValue)
        {
            this.assetLoader = assetLoader;
            this.assetBundleName = assetBundleName;
            this.assetPrefix = assetPrefix ?? "";
            this.Update();
        }

        protected override T ComputeValue(string propertyValue)
        {
            return this.assetLoader.Load<T>(this.assetBundleName, this.assetPrefix + propertyValue);
        }
    }
}
