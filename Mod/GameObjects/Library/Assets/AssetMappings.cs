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
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Storage;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{
    /// <summary>
    /// Mappings of asset bundle names as they appear in the JSON files, to actual files on disk.
    /// This is here mainly so that I can update the internal asset bundles in future versions
    /// without needing to update the name in every single JSON.
    /// </summary>
    [DataContract]
    internal class AssetMappings : JsonStorage
    {
        [DataMember(Name = "version", Order = 0)]
        private int version;

        [DataMember(Name = "mappings", Order = 1)]
        private Dictionary<string, string> mappings = new Dictionary<string, string>();

        // for internal mappings
        private readonly Dictionary<string, string> hashes = new Dictionary<string, string>();

        internal static AssetMappings Load(params string[] path)
        {
            AssetMappings mappings = JsonStorage.Load<AssetMappings>(path);
            if (mappings?.mappings == null || mappings.mappings.Count < 1)
            {
                mappings = new AssetMappings { FileName = Path.Combine(path) };
            }

            mappings.AddDefaults();
            return mappings;
        }

        private void AddDefaults()
        {
            bool dirty = false;

            int oldVersion = this.version;

            // Version 1 mappings
            dirty |= this.InternalMapping(oldVersion, 1, AssetLoader.Meshes, "meshes_decoration_lights.assetbundle", "meshes_decoration_lights_1.0.assetbundle", "68-D9-83-6A-9A-21-D6-AE-F0-CC-D7-0F-09-D1-85-AD-CF-BF-19-0E");
            dirty |= this.InternalMapping(oldVersion, 1, AssetLoader.Meshes, "meshes_lampposts.assetbundle",         "meshes_lampposts_1.0.assetbundle",         "20-03-19-45-FA-41-12-1B-43-29-BC-77-1A-DC-BE-B9-5A-4A-AD-08");
            dirty |= this.InternalMapping(oldVersion, 1, AssetLoader.Meshes, "meshes_levelcrossings.assetbundle",    "meshes_levelcrossings_1.0.assetbundle",    "4D-16-ED-CA-80-CE-BD-86-2F-3E-48-86-12-7E-C0-1E-1C-A2-CB-EF");

            // Version 2 mappings
            dirty |= this.InternalMapping(oldVersion, 2, AssetLoader.Meshes, "meshes_decoration_lights.assetbundle", "meshes_decoration_lights_1.1.assetbundle", null);
            dirty |= this.InternalMapping(oldVersion, 2, AssetLoader.Meshes, "meshes_levelcrossings.assetbundle",    "meshes_levelcrossings_1.1.assetbundle",    null);
            dirty |= this.InternalMapping(oldVersion, 2, AssetLoader.Meshes, "meshes_signs.assetbundle",             "meshes_signs_1.0.assetbundle",             null);

            if (dirty)
            {
                this.Save();
            }
        }

        private bool InternalMapping(int oldVersion, int version, AssetLoader assetLoader, string assetBundleName, string versionedAssetBundleName, string sha1hash)
        {
            bool dirty = false;

            if (oldVersion < version)
            {
                // Clean up the old asset bundle if the sha1 matches the known version (eg. the user
                // hasn't modified the file in the mean time)
                if (this.mappings.ContainsKey(assetBundleName))
                {
                    string existing = this.mappings[assetBundleName];
                    if (this.hashes.ContainsKey(existing))
                    {
                        string existingFile = Path.Combine(assetLoader.Dir, existing);
                        if (File.Exists(existingFile) && existingFile.GetSha1Hash() == this.hashes[existing])
                        {
                            File.Delete(existingFile);
                            this.mappings.Remove(assetBundleName);
                        }
                    }
                }

                this.mappings[assetBundleName] = versionedAssetBundleName;
                this.version = version;
                dirty = true;
            }

            if (sha1hash != null)
            {
                this.hashes[versionedAssetBundleName] = sha1hash;
            }

            return dirty;
        }

        internal string this[string assetBundleName]
        {
            get
            {
                return this.mappings.ContainsKey(assetBundleName) ? this.mappings[assetBundleName] : assetBundleName;
            }
            set
            {
                this.mappings[assetBundleName] = value;
                this.Save();
            }
        }

        internal bool Contains(string assetBundleName)
        {
            return this.mappings.ContainsKey(assetBundleName);
        }
    }
}
