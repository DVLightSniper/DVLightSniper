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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Crosstales.NAudio.Wave;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners.Packs;
using DVLightSniper.Mod.Storage;
using DVLightSniper.Mod.Util;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Logger = DVLightSniper.Mod.Util.Logger;
using Resources = DVLightSniper.Properties.Resources;
using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{
    /// <summary>
    /// We can load assets from built-in or externally situated asset bundles (which may themselves
    /// be extracted from built-in resources) so this class handles loading, extracting and caching
    /// loaded assets from asset bundles.
    /// </summary>
    public class AssetLoader
    {
        internal static readonly DebugOverlay.Section DebugMessages = DebugOverlay.AddSection("Asset Loader", new Color(1.0F, 0.4F, 0.0F), Logger.Level.Error);

        /// <summary>
        /// The source an asset bundle is loaded from
        /// </summary>
        public enum Source
        {
            /// <summary>
            /// The bundle has not been loaded yet, or is not available
            /// </summary>
            None,

            /// <summary>
            /// The bundle was successfully loaded fom an extracted file in the assets dir
            /// </summary>
            File,

            /// <summary>
            /// The bundle was successfully loaded from an internal resource
            /// </summary>
            Resource,

            /// <summary>
            /// The bundle was successfully loaded from a pack
            /// </summary>
            Pack
        }

        /// <summary>
        /// Policy which controls whether an asset can be loaded from defined override or fallback
        /// loaders when appropriate
        /// </summary>
        [Flags]
        public enum AssetPolicy
        {
            /// <summary>
            /// The asset can only be loaded from the asset loader being called
            /// </summary>
            Local = 0,

            /// <summary>
            /// The asset can be loaded from a fallback resource if this asset loader cannot provide
            /// it.
            /// </summary>
            Fallback = 1,

            /// <summary>
            /// The asset can be loaded from override asset loader, or this loader
            /// </summary>
            Override = 2,

            /// <summary>
            /// The asset can be loaded from any available loader, including override and fallback
            /// loaders as available
            /// </summary>
            Any = 3
        }

        [Flags]
        internal enum LookupFlags
        {
            Default = 3,
            LookupInRoot = 1,
            LookupInBundleDir = 2
        }

        /// <summary>
        /// Asset loaders by type, all asset loaders apart from the builtin loader reference a dir
        /// under the Assets dir, eg. the Meshes loader references Assets/Meshes
        /// </summary>
        private static readonly IDictionary<string, AssetLoader> loaders = new Dictionary<string, AssetLoader>();

        /// <summary>
        /// Asset bundle mappings storage
        /// </summary>
        private static AssetMappings mappings;

        /// <summary>
        /// Asset bundle name mappings collection for resolving the current version of an asset
        /// bundle from a non-versioned name. Also used when an asset bundle is extracted from a
        /// pack to keep track of the current versioned bundle
        /// </summary>
        private static AssetMappings AssetMappings
        {
            get
            {
                return AssetLoader.mappings ?? (AssetLoader.mappings = AssetMappings.Load(AssetLoader.AssetsDir, "mappings.json"));
            }
        }

        /// <summary>
        /// Base dir for asset loaders
        /// </summary>
        public static string AssetsDir { get => Path.Combine(LightSniper.Path, "Assets"); }

        /// <summary>
        /// Dir for this specific asset loader
        /// </summary>
        public string Dir { get; }

        /// <summary>
        /// Bare subdir for this specific asset loader
        /// </summary>
        public string SubDir { get; }

        /// <summary>
        /// True if this is the builtin loader, no base dir since assets are loaded from the
        /// assembly
        /// </summary>
        public bool IsBuiltin { get; }

        /// <summary>
        /// True if this is the user loader
        /// </summary>
        public bool IsUser { get; }

        /// <summary>
        /// Matcher for resources which are valid bundles/resources for this loader
        /// </summary>
        public Func<string, bool> Matcher { get; set; } = (key) => true;

        /// <summary>
        /// Fallback meta storage for file creation
        /// </summary>
        public MetaStorage Meta { get; set; }

        /// <summary>
        /// Asset loader which contains assets which can override those in this loader
        /// </summary>
        public AssetLoader OverrideLoader { get; set; }

        /// <summary>
        /// Asset loader which contains assets which are used when an asset is not present in this
        /// loader
        /// </summary>
        public AssetLoader FallbackLoader { get; set; }

        /// <summary>
        /// Asset bundles this loader has loaded
        /// </summary>
        private readonly IDictionary<string, AssetBundleHandle> assetBundles = new Dictionary<string, AssetBundleHandle>();

        /// <summary>
        /// Assets this loader has loader (or will load)
        /// </summary>
        private readonly IDictionary<string, IAsset<Object>> assets = new Dictionary<string, IAsset<Object>>();

        /// <summary>
        /// Get the builtin asset loader
        /// </summary>
        public static AssetLoader Builtin
        {
            get
            {
                if (!AssetLoader.loaders.ContainsKey(""))
                {
                    AssetLoader.loaders[""] = new AssetLoader(null, null);
                }

                return AssetLoader.loaders[""];
            }
        }

        /// <summary>
        /// Get the user asset (fallback) loader
        /// </summary>
        public static AssetLoader User
        {
            get => AssetLoader.Of("User");
        }

        /// <summary>
        /// Get the meshes asset loader
        /// </summary>
        public static AssetLoader Meshes
        {
            get => AssetLoader.Of("Meshes");
        }

        /// <summary>
        /// Get the coronas asset loader
        /// </summary>
        public static AssetLoader Coronas
        {
            get => AssetLoader.Of("Coronas");
        }

        /// <summary>
        /// Get an asset loader for the specified asset collection
        /// </summary>
        /// <param name="subDir"></param>
        /// <returns></returns>
        public static AssetLoader Of(string subDir)
        {
            string dir = Path.Combine(AssetLoader.AssetsDir, subDir);
            string key = dir.ToLowerInvariant();
            if (!AssetLoader.loaders.ContainsKey(key))
            {
                AssetLoader.loaders[key] = new AssetLoader(dir, subDir);
            }

            return AssetLoader.loaders[key];
        }

        private AssetLoader(string dir, string subDir)
        {
            this.Dir = dir;
            this.SubDir = subDir;
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            this.IsBuiltin = dir == null;
            this.IsUser = subDir == "User";
        }

        public override string ToString()
        {
            return $"AssetLoader[{this.SubDir ?? "Builtin"}]";
        }

        private AssetBundleHandle GetAssetBundle(string assetBundleName)
        {
            if (!this.assetBundles.ContainsKey(assetBundleName))
            {
                this.assetBundles[assetBundleName] = new AssetBundleHandle(this, assetBundleName, AssetLoader.AssetMappings[assetBundleName]);
            }
            return this.assetBundles[assetBundleName];
        }

        private void TickBundles()
        {
            foreach (AssetBundleHandle bundle in this.assetBundles.Values)
            {
                bundle.TickBundle();
            }
        }

        /// <summary>
        /// Load a builtin asset, not valid for non-builtin asset loaders and will return null
        /// </summary>
        /// <typeparam name="TAsset"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public TAsset LoadBuiltin<TAsset>(string assetName) where TAsset : Object
        {
            return this.IsBuiltin ? this.LoadAsset<TAsset>("builtin.assetbundle", assetName)?.Asset : null;
        }

        /// <summary>
        /// Attempt to load an asset from the specified asset bundle and return the asset itself
        /// </summary>
        /// <typeparam name="TAsset"></typeparam>
        /// <param name="assetBundleName"></param>
        /// <param name="assetName"></param>
        /// <param name="policy"></param>
        /// <returns></returns>
        public TAsset Load<TAsset>(string assetBundleName, string assetName, AssetPolicy policy = AssetPolicy.Fallback) where TAsset : Object
        {
            return this.LoadAsset<TAsset>(assetBundleName, assetName, policy)?.Asset;
        }

        /// <summary>
        /// Attemtp to load an asset from the specified asset bundle and return the asset handle
        /// </summary>
        /// <typeparam name="TAsset"></typeparam>
        /// <param name="assetBundleName"></param>
        /// <param name="assetName"></param>
        /// <param name="policy"></param>
        /// <returns></returns>
        public IAsset<TAsset> LoadAsset<TAsset>(string assetBundleName, string assetName, AssetPolicy policy = AssetPolicy.Fallback) where TAsset : Object
        {
            string key = typeof(TAsset).Name + ":" + assetBundleName + ":" + assetName;
            if (!this.assets.ContainsKey(key))
            {
                IAsset<Object> asset;
                if (policy.HasFlag(AssetPolicy.Override) && this.OverrideLoader != null && FileAsset<TAsset>.Exists(this.OverrideLoader, assetBundleName, assetName, LookupFlags.LookupInBundleDir))
                {
                    asset = FileAsset<TAsset>.Create(this.OverrideLoader, assetBundleName, assetName, LookupFlags.LookupInBundleDir);
                    if (asset?.Asset != null)
                    {
                        AssetLoader.DebugMessages.AddMessage($"Loaded override asset {asset} from {this.OverrideLoader}");
                        this.assets[key] = asset;
                        return (IAsset<TAsset>)this.assets[key];
                    }
                }

                if (string.IsNullOrEmpty(assetBundleName))
                {
                    asset = FileAsset<TAsset>.Create(this, assetBundleName, assetName);
                }
                else
                {
                    asset = new AssetBundleAsset<TAsset>(this.GetAssetBundle(assetBundleName), assetName);
                }

                if (policy.HasFlag(AssetPolicy.Fallback) && asset?.Asset == null && this.FallbackLoader != null && FileAsset<TAsset>.Exists(this.FallbackLoader, assetBundleName, assetName))
                {
                    asset = FileAsset<TAsset>.Create(this.FallbackLoader, assetBundleName, assetName);
                    if (asset?.Asset != null)
                    {
                        AssetLoader.DebugMessages.AddMessage($"Loaded fallback asset {asset} from {this.FallbackLoader}");
                    }
                }

                if (asset != null)
                {
                    this.assets[key] = asset;
                }
            }
            return (IAsset<TAsset>)this.assets[key];
        }

        /// <summary>
        /// Determine the source of the specified asset bundle
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <returns></returns>
        public Source FindSource(string assetBundleName)
        {
            return this.IsBuiltin ? Source.None : this.GetAssetBundle(assetBundleName).Source;
        }

        /// <summary>
        /// If the specified asset bundle is located in a pack, extract it
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <returns></returns>
        public bool ExtractFromPack(string assetBundleName)
        {
            return !this.IsBuiltin && this.GetAssetBundle(assetBundleName).ExtractFromPack(AssetLoader.AssetMappings);
        }

        /// <summary>
        /// List all files available to this loader which match the supplied matcher
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> ListFiles(AssetType type, Func<string, bool> matcher = null)
        {
            return this.ListFiles(type.Dir, matcher);
        }

        /// <summary>
        /// List all files available to this loader which match the supplied matcher
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> ListFiles(string dir, Func<string, bool> matcher = null)
        {
            ISet<string> matches = new HashSet<string>();
            this.FilterFiles(Directory.EnumerateFiles(Path.Combine(this.Dir, dir), "*.*"), matches, matcher ?? this.Matcher);
            this.FilterFiles(PackLoader.Find(Path.Combine(this.Dir.RelativeToBaseDir(), dir) + "\\"), matches, matcher ?? this.Matcher);
            return matches;
        }

        private void FilterFiles(IEnumerable<string> files, ISet<string> matches, Func<string, bool> matcher)
        {
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                if (fileName != null && matcher(fileName))
                {
                    matches.Add(fileName);
                }
            }
        }

        /// <summary>
        /// Extract all resources which match the specified bundle prefix
        /// </summary>
        public void ExtractBundles(string bundlePrefix)
        {
            this.ExtractResources(this.Dir, (key) => key.StartsWith(bundlePrefix) && key.EndsWith(".assetbundle"));
        }

        /// <summary>
        /// Extract all resources which match this loader's matcher criteria
        /// </summary>
        public void ExtractResources(Func<string, bool> matcher = null)
        {
            this.ExtractResources(this.Dir, matcher);
        }

        /// <summary>
        /// Extract all resources which match the supplied matcher criteria
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="matcher"></param>
        public void ExtractResources(string dir, Func<string, bool> matcher = null)
        {
            matcher = matcher ?? this.Matcher;
            foreach (DictionaryEntry resource in Resources.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true))
            {
                string fileName = resource.Key.ToString();
                string matchPart = fileName.EndsWith(".meta") ? fileName.Substring(0, fileName.Length - 5) : fileName;
                if (!matcher(matchPart))
                {
                    continue;
                }

                AssetType type = AssetType.FromExtension(matchPart);
                string subDir = type != null ? Path.Combine(dir, type.Dir) : dir;

                string oldPath = Path.Combine(dir, fileName);
                string extractPath = Path.Combine(subDir, fileName);

                // TODO: remove in future version, just used to move textures into subdir for now
                if (oldPath != extractPath && File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }

                if (File.Exists(extractPath))
                {
                    continue;
                }

                object data = Resources.ResourceManager.GetObject(fileName, Resources.Culture);
                if (data != null)
                {
                    Directory.CreateDirectory(subDir);
                    File.WriteAllBytes(extractPath, (byte[])data);
                }
            }
        }

        /// <summary>
        /// Extract a texture asset if it is located in a pack
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool ExtractTexture(string fileName)
        {
            return this.ExtractAsset(fileName, AssetType.TEXTURE.Dir);
        }

        /// <summary>
        /// Extract an asset if it is located in a pack
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="subDir"></param>
        /// <returns></returns>
        public bool ExtractAsset(string fileName, string subDir)
        { 
            string dir = Path.Combine(this.Dir, subDir);
            string path = Path.Combine(dir, fileName);
            if (File.Exists(path))
            {
                return true;
            }

            PackResource resource = PackLoader.OpenResource(path.RelativeToBaseDir());
            if (resource == null)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, resource.Resource);
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error("Error extracting {0} from pack: {1}", fileName, e.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Tick all asset loaders, this unloads any bundles which have not been accessed within
        /// the keepalive time
        /// </summary>
        public static void Tick()
        {
            foreach (AssetLoader assetLoader in AssetLoader.loaders.Values)
            {
                assetLoader.TickBundles();
            }
        }
    }
}
