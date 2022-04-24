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

using DVLightSniper.Mod.Components;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

using DVLightSniper.Mod.GameObjects.Library;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.Util;

using UpdateTicket = DVLightSniper.Mod.GameObjects.SpawnerController.UpdateTicket;

using Newtonsoft.Json;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Spawners
{
    [DataContract]
    internal class MeshSpawner : Spawner
    {
        private string name;

        internal override string Name
        {
            get
            {
                return this.name ?? (this.name = this.Group.Prefix + this.GlobalPrefix + "Mesh" + this.LocalPosition.AsHex());
            }
        }

        [DataMember(Name = "properties", Order = 4)]
        private MeshProperties properties;

        internal MeshProperties Properties
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

        private readonly IList<LightSpawner> childLights = new List<LightSpawner>();

        private LightSpawner masterLight;

        internal bool On { get { return this.masterLight == null || this.masterLight.On; } }

        private DateTime lastLoadAttempt;
        private int failedLoadAttempts;

        internal bool MissingResource { get; private set; }

        [JsonConstructor]
        internal MeshSpawner(string parentPath, Vector3 localPosition, Quaternion rotation, MeshProperties properties)
            : base(parentPath, localPosition, rotation)
        {
            this.properties = properties;
        }

        protected override void Tick(UpdateTicket ticket, bool visible)
        {
            ticket.TickedMesh(visible);
        }

        protected override void Spawn(UpdateTicket ticket, GameObject parent)
        {
            if (this.MissingResource)
            {
                if (this.failedLoadAttempts > 4 || (DateTime.Now - this.lastLoadAttempt).TotalSeconds < 60)
                {
                    return;
                }
            }

            GameObject meshHolder = this.GameObject;
            if (meshHolder == null)
            {
                this.lastLoadAttempt = DateTime.Now;

                GameObject meshTemplate = AssetLoader.Meshes.Load<GameObject>(this.Properties.AssetBundleName, this.Properties.AssetName);
                if (meshTemplate == null)
                {
                    this.MissingResource = true;
                    this.failedLoadAttempts++;
                    return;
                }

                this.MissingResource = false;
                this.failedLoadAttempts = 0;

                meshHolder = Object.Instantiate(meshTemplate);
                meshHolder.name = this.Name;
                meshHolder.transform.parent = parent.transform;
                if (this.IsGlobal)
                {
                    meshHolder.transform.position = this.LocalPosition.AsOffsetCoordinate();
                }
                else
                {
                    meshHolder.transform.localPosition = this.LocalPosition;
                }
                meshHolder.transform.rotation = this.Rotation;

                ticket.Mark(2);
            }

            this.Configure(meshHolder);
            this.Region.Controller.NotifySpawned(this);
            ticket.Mark(1);
        }

        internal void Paint(LightProperties lightProperties)
        {
            foreach (LightSpawner childLight in this.childLights)
            {
                childLight.Paint(lightProperties.Clone());
            }
        }

        internal override void Undelete()
        {
            base.Undelete();

            foreach (LightSpawner childLight in this.childLights)
            {
                childLight.Undelete();
            }
        }

        internal override void Configure(GameObject meshHolder)
        {
            if (meshHolder == null)
            {
                return;
            }

            this.SetGameObject(meshHolder);

            MeshComponent meshNotifier = meshHolder.GetComponent<MeshComponent>();
            if (meshNotifier == null)
            {
                meshNotifier = meshHolder.AddComponent<MeshComponent>();
                meshNotifier.OnDestroyed += this.OnMeshDestroyed;
            }
            meshNotifier.Spawner = this;
        }

        private void OnMeshDestroyed(MeshComponent sprite)
        {
            if (this.Active)
            {
                this.Region.Controller.NotifyDestroyed(this);
            }
            this.SetGameObject(null);
        }

        internal void Accept(LightSpawner light)
        {
            if (this.childLights.Contains(light))
            {
                return;
            }

            this.childLights.Add(light);
            light.OnDeleted += this.Forget;
            if (this.masterLight == null || this.masterLight.Deleted)
            {
                this.masterLight = light;
            }
        }

        private void Forget(Spawner spawner)
        {
            if (this.Deleted)
            {
                return;
            }

            this.childLights.Remove(spawner as LightSpawner);
            if (this.masterLight == spawner)
            {
                this.masterLight = this.childLights.Count > 0 ? this.childLights[0] : null;
            }
        }

        public override string ToString()
        {
            return $"Mesh(Name={this.Name} Properties={this.Properties})";
        }
    }
}
