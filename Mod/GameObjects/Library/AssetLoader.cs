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

        public enum Source
        {
            None,
            File,
            Resource,
            Pack
        }

        internal class AssetHandle
        {
            internal string Dir { get; }

            internal string AssetBundleName { get; }

            internal string MappedName { get; }

            internal string AssetName { get; }

            private Object asset;

            internal AssetHandle(string dir, string assetBundleName, string mappedName, string assetName)
            {
                this.Dir = dir;
                this.AssetBundleName = assetBundleName;
                this.MappedName = mappedName;
                this.AssetName = assetName;
            }

            internal T Load<T>() where T : Object
            {
                if (this.asset != null)
                {
                    return this.asset as T;
                }

                AssetBundle assetBundle = this.LoadAssetBundle(out Source _);
                if (assetBundle == null)
                {
                    AssetLoader.DebugMessages.AddMessage($"Failed loading assetbundle {this.AssetBundleName} ({this.MappedName})");
                    return null;
                }

                if (string.IsNullOrEmpty(this.AssetName)) // discovery marker, should never get here
                {
                    return null;
                }

                try
                {
                    this.asset = assetBundle.LoadAsset<T>(this.AssetName);
                    if (asset == null)
                    {
                        AssetLoader.DebugMessages.AddMessage($"Failed to load asset {this.AssetName} from assetbundle {this.AssetBundleName} ({this.MappedName})");
                        return null;
                    }
                    return this.asset as T;
                }
                finally
                {
                    assetBundle.Unload(false);
                }
            }

            internal Source FindSource()
            {
                AssetBundle assetBundle = this.LoadAssetBundle(out Source source);
                assetBundle?.Unload(false);
                return source;
            }

            internal bool ExtractFromPack()
            {
                string fileName = Path.Combine(this.Dir, this.MappedName);
                if (File.Exists(fileName))
                {
                    return true;
                }

                string relativePath = Path.Combine(this.Dir.RelativeToBaseDir(), this.MappedName);
                byte[] assetResource = PackLoader.OpenResource(relativePath);
                if (assetResource == null)
                {
                    return false;
                }

                try
                {
                    File.WriteAllBytes(fileName, assetResource);
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Error("Error extracting resource from pack " + e.Message);
                    return false;
                }
                return true;
            }

            private AssetBundle LoadAssetBundle(out Source source)
            {
                if (this.Dir != null)
                {
                    string fileName = Path.Combine(this.Dir, this.MappedName);
                    if (File.Exists(fileName))
                    {
                        source = Source.File;
                        return AssetBundle.LoadFromFile(fileName);
                    }
                }

                object data = Resources.ResourceManager.GetObject(this.MappedName, Resources.Culture);
                if (data != null)
                {
                    source = Source.Resource;
                    return AssetBundle.LoadFromMemory((byte[])data);
                }

                string relativePath = Path.Combine(this.Dir.RelativeToBaseDir(), this.MappedName);
                byte[] assetResource = PackLoader.OpenResource(relativePath);
                if (assetResource != null)
                {
                    source = Source.Pack;
                    return AssetBundle.LoadFromMemory(assetResource);
                }

                source = Source.None;
                return null;
            }
        }

        private static readonly IDictionary<string, AssetLoader> loaders = new Dictionary<string, AssetLoader>();

        private static AssetMappings mappings;

        private static AssetMappings AssetMappings
        {
            get
            {
                return AssetLoader.mappings ?? (AssetLoader.mappings = AssetMappings.Load(LightSniper.Path, "Resources", "assetbundle.mappings.json"));
            }
        }

        public string Dir { get; }

        public Func<string, bool> Matcher { get; set; } = (key) => true;

        private readonly IDictionary<string, AssetHandle> assets = new Dictionary<string, AssetHandle>();

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

        public static AssetLoader Meshes
        {
            get => AssetLoader.Of("Meshes");
        }

        public static AssetLoader Coronas
        {
            get => AssetLoader.Of("Coronas");
        }

        public static AssetLoader Of(string subDir, Func<string, bool> matcher = null)
        {
            string dir = Path.Combine(LightSniper.Path, "Assets", subDir);
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

        public T LoadBuiltin<T>(string assetName) where T : UnityEngine.Object
        {
            return this.Load<T>("builtin.assetbundle", assetName);
        }

        public T Load<T>(string assetBundleName, string assetName) where T : UnityEngine.Object
        {
            string key = assetBundleName + ":" + assetName;
            if (!this.assets.ContainsKey(key))
            {
                string mappedName = AssetLoader.AssetMappings[assetBundleName];
                this.assets[key] = new AssetHandle(this.Dir, assetBundleName, mappedName, assetName);
            }
            return this.assets[key].Load<T>();
        }

        public Source FindSource(string assetBundleName)
        {
            string mappedName = AssetLoader.AssetMappings[assetBundleName];
            return new AssetHandle(this.Dir, assetBundleName, mappedName, "").FindSource();
        }

        public bool ExtractFromPack(string assetBundleName)
        {
            string mappedName = AssetLoader.AssetMappings[assetBundleName];
            return new AssetHandle(this.Dir, assetBundleName, mappedName, "").ExtractFromPack();
        }

        public string[] ListResources()
        {
            return (from DictionaryEntry resource in Resources.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true)
                    where this.Matcher(resource.Key.ToString())
                    select resource.Key.ToString()).ToArray();
        }

        public void ExtractResources()
        {
            AssetLoader.ExtractResources(this.Dir, this.Matcher);
        }

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
    }
}
