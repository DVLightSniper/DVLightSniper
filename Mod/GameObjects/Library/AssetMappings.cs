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
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Storage;

namespace DVLightSniper.Mod.GameObjects.Library
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
            bool saveRequired = false;

            // Add mappings for version 1 if saved version is lower
            if (this.version < 1)
            {
                this["meshes_decoration_lights.assetbundle"] = "meshes_decoration_lights_1.0.assetbundle";
                this["meshes_lampposts.assetbundle"] = "meshes_lampposts_1.0.assetbundle";
                this["meshes_levelcrossings.assetbundle"] = "meshes_levelcrossings_1.0.assetbundle";
                this.version = 1;
                saveRequired = true;
            }

            if (saveRequired)
            {
                this.Save();
            }
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
