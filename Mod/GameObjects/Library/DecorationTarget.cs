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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using Newtonsoft.Json;

namespace DVLightSniper.Mod.GameObjects.Library
{
    /// <summary>
    /// Target information for a decoration, defining a renderer or gameobject name that 
    /// decoration is applicable to, and the properties to apply when applying the decoration to the
    /// mesh. The target is prefixed with "@" to indicate a GameObject name, otherwise it is assumed
    /// to be a renderer name.
    /// </summary>
    [DataContract]
    internal class DecorationTarget
    {
        [DataMember(Name = "target", Order = 0)]
        internal string Target { get; set; }

        [DataMember(Name = "mesh", Order = 1)]
        internal MeshProperties Properties { get; set; }

        [DataMember(Name = "dutyCycle", Order = 2, EmitDefaultValue = false)]
        private string dutyCycleDef;

        private DutyCycle dutyCycle;

        internal DutyCycle DutyCycle
        {
            get
            {
                return this.dutyCycle ?? (this.dutyCycle = DutyCycle.Parse(this.dutyCycleDef));
            }
            set
            {
                this.dutyCycle = (value ?? DutyCycle.ALWAYS);
                this.dutyCycleDef = this.dutyCycle.ToString();
            }
        }

        [JsonConstructor]
        internal DecorationTarget(string target, string assetBundleName, string assetName)
            : this(target, new MeshProperties(assetBundleName, assetName))
        {
        }

        internal DecorationTarget(string target, MeshProperties properties)
        {
            this.Target = target;
            this.Properties = properties;
        }

        internal DecorationTarget WithDutyCycle(DutyCycle dutyCycle)
        {
            this.dutyCycle = dutyCycle;
            return this;
        }

        internal DecorationTarget WithMaterialAssignment(string rendererName, int from = 0)
        {
            this.Properties.WithMaterialAssignment(rendererName, from);
            return this;
        }

        internal DecorationTarget WithRandomAlpha(float randomAlpha)
        {
            this.Properties.WithRandomAlpha(randomAlpha);
            return this;
        }

        internal DecorationTarget HideSourcePrefab(bool value)
        {
            this.Properties.HideSourcePrefab(value);
            return this;
        }
    }
}
