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

using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using Extensions.SystemRandom;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.Rendering;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Component added to decorated GameObjects to manage applying and removing additional
    /// materials based on definitions inside the decoration.
    /// </summary>
    internal class MaterialAssignmentComponent : MonoBehaviour
    {
        /// <summary>
        /// Manager object which tracks materials added to a specific renderer and updates the
        /// renderer's materials property with extra materials as required. The materials array is
        /// grown and shrunk as appropriate in order to accomodate additional materials.
        /// </summary>
        internal class MaterialManager
        {
            /// <summary>
            /// All existing managers
            /// </summary>
            internal static readonly List<MaterialManager> managers = new List<MaterialManager>();

            internal readonly MeshRenderer renderer;

            private readonly Dictionary<string, Material> extraMaterials = new Dictionary<string, Material>();

            private readonly int initialSize;

            private bool dirty = false;

            public MaterialManager(MeshRenderer renderer)
            {
                this.renderer = renderer;
                this.initialSize = renderer.materials.Length;
            }

            private void Destroy()
            {
                foreach (Material material in this.extraMaterials.Values)
                {
                    Object.Destroy(material);
                }
            }

            internal void Add(string key, Material sourceMaterial)
            {
                if (!this.extraMaterials.ContainsKey(key))
                {
                    sourceMaterial.renderQueue = (int)RenderQueue.GeometryLast;
                    this.extraMaterials[key] = sourceMaterial;
                    this.dirty = true;
                }
            }

            internal void Remove(string key)
            {
                if (this.extraMaterials.Remove(key))
                {
                    this.dirty = true;
                }
            }

            internal void Apply()
            {
                if (!this.dirty)
                {
                    return;
                }

                Material[] materials = new Material[this.initialSize + this.extraMaterials.Count];
                Array.Copy(this.renderer.materials, materials, this.initialSize);
                int index = this.initialSize;
                foreach (Material material in this.extraMaterials.Values)
                {
                    materials[index++] = material;
                }
                this.renderer.materials = materials;
                this.dirty = false;
            }

            private static MaterialManager GetManager(MeshRenderer renderer)
            {
                return MaterialManager.managers.FirstOrDefault(manager => renderer == manager.renderer);
            }

            internal static MaterialManager Of(MeshRenderer renderer)
            {
                MaterialManager manager = MaterialManager.GetManager(renderer);
                if (manager == null)
                {
                    manager = new MaterialManager(renderer);
                    MaterialManager.managers.Add(manager);
                }
                return manager;
            }

            internal static void Destroy(MeshRenderer renderer)
            {
                MaterialManager.GetManager(renderer)?.Destroy();
            }
        }

        /// <summary>
        /// Wrapper which manages applying and removing an assignment bundle from child renderers
        /// </summary>
        internal class DecorationAssignments
        {
            private static readonly Random RANDOM_SOURCE = new Random();

            /// <summary>
            /// Spawner which owns these decorations
            /// </summary>
            internal DecorationSpawner Spawner { get; }

            /// <summary>
            /// Source materials array lifted from the source prefab
            /// </summary>
            internal Material[] SourceMaterials { get; }

            /// <summary>
            /// Assignments of materials to target renderers
            /// </summary>
            internal MaterialAssignments Assignments { get; }

            /// <summary>
            /// Computed random alpha values
            /// </summary>
            private float[] randomAlpha;

            /// <summary>
            /// True if the materials are currently applied, tracked so we only do the relatively
            /// expensive apply/remove operations when the enabled state actually changes
            /// </summary>
            private bool enabled = false;

            private float preCullFade = 1.0F;

            internal DecorationAssignments(DecorationSpawner spawner, GameObject source, MaterialAssignments assignments)
            {
                this.Spawner = spawner;
                Material[] materials = source.GetComponent<MeshRenderer>().materials;
                this.SourceMaterials = new Material[materials.Length];
                this.randomAlpha = new float[materials.Length];
                for (var index = 0; index < materials.Length; index++)
                {
                    Material material = Object.Instantiate(materials[index]);
                    float alphaBlend = material.GetFloat("_AlphaBlend");
                    if (assignments.RandomAlpha > 0.0F)
                    {
                        this.randomAlpha[index] = (alphaBlend * DecorationAssignments.RANDOM_SOURCE.NextFloat(assignments.MinRandomAlpha, assignments.MaxRandomAlpha));
                        material.SetFloat("_AlphaBlend", this.randomAlpha[index]);
                    }
                    else
                    {
                        this.randomAlpha[index] = alphaBlend;
                    }
                    this.SourceMaterials[index] = material;
                }
                this.Assignments = assignments;
            }

            internal void Update(GameObject gameObject)
            {
                this.Apply(gameObject, this.Spawner.On && !this.Spawner.Culled);
            }

            internal DecorationAssignments Apply(GameObject gameObject, bool enabled = true)
            {
                if (enabled == this.enabled)
                {
                    if (this.Spawner.PreCullFade != this.preCullFade)
                    {
                        this.preCullFade = this.Spawner.PreCullFade;
                        for (var index = 0; index < this.SourceMaterials.Length; index++)
                        {
                            this.SourceMaterials[index].SetFloat("_AlphaBlend", this.randomAlpha[index] * this.preCullFade);
                        }
                    }

                    return this;
                }

                this.enabled = enabled;
                foreach (MeshRenderer renderer in gameObject.GetComponentsInChildren<MeshRenderer>())
                {
                    MaterialManager materialManager = MaterialManager.Of(renderer);
                    for (var index = 0; index < this.Assignments.Count; index++)
                    {
                        MaterialAssignment assignment = this.Assignments[index];
                        if (assignment.RendererName == renderer.name)
                        {
                            string key = $"{this.Spawner.Name}[{index}]";
                            if (enabled)
                            {
                                materialManager.Add(key, this.SourceMaterials[assignment.FromIndex]);
                            }
                            else
                            {
                                materialManager.Remove(key);
                            }
                        }
                    }

                    materialManager.Apply();
                }

                return this;
            }
        }

        /// <summary>
        /// Decoration assignments from different decoration sources applied to this gameobject
        /// </summary>
        internal List<DecorationAssignments> assignments = new List<DecorationAssignments>();

        /// <summary>
        /// Add a new set of material assignments from the specified mesh spawner
        /// </summary>
        /// <param name="spawner"></param>
        /// <param name="source"></param>
        /// <param name="assignments"></param>
        internal void Add(DecorationSpawner spawner, GameObject source, MaterialAssignments assignments)
        {
            this.assignments.Add(new DecorationAssignments(spawner, source, assignments));
            if (assignments.HideSourcePrefab)
            {
                source.GetComponent<DecorationComponent>().Hidden = true;
            }
        }

        /// <summary>
        /// Remove the assignments from the specified spawner (if any)
        /// </summary>
        /// <param name="spawner"></param>
        internal void Remove(DecorationSpawner spawner)
        {
            DecorationAssignments assignment = this.assignments.FirstOrDefault(a => a.Spawner == spawner);
            if (assignment != null)
            {
                this.assignments.Remove(assignment.Apply(this.gameObject, false));
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            foreach (DecorationAssignments assignment in this.assignments)
            {
                assignment.Update(this.gameObject);
            }
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            foreach (MeshRenderer renderer in this.gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                MaterialManager.Destroy(renderer);
            }

            foreach (DecorationAssignments assignment in this.assignments)
            {
                assignment.Spawner.Destroy();
            }
        }
    }
}
