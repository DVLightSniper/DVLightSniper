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
using DVLightSniper.Mod.Storage;
using DVLightSniper.Properties;

using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{

    /// <summary>
    /// Handle to an asset resource which is (probably) loaded from a file on disk
    /// </summary>
    internal class FileAsset<TAsset> : IAsset<TAsset> where TAsset : UnityEngine.Object
    {
        /// <summary>
        /// AssetLoader for this asset collection
        /// </summary>
        public AssetLoader AssetLoader { get; }

        /// <summary>
        /// Asset bundle name if loading an override asset
        /// </summary>
        public string AssetBundleName { get; }

        /// <summary>
        /// Filename to load the texture from
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// Actual asset resource to load the asset data into
        /// </summary>
        public TAsset Asset { get; protected set; }

        /// <inheritdoc />
        public bool Changed { get; private set; }

        /// <inheritdoc />
        public bool Valid { get; private set; }

        /// <summary>
        /// The resolved name of the asset if it is successfully loaded
        /// </summary>
        public string ResolvedName { get; private set; }

        protected readonly string originalExtension;

        protected readonly AssetLoader.LookupFlags lookupFlags;

        internal string AbsoluteRootPath
        {
            get => Path.Combine(this.AssetLoader.Dir, this.Type.Dir);
        }

        internal string RelativeRootPath
        {
            get => Path.Combine(this.AssetLoader.Dir.RelativeToBaseDir(), this.Type.Dir);
        }

        internal string AbsoluteBundlePath
        {
            get => Path.Combine(this.AssetLoader.Dir, Path.GetFileNameWithoutExtension(this.AssetBundleName), this.Type.Dir);
        }

        internal string RelativeBundlePath
        {
            get => Path.Combine(this.AssetLoader.Dir.RelativeToBaseDir(), Path.GetFileNameWithoutExtension(this.AssetBundleName), this.Type.Dir);
        }

        public AssetType Type
        {
            get;
        }

        internal FileAsset(AssetLoader assetLoader, AssetType type, string assetBundleName, string assetName, AssetLoader.LookupFlags lookupFlags = AssetLoader.LookupFlags.Default)
        {
            this.AssetLoader = assetLoader;
            this.Type = type;
            this.AssetBundleName = assetBundleName;
            this.ResolvedName = assetName;
            this.AssetName = Path.GetFileNameWithoutExtension(assetName);
            this.originalExtension = Path.GetExtension(assetName);
            this.lookupFlags = lookupFlags;

            this.Asset = this.InitAsset();

            FileSystemWatcher watcher = null;
            if (this.AssetLoader.Dir != null && this.lookupFlags.HasFlag(AssetLoader.LookupFlags.LookupInRoot))
            {
                watcher = new FileSystemWatcher(this.AbsoluteRootPath)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = this.AssetName + ".*"
                };
            }

            this.Reload();

            if (watcher != null)
            {
                watcher.Changed += (sender, e) => this.Changed = true;
                watcher.EnableRaisingEvents = true;
            }
        }

        public override string ToString()
        {
            return this.ResolvedName;
        }

        private TAsset InitAsset()
        {
            MetaStorage meta = null;
            string path = this.FindFile();
            if (path != null)
            {
                meta = MetaStorage.Load(path + ".meta");
            }
            else
            {
                PackStream stream = this.FindPackResource();
                if (stream != null)
                {
                    meta = stream.Pack.OpenMeta(stream.Name + ".meta")?.MetaStorage;
                }
            }

            return this.CreateAsset(meta ?? this.AssetLoader.Meta);
        }

        protected virtual TAsset CreateAsset(MetaStorage meta)
        {
            // stub
            return null;
        }

        /// <inheritdoc />
        public void Reload()
        {
            if (!this.AssetLoader.IsBuiltin)
            {
                try
                {
                    string path = this.FindFile();
                    if (path != null && this.LoadFromFile(path))
                    {
                        this.ResolvedName = path.RelativeToBaseDir();
                        this.Changed = false;
                        this.Valid = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Debug(e);
                }
            }

            object data = Resources.ResourceManager.GetObject(this.AssetName + this.originalExtension, Resources.Culture);
            if (data != null)
            {
                try
                {
                    if (this.LoadFromResource((byte[])data))
                    {
                        this.ResolvedName = $"resources:{this.AssetName}{this.originalExtension}";
                        this.Changed = false;
                        this.Valid = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Debug(e);
                }
            }

            if (!this.AssetLoader.IsBuiltin)
            {
                PackStream resource = this.FindPackResource();
                try
                {
                    if (resource != null && this.LoadFromPack(resource))
                    {
                        this.ResolvedName = $"{resource.Pack.Name}:{resource.Name}";
                        this.Changed = false;
                        this.Valid = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Debug(e);
                }
            }

            this.Valid = false;
        }

        protected virtual bool LoadFromFile(string path)
        {
            return this.LoadAsset(File.ReadAllBytes(path));
        }

        protected virtual bool LoadFromResource(byte[] data)
        {
            return this.LoadAsset(data);
        }

        protected virtual bool LoadFromPack(PackStream stream)
        {
            return this.LoadAsset(stream.GetData());
        }

        protected virtual bool LoadAsset(byte[] fileData)
        {
            // stub
            return false;
        }

        internal bool Exists()
        {
            if (!this.AssetLoader.IsBuiltin && this.FindFile() != null)
            {
                return true;
            }

            if (Resources.ResourceManager.GetObject(this.AssetName + this.originalExtension, Resources.Culture) != null)
            {
                return true;
            }

            return !this.AssetLoader.IsBuiltin && this.FindPackResource() != null;
        }

        private PackStream FindPackResource()
        {
            if (this.lookupFlags.HasFlag(AssetLoader.LookupFlags.LookupInBundleDir) && !string.IsNullOrEmpty(this.AssetBundleName))
            {
                PackStream stream = this.FindPackResource(this.RelativeBundlePath);
                if (stream != null)
                {
                    return stream;
                }
            }

            if (this.lookupFlags.HasFlag(AssetLoader.LookupFlags.LookupInRoot))
            {
                PackStream stream = this.FindPackResource(this.RelativeRootPath);
                if (stream != null)
                {
                    return stream;
                }
            }

            return null;
        }

        private PackStream FindPackResource(string dir)
        {
            foreach (string extension in this.Type.GetExtensions(this.originalExtension))
            {
                PackStream stream = PackLoader.OpenStream(Path.Combine(dir, this.AssetName + extension));
                if (stream != null)
                {
                    return stream;
                }
            }
            return null;
        }

        private string FindFile()
        {
            if (this.lookupFlags.HasFlag(AssetLoader.LookupFlags.LookupInBundleDir) && !string.IsNullOrEmpty(this.AssetBundleName))
            {
                string path = this.FindFile(this.AbsoluteBundlePath);
                if (path != null)
                {
                    return path;
                }
            }

            if (this.lookupFlags.HasFlag(AssetLoader.LookupFlags.LookupInRoot))
            {
                string path = this.FindFile(this.AbsoluteRootPath);
                if (path != null)
                {
                    return path;
                }
            }

            return null;
        }

        private string FindFile(string dir)
        {
            foreach (string extension in this.Type.GetExtensions(this.originalExtension))
            {
                string path = Path.Combine(dir, this.AssetName + extension);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        public static bool Exists(AssetLoader assetLoader, string assetBundleName, string assetName, AssetLoader.LookupFlags lookupFlags = AssetLoader.LookupFlags.Default)
        {
            return AssetType.FromObjectType<TAsset>() != null && new FileAsset<Object>(assetLoader, AssetType.FromObjectType<TAsset>(), assetBundleName, assetName, lookupFlags).Exists();
        }

        public static IAsset<TAsset> Create(AssetLoader assetLoader, string assetBundleName, string assetName, AssetLoader.LookupFlags lookupFlags = AssetLoader.LookupFlags.Default)
        {
            return AssetType.FromObjectType<TAsset>()?.Create<TAsset>(assetLoader, assetBundleName, assetName, lookupFlags);
        }
    }
}
