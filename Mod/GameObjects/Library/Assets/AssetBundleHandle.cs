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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.GameObjects.Spawners.Packs;

using UnityEngine;

using Resources = DVLightSniper.Properties.Resources;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{

    /// <summary>
    /// Handle to an asset bundle by name. The asset bundle may or may not exist and may be
    /// loaded from multiple sources, including an extracted file on disk, a resource in this
    /// assembly, or a pack
    /// </summary>
    internal class AssetBundleHandle
    {
        /// <summary>
        /// AssetLoader for this asset collection
        /// </summary>
        internal AssetLoader AssetLoader { get; }

        /// <summary>
        /// In order to avoid churn, we keep asset packs loaded for a few seconds after reading
        /// an asset from them, in case further assets need to be loaded. We unload the pack
        /// after a short delay so that it can still be modified on-the-fly when required.
        /// </summary>
        private const int KEEP_ALIVE_TIME_SECONDS = 5;

        /// <summary>
        /// The file name of the asset bundle (unmapped name, eg. my_assets.assetbundle)
        /// </summary>
        internal string AssetBundleName { get; }

        /// <summary>
        /// The file name of the asset bundle (mapped name, eg. my_assets_1.0.assetbundle)
        /// </summary>
        internal string MappedName { get; private set; }

        private AssetLoader.Source source;

        /// <summary>
        /// The source of the asset bundle. Non-pure, attempts to determine the source if the
        /// bundle hasn't been loaded yet.
        /// </summary>
        internal AssetLoader.Source Source
        {
            get
            {
                if (this.source == AssetLoader.Source.None && this.assetBundle == null)
                {
                    this.Load();
                    this.Unload();
                }
                return this.source;
            }
        }

        private AssetBundle assetBundle;

        /// <summary>
        /// The asset bundle
        /// </summary>
        internal AssetBundle AssetBundle
        {
            get
            {
                this.lastAccessTime = DateTime.UtcNow;
                return this.assetBundle ?? (this.assetBundle = this.Load());
            }
        }

        private DateTime lastAccessTime;

        internal AssetBundleHandle(AssetLoader assetLoader, string assetBundleName, string mappedName)
        {
            this.AssetLoader = assetLoader;
            this.AssetBundleName = assetBundleName;
            this.MappedName = mappedName;
        }

        /// <summary>
        /// Extracts a pack-provided asset bundle onto disk if it doesn't exist already
        /// </summary>
        /// <returns></returns>
        internal bool ExtractFromPack(AssetMappings mappings)
        {
            if (this.AssetLoader.IsBuiltin)
            {
                return false;
            }

            string fileName = Path.Combine(this.AssetLoader.Dir, this.MappedName);
            if (File.Exists(fileName))
            {
                return true;
            }

            string relativePath = Path.Combine(this.AssetLoader.Dir.RelativeToBaseDir(), this.AssetBundleName);
            PackResource assetResource = PackLoader.OpenResource(relativePath);
            if (assetResource == null)
            {
                return false;
            }

            if (!mappings.Contains(this.AssetBundleName))
            {
                this.MappedName = $"{Path.GetFileNameWithoutExtension(this.AssetBundleName)}_{assetResource.LastWriteTime.ToFileTimeUtc():X}.assetbundle";
                mappings[this.AssetBundleName] = this.MappedName;
                fileName = Path.Combine(this.AssetLoader.Dir, this.MappedName);
            }

            try
            {
                File.WriteAllBytes(fileName, assetResource.Resource);
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error("Error extracting resource from pack " + e.Message);
                return false;
            }
            this.Unload();
            return true;
        }

        /// <summary>
        /// Loads the asset bundle, checks on disk first, then checks internal resources and
        /// finally available packs. If the asset bundle isn't found under the mapped name,
        /// checks the unmapped name just in case.
        /// </summary>
        /// <returns></returns>
        private AssetBundle Load()
        {
            return this.Load(this.MappedName) ?? this.Load(this.AssetBundleName);
        }

        private AssetBundle Load(string name)
        {
            if (!this.AssetLoader.IsBuiltin)
            {
                string fileName = Path.Combine(this.AssetLoader.Dir, name);
                if (File.Exists(fileName))
                {
                    this.source = AssetLoader.Source.File;
                    return AssetBundle.LoadFromFile(fileName);
                }
            }

            object data = Resources.ResourceManager.GetObject(name, Resources.Culture);
            if (data != null)
            {
                this.source = AssetLoader.Source.Resource;
                return AssetBundle.LoadFromMemory((byte[])data);
            }

            if (!this.AssetLoader.IsBuiltin)
            {
                string relativePath = Path.Combine(this.AssetLoader.Dir.RelativeToBaseDir(), name);
                PackResource assetResource = PackLoader.OpenResource(relativePath);
                if (assetResource != null)
                {
                    this.source = AssetLoader.Source.Pack;
                    return AssetBundle.LoadFromMemory(assetResource.Resource);
                }
            }

            this.source = AssetLoader.Source.None;
            return null;
        }

        /// <summary>
        /// Tick the bundle to unload it when it hasn't been used for longer than the keepalive
        /// time
        /// </summary>
        internal void TickBundle()
        {
            if (this.assetBundle != null && !this.AssetLoader.IsBuiltin && (DateTime.UtcNow - this.lastAccessTime).TotalSeconds > AssetBundleHandle.KEEP_ALIVE_TIME_SECONDS)
            {
                this.Unload();
            }
        }

        internal void Unload()
        {
            this.assetBundle?.Unload(false);
            this.assetBundle = null;
        }

        public override string ToString()
        {
            return $"{this.AssetBundleName} ({ this.MappedName})";
        }
    }
}
