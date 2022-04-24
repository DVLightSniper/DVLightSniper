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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;

using Newtonsoft.Json;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// Properties defining a sniped mesh
    /// </summary>
    [DataContract]
    internal class MeshProperties
    {
        /// <summary>
        /// Filename for the asset bundle to load from
        /// </summary>
        [DataMember(Name = "file", Order = 0)]
        internal string AssetBundleName { get; set; }

        /// <summary>
        /// Name of the prefab within the asset bundle
        /// </summary>
        [DataMember(Name = "mesh", Order = 1)]
        internal string AssetName { get; set; }

        /// <summary>
        /// Material assignments to apply
        /// </summary>
        [DataMember(Name = "assignMaterials", Order = 2, EmitDefaultValue = false)]
        private MaterialAssignments materialAssignments;

        internal MaterialAssignments MaterialAssignments
        {
            get
            {
                return this.materialAssignments ?? (this.materialAssignments = new MaterialAssignments());
            }
            set
            {
                this.materialAssignments = value;
            }
        }

        [JsonConstructor]
        internal MeshProperties(string assetBundleName, string assetName)
        {
            this.AssetBundleName = assetBundleName;
            this.AssetName = assetName;
        }

        internal MeshProperties HideSourcePrefab(bool value)
        {
            this.MaterialAssignments.HideSourcePrefab = value;
            return this;
        }

        internal MeshProperties WithMaterialAssignment(string rendererName, int from = 0)
        {
            this.MaterialAssignments.Add(new MaterialAssignment(rendererName, from));
            return this;
        }

        internal MeshProperties WithRandomAlpha(float randomAlpha)
        {
            this.MaterialAssignments.RandomAlpha = randomAlpha;
            return this;
        }

        public override string ToString()
        {
            return $"MeshProperties(File={this.AssetBundleName} Mesh={this.AssetName})";
        }
    }

}
