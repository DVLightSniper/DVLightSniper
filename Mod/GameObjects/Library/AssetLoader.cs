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
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners.Packs;
using DVLightSniper.Mod.Util;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Logger = DVLightSniper.Mod.Util.Logger;
using Resources = DVLightSniper.Properties.Resources;
using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Library
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

            private Source source;

            /// <summary>
            /// The source of the asset bundle. Non-pure, attempts to determine the source if the
            /// bundle hasn't been loaded yet.
            /// </summary>
            internal Source Source
            {
                get
                {
                    if (this.source == Source.None && this.assetBundle == null)
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
                        this.source = Source.File;
                        return AssetBundle.LoadFromFile(fileName);
                    }
                }

                object data = Resources.ResourceManager.GetObject(name, Resources.Culture);
                if (data != null)
                {
                    this.source = Source.Resource;
                    return AssetBundle.LoadFromMemory((byte[])data);
                }

                if (!this.AssetLoader.IsBuiltin)
                {
                    string relativePath = Path.Combine(this.AssetLoader.Dir.RelativeToBaseDir(), name);
                    PackResource assetResource = PackLoader.OpenResource(relativePath);
                    if (assetResource != null)
                    {
                        this.source = Source.Pack;
                        return AssetBundle.LoadFromMemory(assetResource.Resource);
                    }
                }

                this.source = Source.None;
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

        /// <summary>
        /// Handle to an asset loaded (or to be loaded) from an assetbundle
        /// </summary>
        internal class AssetHandle
        {
            /// <summary>
            /// Assetbundle to load the asset from
            /// </summary>
            internal AssetBundleHandle AssetBundle { get; }

            /// <summary>
            /// Name of the asset to load
            /// </summary>
            internal string AssetName { get; }

            /// <summary>
            /// Loaded asset
            /// </summary>
            private Object asset;

            internal AssetHandle(AssetBundleHandle assetBundle, string assetName)
            {
                this.AssetBundle = assetBundle;
                this.AssetName = assetName;
            }

            internal T Load<T>() where T : Object
            {
                if (this.asset != null)
                {
                    return this.asset as T;
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

                this.asset = assetBundle.LoadAsset<T>(this.AssetName);
                if (asset == null)
                {
                    AssetLoader.DebugMessages.AddMessage($"Failed to load asset {this.AssetName} from assetbundle {this.AssetBundle}");
                    return null;
                }
                return this.asset as T;
            }
        }

        /// <summary>
        /// Handle to a texture resource which is (probably) loaded from a file on disk
        /// </summary>
        public class TextureHandle
        {
            /// <summary>
            /// AssetLoader for this asset collection
            /// </summary>
            public AssetLoader AssetLoader { get; }

            /// <summary>
            /// Filename to load the texture from
            /// </summary>
            public string Filename { get; }

            /// <summary>
            /// Texture resource to load the texture data into
            /// </summary>
            public Texture2D Texture { get; }

            /// <summary>
            /// Flag which indicates the file was changed on disk, used to trigger a reload of the
            /// sprite the next time it's updated
            /// </summary>
            public bool Changed { get; private set; }

            /// <summary>
            /// True if the file was loaded successfully
            /// </summary>
            public bool Valid { get; private set; }

            internal TextureHandle(AssetLoader assetLoader, string fileName, int textureSize)
            {
                this.AssetLoader = assetLoader;
                this.Filename = fileName;
                this.Texture = new Texture2D(textureSize, textureSize)
                {
                    filterMode = FilterMode.Point
                };

                if (this.AssetLoader.Dir != null)
                {
                    FileSystemWatcher watcher = new FileSystemWatcher(this.AssetLoader.Dir)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        Filter = fileName
                    };

                    this.Reload();

                    watcher.Changed += (sender, e) => this.Changed = true;
                    watcher.EnableRaisingEvents = true;
                }
                else
                {
                    this.Reload();
                }
            }

            /// <summary>
            /// Reloads the texture resource
            /// </summary>
            public void Reload()
            {
                if (!this.AssetLoader.IsBuiltin)
                {
                    string path = Path.Combine(this.AssetLoader.Dir, this.Filename);
                    if (File.Exists(path))
                    {
                        try
                        {
                            byte[] fileData = File.ReadAllBytes(path);
                            this.Texture.LoadImage(fileData);
                            this.Changed = false;
                            this.Valid = true;
                            return;
                        }
                        catch (Exception e)
                        {
                            LightSniper.Logger.Debug(e);
                        }
                    }
                }

                object data = Resources.ResourceManager.GetObject(this.Filename, Resources.Culture);
                if (data != null)
                {
                    try
                    {
                        this.Texture.LoadImage((byte[])data);
                        this.Changed = false;
                        this.Valid = true;
                        return;
                    }
                    catch (Exception e)
                    {
                        LightSniper.Logger.Debug(e);
                    }
                }

                if (!this.AssetLoader.IsBuiltin)
                {
                    PackResource resource = PackLoader.OpenResource(Path.Combine(this.AssetLoader.Dir.RelativeToBaseDir(), this.Filename));
                    if (resource != null)
                    {
                        this.Texture.LoadImage(resource.Resource);
                        this.Valid = true;
                        this.Changed = false;
                        return;
                    }
                }

                this.Valid = false;
            }
        }

        /// <summary>
        /// Asset loaders by type, all asset loaders apart from the builtin loader reference a dir
        /// under the Assets dir, eg. the Meshes loader references Assets/Meshes
        /// </summary>
        private static readonly IDictionary<string, AssetLoader> loaders = new Dictionary<string, AssetLoader>();

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
        /// True if this is the builtin loader, no base dir since assets are loaded from the
        /// assembly
        /// </summary>
        public bool IsBuiltin { get => this.Dir == null; }

        /// <summary>
        /// Matcher for resources which are valid bundles/resources for this loader
        /// </summary>
        public Func<string, bool> Matcher { get; set; } = (key) => true;

        /// <summary>
        /// Asset bundles this loader has loaded
        /// </summary>
        private readonly IDictionary<string, AssetBundleHandle> assetBundles = new Dictionary<string, AssetBundleHandle>();

        /// <summary>
        /// Assets this loader has loader (or will load)
        /// </summary>
        private readonly IDictionary<string, AssetHandle> assets = new Dictionary<string, AssetHandle>();

        /// <summary>
        /// Textures this loader has loaded
        /// </summary>
        private readonly IDictionary<string, TextureHandle> textures = new Dictionary<string, TextureHandle>();

        /// <summary>
        /// Get the builtin asset loader
        /// </summary>
        public static AssetLoader Builtin
        {
            get
            {
                if (!AssetLoader.loaders.ContainsKey(""))
                {
                    AssetLoader.loaders[""] = new AssetLoader(null);
                }

                return AssetLoader.loaders[""];
            }
        }

        /// <summary>
        /// Get the meshes asset loader
        /// </summary>
        public static AssetLoader Meshes
        {
            get => AssetLoader.Of("Meshes", (key) => key.StartsWith("meshes_") && key.EndsWith(".assetbundle"));
        }

        /// <summary>
        /// Get the coronas asset loader
        /// </summary>
        public static AssetLoader Coronas
        {
            get => AssetLoader.Of("Coronas", (key) => key.StartsWith("corona_") && key.EndsWith(".png"));
        }

        /// <summary>
        /// Get an asset loader for the specified asset collection
        /// </summary>
        /// <param name="subDir"></param>
        /// <param name="matcher"></param>
        /// <returns></returns>
        public static AssetLoader Of(string subDir, Func<string, bool> matcher = null)
        {
            string dir = Path.Combine(AssetLoader.AssetsDir, subDir);
            string key = dir.ToLowerInvariant();
            if (!AssetLoader.loaders.ContainsKey(key))
            {
                AssetLoader.loaders[key] = new AssetLoader(dir);
            }

            if (matcher != null)
            {
                AssetLoader.loaders[key].Matcher = matcher;
            }

            return AssetLoader.loaders[key];
        }

    
        private AssetLoader(string dir)
        {
            this.Dir = dir;
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
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public T LoadBuiltin<T>(string assetName) where T : UnityEngine.Object
        {
            return this.IsBuiltin ? this.Load<T>("builtin.assetbundle", assetName) : null;
        }

        /// <summary>
        /// Load an asset from the specified asset bundle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetBundleName"></param>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public T Load<T>(string assetBundleName, string assetName) where T : UnityEngine.Object
        {
            string key = assetBundleName + ":" + assetName;
            if (!this.assets.ContainsKey(key))
            {
                this.assets[key] = new AssetHandle(this.GetAssetBundle(assetBundleName), assetName);
            }
            return this.assets[key].Load<T>();
        }

        /// <summary>
        /// Load a texture
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="textureSize"></param>
        /// <returns></returns>
        public TextureHandle GetTexture(string fileName, int textureSize)
        {
            string key = fileName + ":" + textureSize;
            if (!this.textures.ContainsKey(key))
            {
                this.textures[key] = new TextureHandle(this, fileName, textureSize);
            }
            return this.textures[key];
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
        /// List all files available to this loader which match the current Matcher
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> ListFiles()
        {
            ISet<string> matches = new HashSet<string>();
            this.FilterFiles(Directory.EnumerateFiles(this.Dir, "*.*"), matches);
            this.FilterFiles(PackLoader.Find(this.Dir.RelativeToBaseDir() + "\\"), matches);
            return matches;
        }

        private void FilterFiles(IEnumerable<string> files, ISet<string> matches)
        {
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                if (fileName != null && this.Matcher(fileName))
                {
                    matches.Add(fileName);
                }
            }
        }

        /// <summary>
        /// List resources which match the Matcher criteria for this loader
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> ListResources()
        {
            return (from DictionaryEntry resource in Resources.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true)
                    where this.Matcher(resource.Key.ToString())
                    select resource.Key.ToString()).ToArray();
        }

        /// <summary>
        /// Extract all resources which match this loader's matcher criteria
        /// </summary>
        public void ExtractResources()
        {
            AssetLoader.ExtractResources(this.Dir, this.Matcher);
        }

        /// <summary>
        /// Extract all resources which match the supplied matcher criteria
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="matcher"></param>
        public static void ExtractResources(string dir, Func<string, bool> matcher)
        {
            Directory.CreateDirectory(dir);
            foreach (DictionaryEntry resource in Resources.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true))
            {
                if (!matcher(resource.Key.ToString()))
                {
                    continue;
                }

                string extractPath = Path.Combine(dir, resource.Key.ToString());
                if (File.Exists(extractPath))
                {
                    continue;
                }

                object data = Resources.ResourceManager.GetObject(resource.Key.ToString(), Resources.Culture);
                if (data != null)
                {
                    File.WriteAllBytes(extractPath, (byte[])data);
                }
            }
        }

        /// <summary>
        /// Extract a texture resource if it is located in a pack
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool ExtractTexture(string fileName)
        {
            string path = Path.Combine(this.Dir, fileName);
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
                File.WriteAllBytes(path, resource.Resource);
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error("Error extracting texture {0} from pack " + e.Message);
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
