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
            dirty |= this.InternalMapping(oldVersion, 1, AssetLoader.Meshes, "meshes_decoration_lights.assetbundle", "meshes_decoration_lights_1.0.assetbundle", "68D9836A9A21D6AEF0CCD70F09D185ADCFBF190E");
            dirty |= this.InternalMapping(oldVersion, 1, AssetLoader.Meshes, "meshes_lampposts.assetbundle",         "meshes_lampposts_1.0.assetbundle",         "20031945FA41121B4329BC771ADCBEB95A4AAD08");
            dirty |= this.InternalMapping(oldVersion, 1, AssetLoader.Meshes, "meshes_levelcrossings.assetbundle",    "meshes_levelcrossings_1.0.assetbundle",    "4D16EDCA80CEBD862F3E4886127EC01E1CA2CBEF");

            // Version 2 mappings
            dirty |= this.InternalMapping(oldVersion, 2, AssetLoader.Meshes, "meshes_decoration_lights.assetbundle", "meshes_decoration_lights_1.1.assetbundle", "49A706BE0389BCECF08013E26A798B1CF1781C93");
            dirty |= this.InternalMapping(oldVersion, 2, AssetLoader.Meshes, "meshes_levelcrossings.assetbundle",    "meshes_levelcrossings_1.1.assetbundle",    "19B8820FB02AAF3EAF08D3F2DC5D328D7A697473");
            dirty |= this.InternalMapping(oldVersion, 2, AssetLoader.Meshes, "meshes_signs.assetbundle",             "meshes_signs_1.0.assetbundle",             "0D879A923160220C9256584F6C47AA0C1EB564C2");

            // Version 3 mappings
            dirty |= this.InternalMapping(oldVersion, 3, AssetLoader.Meshes, "meshes_decoration_lights.assetbundle", "meshes_decoration_lights_1.2.assetbundle", "149D10216E225481E4A322DBE54A094001A00FE8");
            dirty |= this.InternalMapping(oldVersion, 3, AssetLoader.Meshes, "meshes_lampposts.assetbundle",         "meshes_lampposts_1.1.assetbundle",         "89A01F1C0657EF35ECB7DA4963A6FB75408A87C7");
            dirty |= this.InternalMapping(oldVersion, 3, AssetLoader.Meshes, "meshes_levelcrossings.assetbundle",    "meshes_levelcrossings_1.2.assetbundle",    "B440FE50A990A63EDD81882E1026D8FBDADF4EF0");
            dirty |= this.InternalMapping(oldVersion, 3, AssetLoader.Meshes, "meshes_signs.assetbundle",             "meshes_signs_1.1.assetbundle",             "5A8B289922625423B35C7F5DEEADCFB39B056DC3");

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
