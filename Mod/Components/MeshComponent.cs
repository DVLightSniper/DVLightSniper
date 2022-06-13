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

using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Library.Assets;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using JetBrains.Annotations;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Component added to spawned meshes to link behaviours from the spawner such as showing &
    /// hiding the "source" parts based on the "on" state of attached lights, and rendering the mesh
    /// as a wireframe when hovering over it during delete, paint, and other selection operations.
    /// </summary>
    internal class MeshComponent : SpawnerComponent
    {
        internal MeshSpawner Spawner { get; set; }

        internal MeshProperties Properties { get => this.Spawner?.Properties; }

        internal event Action<MeshComponent> OnDestroyed;

        private readonly IDictionary<string, Material> materials = new Dictionary<string, Material>();

        private readonly List<MeshRenderer> sources = new List<MeshRenderer>();

        private bool selected, sourceOn = true;

        private Material selectedMaterial;

        private Material SelectedMaterial
        {
            get
            {
                if (this.selectedMaterial == null)
                {
                    Shader selectedShader = AssetLoader.Builtin.LoadBuiltin<Shader>("assets/builtin/shaders/wireframe.shader");
                    this.selectedMaterial = new Material(selectedShader);
                }

                return this.selectedMaterial;
            }
        }

        [UsedImplicitly]
        private void Start()
        {
            foreach (MeshRenderer meshRenderer in this.gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                this.materials[meshRenderer.name] = meshRenderer.material;
                if (meshRenderer.name.StartsWith("Source"))
                {
                    this.sources.Add(meshRenderer);
                    meshRenderer.enabled = false;
                    this.sourceOn = false;
                }
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            if (this.Spawner == null)
            {
                return;
            }

            if (this.Spawner.Selected != this.selected)
            {
                this.selected = this.Spawner.Selected;
                this.SelectedMaterial.color = this.Spawner.SelectionColour;
                foreach (MeshRenderer meshRenderer in this.gameObject.GetComponentsInChildren<MeshRenderer>())
                {
                    meshRenderer.material = this.selected ? this.SelectedMaterial : this.materials[meshRenderer.name];
                }
            }

            bool actuallyOn = this.Spawner.On && !this.Spawner.Inhibit;
            if (actuallyOn != this.sourceOn || (this.Spawner.Inhibit && !actuallyOn))
            {
                this.sourceOn = actuallyOn;
                foreach (MeshRenderer source in this.sources)
                {
                    source.enabled = !this.Spawner.Inhibit && (source.name.EndsWith("_Inv") ? !this.sourceOn : this.sourceOn);
                }
            }
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            Action<MeshComponent> onDestroyed = this.OnDestroyed;
            onDestroyed?.Invoke(this);
        }
    }
}