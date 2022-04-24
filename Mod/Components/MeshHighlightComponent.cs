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

using DVLightSniper.Mod.GameObjects.Library;

using JetBrains.Annotations;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Handles highlighting (rendering as a fullbright coloured wireframe) a mesh when applying or
    /// removing decorations.
    /// </summary>
    internal class MeshHighlightComponent : MonoBehaviour
    {
        private readonly IDictionary<string, Material[]> originalMaterials = new Dictionary<string, Material[]>();

        private Material highlightMaterial;

        private Material HighlightMaterial
        {
            get
            {
                if (this.highlightMaterial == null)
                {
                    Shader highlightShader = AssetLoader.Builtin.LoadBuiltin<Shader>("assets/builtin/shaders/Unlit-Wireframe.shader");
                    this.highlightMaterial = new Material(highlightShader);
                    this.HighlightMaterial.color = Color.green;
                    this.HighlightMaterial.SetFloat("_WireThickness", 0.0F);
                }

                return this.highlightMaterial;
            }
        }

        private bool highlighted, highlightApplied;
        private DateTime highlightTime;

        internal bool Highlighted
        {
            get
            {
                return this.highlighted;
            }
            set
            {
                this.highlighted = value;
                this.highlightTime = DateTime.Now;
            }
        }

        internal Color HighlightColour
        {
            get
            {
                return this.HighlightMaterial.color;
            }
            set
            {
                Color oldColour = this.HighlightMaterial.color;
                this.HighlightMaterial.color = value;
                if (oldColour != value && this.highlightApplied)
                {
                    this.highlightApplied = false;
                    this.ApplyMaterials();
                    this.highlightApplied = true;
                    this.ApplyMaterials();
                }
            }
        }

        [UsedImplicitly]
        private void Start()
        {
            this.ObtainMaterials(this.gameObject.GetComponents<Renderer>());

            foreach (LODGroup lodGroup in this.gameObject.GetComponents<LODGroup>())
            {
                foreach (LOD lod in lodGroup.GetLODs())
                {
                    this.ObtainMaterials(lod.renderers);
                }
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            if (this.highlightApplied && (DateTime.Now - this.highlightTime).TotalMilliseconds > 1000)
            {
                this.highlighted = false;
            }

            if (this.highlighted == this.highlightApplied)
            {
                return;
            }

            this.highlightApplied = this.highlighted;
            this.ApplyMaterials();
        }

        private void ObtainMaterials(IEnumerable<Renderer> meshRenderers)
        {
            foreach (Renderer meshRenderer in meshRenderers)
            {
                string key = meshRenderer.name + "#" + meshRenderer.GetInstanceID();
                this.originalMaterials[key] = meshRenderer.materials;
            }
        }

        private void ApplyMaterials()
        {
            this.ApplyMaterials(this.gameObject.GetComponents<Renderer>());
            
            foreach (LODGroup lodGroup in this.gameObject.GetComponents<LODGroup>())
            {
                foreach (LOD lod in lodGroup.GetLODs())
                {
                    this.ApplyMaterials(lod.renderers);
                }
            }
        }

        private void ApplyMaterials(IEnumerable<Renderer> meshRenderers)
        {
            foreach (Renderer meshRenderer in meshRenderers)
            {
                string key = meshRenderer.name + "#" + meshRenderer.GetInstanceID();
                if (!this.originalMaterials.ContainsKey(key))
                {
                    continue;
                }
                Material[] original = this.originalMaterials[key];
                Material[] materials = meshRenderer.materials;
                for (int index = 0; index < original.Length; index++)
                {
                    materials[index] = this.highlightApplied ? this.HighlightMaterial : original[index];
                }
                meshRenderer.materials = materials;
            }
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            Object.Destroy(this.highlightMaterial);
        }
    }
}
