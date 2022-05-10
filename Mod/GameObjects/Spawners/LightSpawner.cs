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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;

using DV;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.Util;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

using ISpriteOwner = DVLightSniper.Mod.Components.SpriteComponent.ISpriteOwner;
using UpdateTicket = DVLightSniper.Mod.GameObjects.SpawnerController.UpdateTicket;

using Newtonsoft.Json;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Spawners
{
    /// <summary>
    /// A Spawner which spawns a single light source. The main duty of this class after the light is
    /// spawned is managing the dutycycle of the light. This spawner also identifies if the light is
    /// parented to a mesh spawner and adds itself to the mesh spawner to allow behaviours such as
    /// source control and delete-mesh-deletes-lights when editing. Applying the duty cycle to the
    /// light and displaying the billboard sprite when editing are managed by attaching a
    /// LightComponent to the spawned object.
    /// </summary>
    [DataContract]
    internal class LightSpawner : Spawner, ISpriteOwner
    {
        /// <summary>
        /// Light name, computed onces
        /// </summary>
        private string name;

        /// <summary>
        /// The name of this light
        /// </summary>
        internal override string Name
        {
            get
            {
                return this.name ?? (this.name = this.Group.Prefix + this.GlobalPrefix + "Light" + this.Group.NextLightId);
            }
        }

        /// <summary>
        /// Stored properties for the light
        /// </summary>
        [DataMember(Name = "properties", Order = 4)]
        private LightProperties properties;

        /// <summary>
        /// Light properties to apply when the light is spawned
        /// </summary>
        internal LightProperties Properties
        {
            get
            {
                return this.properties;
            }

            set
            {
                this.properties = value;
                this.Apply();
                this.Group?.Save();
            }
        }

        /// <summary>
        /// Stored "on" state, computed from duty cycle
        /// </summary>
        private bool on = true;

        internal bool On
        {
            get
            {
                return (!this.Inhibit && this.on) || this.Designing;
            }
        }

        /// <summary>
        /// An override "off" switch so that certain behaviours can override the duty cycle
        /// </summary>
        internal bool Inhibit { get; set; }

        public bool ShowSprites { get { return this.Selected || this.Region.Controller.ShowSprites; } }

        private int spriteIndex;

        /// <summary>
        /// Used when editing lights to determine the sprite to display on the light
        /// </summary>
        public int SpriteIndex
        {
            get
            {
                return this.spriteIndex + (this.on ? 0 : 8);
            }
            set
            {
                this.spriteIndex = value;
            }
        }

        /// <summary>
        /// True if this light is being designed. Changes the light to always be on.
        /// </summary>
        public bool Designing { get; set; }

        /// <summary>
        /// Computed duty cycle
        /// </summary>
        private DutyCycle dutyCycle = DutyCycle.ALWAYS;

        /// <summary>
        /// If the light is parented to a mesh, stored reference to the parent
        /// </summary>
        private MeshSpawner parentMesh;

        [JsonConstructor]
        internal LightSpawner(string parentPath, Vector3 localPosition, Quaternion rotation, LightProperties properties)
            : base(parentPath, localPosition, rotation)
        {
            this.properties = properties;
            this.DisableDistanceCulling = this.properties.NeverCull;
        }

        /// <summary>
        /// Tick ths light when active, updates the duty cycle
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="visible"></param>
        protected override void Tick(UpdateTicket ticket, bool visible)
        {
            ticket.TickedLight(visible);
            if (this.dutyCycle != null)
            {
                this.dutyCycle.Update();
                this.on = this.dutyCycle.On;
            }
        }

        /// <summary>
        /// The spawner successfully located the parent transform, spawn the light
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="parent"></param>
        protected override bool Spawn(UpdateTicket ticket, GameObject parent)
        {
            GameObject lightHolder = this.GameObject;
            if (lightHolder == null)
            {
                lightHolder = new GameObject(this.Name);
                lightHolder.transform.parent = parent.transform;
                if (this.IsGlobal)
                {
                    lightHolder.transform.position = this.LocalPosition.AsOffsetCoordinate();
                }
                else
                {
                    lightHolder.transform.localPosition = this.LocalPosition;
                }
            }

            this.Configure(lightHolder);
            this.Region.Controller.NotifySpawned(this);
            ticket.Mark();
            return true;
        }

        /// <summary>
        /// Used when any mesh is deleted to determine whether it was the parent 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        internal bool IsParentDeleted(MeshSpawner mesh)
        {
            return (this.parentMesh != null && this.parentMesh.Deleted) || (this.ParentPath.Contains(mesh.Name) && mesh.Deleted);
        }

        /// <summary>
        /// Apply the specified light properties to this light
        /// </summary>
        /// <param name="properties"></param>
        internal void Paint(LightProperties properties)
        {
            this.Properties = properties;
            if (this.Active)
            {
                this.Apply();
            }
        }

        /// <summary>
        /// Apply configuration from this spawner onto the spawned GameObject
        /// </summary>
        /// <param name="lightHolder"></param>
        internal override void Configure(GameObject lightHolder)
        {
            if (lightHolder == null)
            {
                return;
            }

            this.SetGameObject(lightHolder);

            DutyCycle configuredDutyCycle = this.Properties.DutyCycle;
            if (!configuredDutyCycle.Equals(this.dutyCycle))
            {
                this.dutyCycle = configuredDutyCycle;
            }

            Light light = lightHolder.GetComponent<Light>();
            if (light == null)
            {
                light = lightHolder.AddComponent<Light>();

                LightComponent lightBehaviour = lightHolder.AddComponent<LightComponent>();
                lightBehaviour.Spawner = this;
                lightBehaviour.OnDestroyed += this.OnLightDestroyed;

                this.Properties.Configure(light);
                this.ConfigureCorona(lightHolder);

                // GetComponentInParent would be neater but that fails if the object is currently
                // not active (which may be the case if it's outside the culling radius)
                MeshComponent[] mountedOnMesh = lightHolder.GetComponentsInParent<MeshComponent>(true);
                if (mountedOnMesh.Length > 0 && mountedOnMesh[0].Spawner != null)
                {
                    this.parentMesh = mountedOnMesh[0].Spawner;
                    this.parentMesh.Accept(this);
                }

                // We only want the collider to work with the laser pointer
                lightHolder.layer = LayerMask.NameToLayer(Layers.Laser_Pointer_Target);

                // Add collider so we can target the light directly with the comms radio if it's
                // floating in space
                BoxCollider editCollider = lightHolder.AddComponent<BoxCollider>();
                editCollider.size = new Vector3(0.25F, 0.25F, 0.25F);
                editCollider.center = Vector3.zero;
            }
            else
            {
                this.Properties.Configure(light);
                this.ConfigureCorona(lightHolder);
            }
        }

        private void ConfigureCorona(GameObject lightHolder)
        {
            GameObject coronaHolder = GameObject.Find(lightHolder.name + "_Corona");
            if (string.IsNullOrEmpty(this.Properties.Corona?.Sprite))
            {
                if (coronaHolder != null)
                {
                    Object.Destroy(coronaHolder);
                }
                return;
            }

            if (coronaHolder == null)
            {
                coronaHolder = new GameObject(lightHolder.name + "_Corona");
                coronaHolder.layer = LayerMask.NameToLayer(Layers.TransparentFX);
                coronaHolder.transform.parent = lightHolder.transform;
                coronaHolder.transform.localPosition = Vector3.zero;

                this.properties.Corona.Configure(coronaHolder.AddComponent<CoronaComponent>()).Spawner = this;
            }
            else
            {
                this.properties.Corona.Configure(coronaHolder.GetComponent<CoronaComponent>());
            }
        }

        private void OnLightDestroyed(SpriteComponent sprite)
        {
            if (this.Active)
            {
                this.Region.Controller.NotifyDestroyed(this);
            }
            this.SetGameObject(null);
        }

        public override void OnSaving()
        {
            this.Properties?.OnSaving();
        }

        public override string ToString()
        {
            return $"Light(Name={this.Name} Properties={this.Properties})";
        }
    }

}
