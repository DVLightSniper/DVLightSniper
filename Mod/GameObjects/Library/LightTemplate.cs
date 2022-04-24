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

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.Util;

using Newtonsoft.Json;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.GameObjects.Library
{
    /// <summary>
    /// A light template, or mesh template with lights.
    /// </summary>
    [DataContract]
    internal class LightTemplate : Template
    {
        [DataMember(Name = "lights", Order = 2)]
        internal List<LightProperties> Lights { get; private set; } = new List<LightProperties>();

        [DataMember(Name = "meshes", Order = 3)]
        internal Dictionary<MeshOrientation, MeshTemplate> Meshes { get; private set; } = new Dictionary<MeshOrientation, MeshTemplate>();

        [DataMember(Name = "snipeOffset", Order = 4)]
        internal float SnipeOffset { get; private set; } = 0.1F;

        private MeshTemplate meshBuilder;

        public MeshTemplate MeshBuilder
        {
            get
            {
                if (this.meshBuilder == null)
                {
                    throw new InvalidOperationException("MeshBuilder is null");
                }
                return this.meshBuilder;
            }
        }
        private LightProperties lightBuilder;

        public LightProperties LightBuilder
        {
            get
            {
                if (this.lightBuilder == null)
                {
                    throw new InvalidOperationException("LightBuilder is null");
                }
                return this.lightBuilder;
            }
        }

        internal bool HasLights
        {
            get { return this.Lights.Count > 0; }
        }

        internal bool HasMeshes
        {
            get { return this.Meshes.Count > 0; }
        }

        [JsonConstructor]
        internal LightTemplate(string name)
        {
            this.Name = name;
        }

        // For compatibility with TTemplate
        internal LightTemplate(string id, string name) : this(name) { }

        internal bool HasMesh(MeshOrientation orientation)
        {
            return this.Meshes.ContainsKey(orientation);
        }

        internal MeshTemplate GetMesh(MeshOrientation orientation)
        {
            if (this.Meshes.ContainsKey(orientation))
            {
                return this.Meshes[orientation];
            }

            return orientation > 0 ? this.GetMesh(--orientation) : null;
        }

        internal LightTemplate WithSnipeOffset(float snipeOffset)
        {
            this.SnipeOffset = snipeOffset;
            return this;
        }

        internal LightTemplate WithAlignment(MeshTemplate.AlignmentType alignmentType)
        {
            this.MeshBuilder.Alignment = alignmentType;
            return this;
        }

        internal LightTemplate WithLight(float intensity, float range, Color colour, LightType type = LightType.Point)
        {
            return this.WithLight(new LightProperties(intensity, range, colour, type));
        }

        internal LightTemplate WithLight(LightProperties light)
        {
            this.Lights.Add(light);
            this.lightBuilder = light;
            return this;
        }

        internal LightTemplate WithLights(LightTemplate template)
        {
            this.Lights.AddRange(template.Lights);
            return this;
        }

        internal LightTemplate WithCorona(string sprite, float scale = 1.0F, float range = 100.0F)
        {
            this.LightBuilder.Corona = new CoronaProperties(sprite, scale, range);
            return this;
        }

        internal LightTemplate WithDutyCycle(DutyCycle dutyCycle)
        {
            this.LightBuilder.DutyCycle = dutyCycle;
            return this;
        }

        internal LightTemplate WithMesh(MeshOrientation orientation, string assetBundleName, string assetName)
        {
            return this.WithMesh(orientation, new MeshProperties(assetBundleName, assetName));
        }

        internal LightTemplate WithMesh(MeshOrientation orientation, MeshProperties mesh)
        {
            this.meshBuilder = this.Meshes[orientation] = new MeshTemplate(mesh);
            return this;
        }

        internal LightTemplate WithMeshes(LightTemplate template)
        {
            foreach (KeyValuePair<MeshOrientation, MeshTemplate> templateMesh in template.Meshes)
            {
                this.Meshes[templateMesh.Key] = templateMesh.Value;
            }
            return this;
        }

        internal LightTemplate NeverCull()
        {
            this.LightBuilder.NeverCull = true;
            return this;
        }

        internal void ExtractAssetBundles()
        {
            ISet<string> assetBundleNames = new HashSet<string>();

            foreach (MeshTemplate meshTemplate in this.Meshes.Values)
            {
                string assetBundleName = meshTemplate.Properties.AssetBundleName;
                if (assetBundleNames.Add(assetBundleName))
                {
                    AssetLoader.Meshes.ExtractFromPack(assetBundleName);
                }
            }
        }
    }
}
