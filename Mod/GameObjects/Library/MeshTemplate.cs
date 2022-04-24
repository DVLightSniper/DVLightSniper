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
using System.Runtime.Serialization;

using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using Newtonsoft.Json;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Library
{
    /// <summary>
    /// A mesh template with corresponding light offsets
    /// </summary>
    [DataContract]
    internal class MeshTemplate
    {
        /// <summary>
        /// For placement on floor surfaces, meshes can decide whether to orient the "up" direction
        /// toward positive Y (directly up) or align to the surface normal where they are placed
        /// </summary>
        internal enum AlignmentType
        {
            AlignToAxis,
            AlignToNormal
        }

        [DataMember(Name = "properties", Order = 0)]
        internal MeshProperties Properties { get; set; }

        [DataMember(Name = "alignment", Order = 1, EmitDefaultValue = false)]
        internal AlignmentType Alignment { get; set; } = AlignmentType.AlignToAxis;

        [JsonConstructor]
        internal MeshTemplate(string assetBundleName, string assetName)
            : this(new MeshProperties(assetBundleName, assetName))
        {
        }

        internal MeshTemplate(MeshProperties properties)
        {
            this.Properties = properties;
        }
    }
}
