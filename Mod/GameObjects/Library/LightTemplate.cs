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
using DVLightSniper.Mod.GameObjects.Library.Assets;
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
        /// <summary>
        /// Lights defined in this template, can be empty if the template only defines meshes
        /// </summary>
        [DataMember(Name = "lights", Order = 2)]
        internal List<LightProperties> Lights { get; private set; } = new List<LightProperties>();

        /// <summary>
        /// Meshes defined in this template, can be empty if the template only defines lights
        /// </summary>
        [DataMember(Name = "meshes", Order = 3)]
        internal Dictionary<MeshOrientation, MeshTemplate> Meshes { get; private set; } = new Dictionary<MeshOrientation, MeshTemplate>();

        /// <summary>
        /// When sniping lights, this offset is applied to the sniped surface normal in order to
        /// place the light. Normally we want the light to be "against" the surface so we offset it
        /// by 0.1 so that it doesn't intersect with the sniped surface. Larger numbers can be used
        /// to snipe lights further from the surface, or negative numbers can be used to snipe
        /// "behind" the hit surface.
        /// </summary>
        [DataMember(Name = "snipeOffset", Order = 4)]
        internal float SnipeOffset { get; private set; } = 0.1F;

        private MeshTemplate meshBuilder;

        /// <summary>
        /// Supports fluent interface for defining templates in code, the most recent MeshTemplate
        /// defined is stored here so that MeshTemplate calls can be chained through fluently. Just
        /// a property so that we have a convenient mechanism for throwing an exception if calls are
        /// made out of order.
        /// </summary>
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

        /// <summary>
        /// Supports fluent interface for defining templates in code, the most recent
        /// LightProperties defined is stored here so that LightProperties calls can be chained
        /// through fluently. Just a property so that we have a convenient mechanism for throwing an
        /// exception if calls are made out of order.
        /// </summary>
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

        /// <summary>
        /// Gets the mesh on this template for the specified orientation, falls back to floor
        /// finally if wall or ceiling meshes not defined
        /// </summary>
        /// <param name="orientation"></param>
        /// <returns></returns>
        internal MeshTemplate GetMesh(MeshOrientation orientation)
        {
            if (this.Meshes.ContainsKey(orientation))
            {
                return this.Meshes[orientation];
            }

            return orientation > 0 ? this.GetMesh(--orientation) : null;
        }

        /// <summary>
        /// Fluent. Set the snipe offset for this template.
        /// </summary>
        /// <param name="snipeOffset"></param>
        /// <returns></returns>
        internal LightTemplate WithSnipeOffset(float snipeOffset)
        {
            this.SnipeOffset = snipeOffset;
            return this;
        }

        /// <summary>
        /// Fluent. Add a light to this template
        /// </summary>
        /// <param name="intensity"></param>
        /// <param name="range"></param>
        /// <param name="colour"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal LightTemplate WithLight(float intensity, float range, Color colour, LightType type = LightType.Point)
        {
            return this.WithLight(new LightProperties(intensity, range, colour, type));
        }

        /// <summary>
        /// Fluent, Add a light to this template.
        /// </summary>
        /// <param name="light"></param>
        /// <returns></returns>
        internal LightTemplate WithLight(LightProperties light)
        {
            this.Lights.Add(light);
            this.lightBuilder = light;
            return this;
        }

        /// <summary>
        /// Fluent. Add multiple lights to this template
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        internal LightTemplate WithLights(LightTemplate template)
        {
            this.Lights.AddRange(template.Lights);
            return this;
        }

        /// <summary>
        /// Fluent. Set the corona properties for the current light
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="scale"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        internal LightTemplate WithCorona(string sprite, float scale = 1.0F, float range = 100.0F)
        {
            this.LightBuilder.Corona = new CoronaProperties(sprite, scale, range);
            return this;
        }

        /// <summary>
        /// Fluent. Set the duty cycle for the current light
        /// </summary>
        /// <param name="dutyCycle"></param>
        /// <returns></returns>
        internal LightTemplate WithDutyCycle(DutyCycle dutyCycle)
        {
            this.LightBuilder.DutyCycle = dutyCycle;
            return this;
        }

        /// <summary>
        /// Fluent. Set the "never cull" flag for the current light
        /// </summary>
        /// <returns></returns>
        internal LightTemplate NeverCull()
        {
            this.LightBuilder.NeverCull = true;
            return this;
        }

        /// <summary>
        /// Fluent. Add the specified mesh to the template
        /// </summary>
        /// <param name="orientation"></param>
        /// <param name="assetBundleName"></param>
        /// <param name="assetName"></param>
        /// <returns></returns>
        internal LightTemplate WithMesh(MeshOrientation orientation, string assetBundleName, string assetName)
        {
            return this.WithMesh(orientation, new MeshProperties(assetBundleName, assetName));
        }

        /// <summary>
        /// Fluent. Add the specified mesh to the template
        /// </summary>
        /// <param name="orientation"></param>
        /// <param name="mesh"></param>
        /// <returns></returns>
        internal LightTemplate WithMesh(MeshOrientation orientation, MeshProperties mesh)
        {
            this.meshBuilder = this.Meshes[orientation] = new MeshTemplate(mesh);
            return this;
        }

        /// <summary>
        /// Fluent. Add the specified meshes to this template
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        internal LightTemplate WithMeshes(LightTemplate template)
        {
            foreach (KeyValuePair<MeshOrientation, MeshTemplate> templateMesh in template.Meshes)
            {
                this.Meshes[templateMesh.Key] = templateMesh.Value;
            }
            return this;
        }

        /// <summary>
        /// Fluent, set the mesh alignment for the current mesh
        /// </summary>
        /// <param name="alignmentType"></param>
        /// <returns></returns>
        internal LightTemplate WithAlignment(MeshTemplate.AlignmentType alignmentType)
        {
            this.MeshBuilder.Alignment = alignmentType;
            return this;
        }

        /// <summary>
        /// Extracts asset bundles and other assets used by this template if they are stored in
        /// packs and haven't already been extracted. This is used when sniping a template stored in
        /// a pack so that  any user-sniped lights continue to work if the pack is later removed.
        /// </summary>
        internal void ExtractAssets()
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

            foreach (LightProperties lightProperties in this.Lights)
            {
                if (lightProperties.Corona != null)
                {
                    AssetLoader.Coronas.ExtractTexture(lightProperties.Corona.Sprite);
                }
            }
        }
    }
}
