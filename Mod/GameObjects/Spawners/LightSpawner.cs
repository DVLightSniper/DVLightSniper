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
    [DataContract]
    internal class LightSpawner : Spawner, ISpriteOwner
    {
        private string name;

        internal override string Name
        {
            get
            {
                return this.name ?? (this.name = this.Group.Prefix + this.GlobalPrefix + "Light" + this.Group.NextLightId);
            }
        }

        [DataMember(Name = "properties", Order = 4)]
        private LightProperties properties;

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

        private bool on = true;

        internal bool On
        {
            get
            {
                return this.on || this.Designing;
            }
        }

        public bool ShowSprites { get { return this.Selected || this.Region.Controller.ShowSprites; } }

        private int spriteIndex;

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

        public bool Designing { get; set; }

        private DutyCycle dutyCycle = DutyCycle.ALWAYS;

        private MeshSpawner parentMesh;

        [JsonConstructor]
        internal LightSpawner(string parentPath, Vector3 localPosition, Quaternion rotation, LightProperties properties)
            : base(parentPath, localPosition, rotation)
        {
            this.properties = properties;
            this.DisableDistanceCulling = this.properties.NeverCull;
        }

        protected override void Tick(UpdateTicket ticket, bool visible)
        {
            ticket.TickedLight(visible);
            if (this.dutyCycle != null)
            {
                this.dutyCycle.Update();
                this.on = this.dutyCycle.On;
            }
        }

        protected override void Spawn(UpdateTicket ticket, GameObject parent)
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
        }

        internal bool IsParentDeleted(MeshSpawner mesh)
        {
            return this.ParentPath.Contains(mesh.Name) && mesh.Deleted;
        }

        internal void Paint(LightProperties properties)
        {
            this.Properties = properties;
            if (this.Active)
            {
                this.Apply();
            }
        }

        internal override void Configure(GameObject lightHolder)
        {
            if (lightHolder == null)
            {
                return;
            }

            this.SetGameObject(lightHolder);

            // Vector2 worldCoord = lightHolder.transform.AsWorldCoordinate();
            // LightSniper.Logger.Debug("Configuring {0} at {1:F1}, {2:F1}", this, worldCoord.x, worldCoord.y);

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

                MeshComponent mountedOnMesh = lightHolder.GetComponentInParent<MeshComponent>();
                if (mountedOnMesh?.Spawner != null)
                {
                    this.parentMesh = mountedOnMesh.Spawner;
                    this.parentMesh.Accept(this);
                }

                lightHolder.layer = LayerMask.NameToLayer(Layers.Laser_Pointer_Target);

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

        public override string ToString()
        {
            return $"Light(Name={this.Name} Properties={this.Properties})";
        }
    }

}
