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
using DVLightSniper.Mod.Util;

using Newtonsoft.Json;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.GameObjects.Library
{
    /// <summary>
    /// Template for decorations, stores a name and ID for the decoration as well as the available
    /// targets for the decoration.
    /// </summary>
    [DataContract]
    internal class DecorationTemplate : Template
    {
        internal static readonly DebugOverlay.Section debug = DebugOverlay.AddSection("Decorate Mesh", Color.white);
        private static int debugLevel = 0;

        /// <summary>
        /// Target match for a decoration, which contains the target object, matching template and 
        /// decoration target for the match
        /// </summary>
        internal class TemplateMatch
        {
            internal GameObject gameObject;
            internal DecorationTemplate template;
            internal DecorationTarget target;

            internal TemplateMatch(GameObject gameObject, DecorationTemplate template, DecorationTarget target)
            {
                this.gameObject = gameObject;
                this.template = template;
                this.target = target;
            }
        }

        [DataMember(Name = "id", Order = 0)]
        private string id;

        /// <summary>
        /// ID consists of a GROUP and ID separated by : for example GLOW:RED, this is used so
        /// that decorations which should replace eachother can share the same group
        /// </summary>
        internal override string Id
        {
            get
            {
                return this.id;
            }
            set
            {
                this.id = value;
            }
        }

        /// <summary>
        /// Targets supported by this decoration
        /// </summary>
        [DataMember(Name = "targets", Order = 2)]
        internal List<DecorationTarget> Targets { get; set; } = new List<DecorationTarget>();

        private DecorationTarget builder;

        public DecorationTarget Builder
        {
            get
            {
                if (this.builder == null)
                {
                    throw new InvalidOperationException("Builder is null");
                }
                return this.builder;
            }
        }

        [JsonConstructor]
        internal DecorationTemplate(string id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

        internal DecorationTemplate WithTarget(string targetMesh, string assetBundleName, string assetName)
        {
            this.Targets.Add(this.builder = new DecorationTarget(targetMesh, assetBundleName, assetName));
            return this;
        }

        internal DecorationTemplate WithDutyCycle(DutyCycle dutyCycle)
        {
            this.Builder.WithDutyCycle(dutyCycle);
            return this;
        }

        internal DecorationTemplate WithMaterialAssignment(string rendererName, int from = 0)
        {
            this.Builder.WithMaterialAssignment(rendererName, from);
            return this;
        }

        internal DecorationTemplate WithRandomAlpha(float randomAlpha)
        {
            this.Builder.WithRandomAlpha(randomAlpha);
            return this;
        }

        internal DecorationTemplate HideSourcePrefab(bool value)
        {
            this.Builder.HideSourcePrefab(value);
            return this;
        }

        internal void ExtractAssetBundles()
        {
            ISet<string> assetBundleNames = new HashSet<string>();

            foreach (DecorationTarget target in this.Targets)
            {
                string assetBundleName = target.Properties.AssetBundleName;
                if (assetBundleNames.Add(assetBundleName))
                {
                    AssetLoader.Meshes.ExtractFromPack(assetBundleName);
                }
            }
        }

        internal TemplateMatch MatchRecursive(GameObject gameObject)
        {
            DecorationTemplate.debug.Clear();
            DecorationTemplate.debugLevel = 0;

            while (true)
            {
                TemplateMatch matchingTemplate = this.Match(gameObject);
                DecorationTemplate.debugLevel++;
                if (matchingTemplate != null)
                {
                    return matchingTemplate;
                }

                if (gameObject.transform.parent == null)
                {
                    return null;
                }

                gameObject = gameObject.transform.parent.gameObject;
            }
        }

        internal TemplateMatch Match(GameObject gameObject)
        {
            DecorationTarget findTarget(string name) => this.Targets.FirstOrDefault(target => name.StartsWith(target.Target));

            DecorationTemplate.debug.AddMessage(new string(' ', DecorationTemplate.debugLevel) + "@" + gameObject.transform.name, Color.yellow);
            DecorationTemplate.debugLevel++;

            DecorationTarget objectTarget = findTarget("@" + gameObject.transform.name);
            if (objectTarget != null)
            {
                return new TemplateMatch(gameObject, this, objectTarget);
            }

            if (gameObject.GetComponent<MeshRenderer>() != null)
            {
                foreach (MeshFilter meshFilter in gameObject.GetComponents<MeshFilter>())
                {
                    DecorationTemplate.debug.AddMessage(new string(' ', DecorationTemplate.debugLevel) + meshFilter.mesh.name);
                    DecorationTarget target = findTarget(meshFilter.mesh.name);
                    if (target != null)
                    {
                        return new TemplateMatch(gameObject, this, target);
                    }
                }
            }

            LODGroup lodGroup = gameObject.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                return null;
            }

            foreach (LOD lod in lodGroup?.GetLODs())
            {
                foreach (Renderer renderer in lod.renderers)
                {
                    foreach (MeshFilter meshFilter in renderer.GetComponents<MeshFilter>())
                    {
                        DecorationTemplate.debug.AddMessage(new string(' ', DecorationTemplate.debugLevel) + meshFilter.mesh.name);
                        DecorationTarget target = findTarget(meshFilter.mesh.name);
                        if (target != null)
                        {
                            return new TemplateMatch(gameObject, this, target);
                        }
                    }
                }
            }

            return null;
        }
    }
}
