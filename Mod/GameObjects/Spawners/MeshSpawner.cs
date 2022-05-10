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
using System.Reflection;
using System.Runtime.Serialization;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.Components.Prefabs;

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
    /// <summary>
    /// A spawner which spawns a mesh in the world, well strictly speaking any prefab which can be
    /// stored in an asset bundle. The spawned prefab can be positioned and rotated but not scaled.
    /// Lights defined in the template are created as separate spawners parented to the spawned mesh
    /// rather than being managed by this spawner, this is so they can be independently designed as
    /// required by the user.
    /// 
    /// Light spawners parented to this mesh spawner register their existence with us so that the
    /// duty cycle can be propagated to the mesh. Any child objects named "Source" in the prefab
    /// will be enabled/disabled based on the state of the master light, which is the first light to
    /// be registered. Propagation of the duty cycle is managed by attaching MeshComponent to the
    /// spawned object.
    /// 
    /// We also attach any components decorated with matching PrefabBehaviourAttribute to the
    /// spawned object because we can't store code in the assetbundle and putting the code in a
    /// separate assembly is a bit of a faff.
    /// </summary>
    [DataContract]
    internal class MeshSpawner : Spawner
    {
        /// <summary>
        /// Number of times to attempt to load the asset before giving up
        /// </summary>
        private const int MAX_LOAD_ATTEMPTS = 4;

        /// <summary>
        /// Number of seconds to wait between load attempts when loading fails
        /// </summary>
        private const int RELOAD_ATTEMPT_TIME_SECONDS = 60;

        private string name;

        /// <summary>
        /// The name of this mesh
        /// </summary>
        internal override string Name
        {
            get
            {
                return this.name ?? (this.name = this.Group.Prefix + this.GlobalPrefix + "Mesh" + this.LocalPosition.AsHex());
            }
        }

        /// <summary>
        /// Stored properties for the mesh
        /// </summary>
        [DataMember(Name = "properties", Order = 4)]
        private MeshProperties properties;

        /// <summary>
        /// Mesh properties
        /// </summary>
        internal MeshProperties Properties
        {
            get
            {
                return this.properties;
            }

            set
            {
                if (this.properties != null)
                {
                    this.properties.Changed -= this.OnPropertiesChanged;
                }

                this.properties = value;
                this.Apply();
                this.Group?.Save();

                if (this.properties != null)
                {
                    this.properties.Changed += this.OnPropertiesChanged;
                }
            }
        }

        /// <summary>
        /// All light spawners which have us defined as a parent
        /// </summary>
        private readonly IList<LightSpawner> childLights = new List<LightSpawner>();

        /// <summary>
        /// The first light spawner which registered as a child
        /// </summary>
        private LightSpawner masterLight;

        /// <summary>
        /// Duty cycle state propagated from the master light
        /// </summary>
        internal bool On { get { return this.masterLight != null && this.masterLight.On; } }

        private bool inhibit;

        /// <summary>
        /// Force the mesh state to "off"
        /// </summary>
        internal bool Inhibit
        {
            get
            {
                return this.inhibit;
            }
            set
            {
                foreach (LightSpawner childLight in this.childLights)
                {
                    childLight.Inhibit = value;
                }

                this.inhibit = value;
            }
        }

        /// <summary>
        /// Keep track of failed asset loads so we can retry a few times and give up if necessary
        /// </summary>
        private DateTime lastLoadAttempt;
        private int failedLoadAttempts;

        /// <summary>
        /// Flag which indicates we tried to load the asset but were unsuccessful
        /// </summary>
        internal bool MissingResource { get; private set; }

        [JsonConstructor]
        internal MeshSpawner(string parentPath, Vector3 localPosition, Quaternion rotation, MeshProperties properties)
            : base(parentPath, localPosition, rotation)
        {
            this.properties = properties;
            this.properties.Changed += this.OnPropertiesChanged;
        }

        private void OnPropertiesChanged(string key)
        {
            this.Group?.Save();
        }

        protected override void Tick(UpdateTicket ticket, bool visible)
        {
            ticket.TickedMesh(visible);
        }

        /// <summary>
        /// The spawner successfully located the parent transform, attempt to spawn the mesh
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="parent"></param>
        protected override bool Spawn(UpdateTicket ticket, GameObject parent)
        {
            if (this.MissingResource && (this.failedLoadAttempts > MeshSpawner.MAX_LOAD_ATTEMPTS || (DateTime.UtcNow - this.lastLoadAttempt).TotalSeconds < MeshSpawner.RELOAD_ATTEMPT_TIME_SECONDS))
            {
                return false;
            }

            GameObject meshHolder = this.GameObject;
            if (meshHolder == null)
            {
                this.lastLoadAttempt = DateTime.UtcNow;

                GameObject meshTemplate = AssetLoader.Meshes.Load<GameObject>(this.Properties.AssetBundleName, this.Properties.AssetName);
                if (meshTemplate == null)
                {
                    this.MissingResource = true;
                    this.failedLoadAttempts++;
                    return false;
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
            return true;
        }

        /// <summary>
        /// Apply the specified light properties to all child lights of this mesh, this happens when
        /// a user clicks a mesh with the paint tool
        /// </summary>
        /// <param name="lightProperties"></param>
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

        /// <summary>
        /// Apply configured properties to the mesh
        /// </summary>
        /// <param name="meshHolder"></param>
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

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                PrefabComponentAttribute prefabComponent = type.GetCustomAttribute<PrefabComponentAttribute>();
                if (prefabComponent != null && prefabComponent.Matches(this.Properties))
                {
                    meshHolder.AddComponent(type);
                }
            }
        }

        private void OnMeshDestroyed(MeshComponent sprite)
        {
            if (this.Active)
            {
                this.Region.Controller.NotifyDestroyed(this);
            }
            this.SetGameObject(null);
        }

        /// <summary>
        /// Callback from a light spawner to register itself as a child
        /// </summary>
        /// <param name="light"></param>
        internal void Accept(LightSpawner light)
        {
            if (this.childLights.Contains(light))
            {
                return;
            }

            this.childLights.Add(light);
            light.OnDeleted += this.Forget;
            light.Inhibit = this.inhibit;
            if (this.masterLight == null || this.masterLight.Deleted)
            {
                this.masterLight = light;
            }
        }

        /// <summary>
        /// Callback from a light spawner to unregister itself as a child
        /// </summary>
        /// <param name="spawner"></param>
        private void Forget(Spawner spawner)
        {
            if (this.Deleted)
            {
                return;
            }

            if (spawner is LightSpawner lightSpawner)
            {
                this.childLights.Remove(lightSpawner);
                lightSpawner.Inhibit = false;

                if (this.masterLight == spawner)
                {
                    this.masterLight = this.childLights.Count > 0 ? this.childLights[0] : null;
                }
            }
        }

        public override void OnSaving()
        {
            this.Properties?.OnSaving(true);
        }

        public override string ToString()
        {
            return $"Mesh(Name={this.Name} Properties={this.Properties})";
        }
    }
}
