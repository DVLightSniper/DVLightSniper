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

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{
    /// <summary>
    /// Type of asset, used to keep information about where certain asset types should be stored and
    /// their corresponding unity asset type
    /// </summary>
    public sealed class AssetType
    {
        internal delegate IAsset<Object> AssetFactory(AssetLoader assetLoader, string assetBundleName, string assetName, AssetLoader.LookupFlags lookupFlags);

        /// <summary>
        /// All asset types
        /// </summary>
        private static readonly List<AssetType> types = new List<AssetType>();

        public static readonly AssetType TEXTURE = new AssetType("Texture", typeof(Texture2D), "Textures", new[] { ".png", ".jpg", ".jpeg" }, (l, b, a, f) => new TextureAsset(l, b, a, f));
        public static readonly AssetType SOUND = new AssetType("Sound", typeof(AudioClip), "Sounds", new[] { ".wav", ".mp3" }, (l, b, a, f) => new SoundAsset(l, b, a, f));

        public static IEnumerable<AssetType> Types
        {
            get => AssetType.types.AsReadOnly();
        }

        /// <summary>
        /// Name of this asset type, for error messages and other human-readable uses
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Type of unity asset type associated with this asset
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Subdirectory under which assets of this type are stored
        /// </summary>
        public string Dir { get; }

        private readonly AssetFactory factory;

        private readonly string[] extensions;

        private AssetType(string name, Type type, string dir, string[] extensions, AssetFactory factory)
        {
            this.Name = name;
            this.Type = type;
            this.Dir = dir;
            this.extensions = extensions;
            this.factory = factory;

            AssetType.types.Add(this);
        }

        public IEnumerable<string> GetExtensions(params string[] additionalExtensions)
        {
            ISet<string> availableExtensions = new HashSet<string>(this.extensions);
            foreach (string extension in additionalExtensions)
            {
                availableExtensions.Add(extension.ToLowerInvariant());
            }
            return availableExtensions;
        }

        public static AssetType FromExtension(string fileName)
        {
            string fileExtension = Path.GetExtension(fileName);
            return AssetType.types.FirstOrDefault(type => type.extensions.Contains(fileExtension));
        }

        public static AssetType FromObject(Object asset)
        {
            return AssetType.FromObjectType(asset.GetType());
        }

        public static AssetType FromObjectType<TAsset>()
        {
            return AssetType.FromObjectType(typeof(TAsset));
        }

        public static AssetType FromObjectType(Type tAsset)
        {
            return AssetType.types.FirstOrDefault(type => tAsset == type.Type || tAsset.IsSubclassOf(type.Type));
        }

        internal IAsset<TAsset> Create<TAsset>(AssetLoader assetLoader, string assetBundleName, string assetName, AssetLoader.LookupFlags lookupFlags) where TAsset : Object
        {
            return (IAsset<TAsset>)this.factory(assetLoader, assetBundleName, assetName, lookupFlags);
        }

        public static void CreateDirectories(string dir)
        {
            foreach (AssetType type in AssetType.types)
            {
                Directory.CreateDirectory(Path.Combine(dir, type.Dir));
            }
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
}
