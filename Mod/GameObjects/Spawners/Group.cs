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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;

using DV;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners.Packs;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.GameObjects.Spawners.Upgrade;
using DVLightSniper.Mod.Storage;
using UpdateTicket = DVLightSniper.Mod.GameObjects.SpawnerController.UpdateTicket;

using Newtonsoft.Json;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.GameObjects.Spawners
{
    /// <summary>
    /// A Group is collection of spawners backed by a json file on disk, each region can contain
    /// multiple groups and this allows a partially modular approach to managing collections of 
    /// lights and meshes. The main purpose of this is to allow users to create collections of
    /// lights and/or meshes and share them with others.
    /// </summary>
    [DataContract]
    internal class Group : JsonStorage, IEnumerable<Spawner>
    {
        /// <summary>
        /// Raised when a mesh is removed so that the region can propagate the removal to any
        /// parented lights
        /// </summary>
        internal event Action<Group, MeshSpawner> MeshRemoved;

        /// <summary>
        /// Reference to the region which contains this specific group
        /// </summary>
        internal Region Region { get; private set; }

        /// <summary>
        /// Reference to the pack this group was loaded from if it was loaded from a pack
        /// </summary>
        internal Pack Pack { get; private set; }

        /// <summary>
        /// Whether this group is editable (backed by JSON), false if the group was read from
        /// inside a zip file
        /// </summary>
        internal virtual bool Editable { get { return this.Pack == null; } }

        private int nextLightId = 1;

        /// <summary>
        /// Lights do not have ids when stored in the json file, they are dynamically assigned ids
        /// as they spawn.
        /// </summary>
        internal int NextLightId
        {
            get
            {
                return this.nextLightId++;
            }
        }

        private int nextDecorationId = 1;

        /// <summary>
        /// Decorations do not have ids when stored in the json file, they are dynamically assigned
        /// ids as they spawn.
        /// </summary>
        internal int NextDecorationId
        {
            get
            {
                return this.nextDecorationId++;
            }
        }

        /// <summary>
        /// Group base name (file name without pack prefix)
        /// </summary>
        internal string BaseName
        {
            get
            {
                return Path.GetFileNameWithoutExtension(this.FileName);
            }
        }

        /// <summary>
        /// Group name (file name with pack prefix)
        /// </summary>
        internal string Name
        {
            get
            {
                return (this.Pack != null ? this.Pack.Id + ":" : "") + this.BaseName;
            }
        }

        /// <summary>
        /// Prefix used for objects spawned from spawners in this group
        /// </summary>
        internal string Prefix
        {
            get
            {
                return this.Name + "_" + this.Region.YardID + "_";
            }
        }

        /// <summary>
        /// True if this group contains no objects and therefore the file on disk should be deleted
        /// </summary>
        internal override bool IsEmpty
        {
            get
            {
                return this.lights.Count == 0 && this.meshes.Count == 0 && this.decorations.Count == 0;
            }
        }

        [DataMember(Name = "version", Order = 0)]
        internal int Version { get; set; } = Config.BUILD_VERSION;

        [DataMember(Name = "enabled", Order = 1)]
        private bool enabled = true;

        internal bool Enabled
        {
            get
            {
                if (this.Pack != null)
                {
                    return this.Pack.GetFlag(this.Region.YardID + ":" + this.BaseName + ":enabled", this.enabled);
                }

                return this.enabled;
            }

            set
            {
                if (this.enabled && !value)
                {
                    this.Destroy();
                }

                if (this.Pack != null)
                {
                    this.Pack.SetFlag(this.Region.YardID + ":" + this.BaseName + ":enabled", value);
                    return;
                }

                this.enabled = value;
            }
        }

        [DataMember(Name = "lights", EmitDefaultValue = false, IsRequired = false, Order = 2)]
        private List<LightSpawner> lights = new List<LightSpawner>();

        [DataMember(Name = "meshes", EmitDefaultValue = false, IsRequired = false, Order = 3)]
        private List<MeshSpawner> meshes = new List<MeshSpawner>();

        [DataMember(Name = "decorations", EmitDefaultValue = false, IsRequired = false, Order = 4)]
        private List<DecorationSpawner> decorations = new List<DecorationSpawner>();

        internal LightSpawner CreateLight(string parentPath, Vector3 localPosition, Quaternion rotation, LightProperties properties)
        {
            if (!this.Editable)
            {
                return null;
            }

            return this.Add(new LightSpawner(parentPath, localPosition, rotation, properties)).Initialise().Save() as LightSpawner;
        }

        internal MeshSpawner CreateMesh(string parentPath, Vector3 localPosition, Quaternion rotation, MeshProperties properties)
        {
            if (!this.Editable)
            {
                return null;
            }

            return this.Add(new MeshSpawner(parentPath, localPosition, rotation, properties)).Initialise().Save() as MeshSpawner;
        }

        internal DecorationSpawner CreateDecoration(string parentPath, string id, MeshProperties properties, DutyCycle dutyCycle)
        {
            if (!this.Editable)
            {
                return null;
            }

            DecorationSpawner decoration = new DecorationSpawner(parentPath, id, properties);
            decoration.DutyCycle = dutyCycle;
            this.Add(decoration).Initialise().Save();
            return decoration;
        }

        [JsonConstructor]
        internal Group(Region region, string fileName)
        {
            this.Region = region;
            this.FileName = fileName;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<Spawner> GetEnumerator()
        {
            return this.lights.Concat<Spawner>(this.meshes).Concat(this.decorations).GetEnumerator();
        }

        internal Spawner Add(Spawner spawner)
        {
            if (!this.Editable)
            {
                return null;
            }

            if (spawner is LightSpawner light)
            {
                return this.Add(light);
            }

            if (spawner is MeshSpawner mesh)
            {
                return this.Add(mesh);
            }

            if (spawner is DecorationSpawner decoration)
            {
                return this.Add(decoration);
            }

            return null;
        }

        private LightSpawner Add(LightSpawner light)
        {
            light.Group?.lights.Remove(light);
            light.Group = this;
            this.lights.Add(light);
            return light;
        }

        private MeshSpawner Add(MeshSpawner mesh)
        {
            mesh.Group?.meshes.Remove(mesh);
            mesh.Group = this;
            this.meshes.Add(mesh);
            return mesh;
        }

        private DecorationSpawner Add(DecorationSpawner decoration)
        {
            decoration.Group?.decorations.Remove(decoration);
            decoration.Group = this;
            this.decorations.Add(decoration);
            return decoration;
        }

        internal void Tick(UpdateTicket ticket)
        {
            if (!this.Enabled)
            {
                return;
            }

            using (UpdateTicket.TimingSection section = ticket.Begin("meshes"))
            {
                this.Tick(ticket, this.meshes);
                section.Next("lights");
                this.Tick(ticket, this.lights);
                section.Next("decorations");
                this.Tick(ticket, this.decorations);
            }
        }

        private void Tick<T>(UpdateTicket ticket, List<T> spawners) where T : Spawner
        {
            foreach (T spawner in spawners)
            {
                spawner.Tick(ticket);
            }

            for (int index = spawners.Count - 1; index >= 0; index--)
            {
                T spawner = spawners[index];
                if (spawner.Errored)
                {
                    LightSniper.Logger.Debug("Removing ERRORED {0} {1}", spawner.GetType().Name, spawner.Name);
                    spawner.Delete();
                }
            }
        }

        internal void GlobalUpdate()
        {
            foreach (Spawner spawner in this)
            {
                if (spawner.IsGlobal)
                {
                    spawner.GlobalUpdate();
                }
            }
        }

        internal void GetNearestLight(RaycastHit hitInfo, ref LightSpawner nearest, ref float distanceToNearest)
        {
            this.GetNearest(this.lights, hitInfo, ref nearest, ref distanceToNearest);
        }

        internal void GetNearestMesh(RaycastHit hitInfo, ref MeshSpawner nearest, ref float distanceToNearest)
        {
            this.GetNearest(this.meshes, hitInfo, ref nearest, ref distanceToNearest);
        }

        private void GetNearest<T>(List<T> spawners, RaycastHit hitInfo, ref T nearest, ref float distanceToNearest) where T : Spawner
        {
            if (!this.Enabled)
            {
                return;
            }

            foreach (T spawner in spawners)
            {
                if (!spawner.Active || !spawner.Editable)
                {
                    continue;
                }

                float distanceTo = Vector3.Distance(spawner.Transform.position, hitInfo.point);
                if (distanceTo < distanceToNearest)
                {
                    distanceToNearest = distanceTo;
                    nearest = spawner;
                }
            }
        }

        internal Spawner Find(string path)
        {
            foreach (Spawner spawner in this)
            {
                if (spawner.LocalPath == path)
                {
                    return spawner;
                }
            }

            return null;
        }

        internal void Remove(Spawner spawner)
        {
            switch (spawner)
            {
                case LightSpawner light:
                    this.Remove(light);
                    return;
                case MeshSpawner mesh:
                    this.Remove(mesh);
                    return;
                case DecorationSpawner decoration:
                    this.Remove(decoration);
                    return;
            }
        }

        internal void Remove(LightSpawner light)
        {
            this.lights.Remove(light);
            this.Save();
        }

        internal void Remove(MeshSpawner mesh)
        {
            this.MeshRemoved?.Invoke(this, mesh);
            this.meshes.Remove(mesh);
            this.Save();
        }

        internal void Remove(DecorationSpawner decoration)
        {
            this.decorations.Remove(decoration);
            this.Save();
        }

        internal void OnMeshRemoved(MeshSpawner mesh)
        {
            List<LightSpawner> lightsToDelete = new List<LightSpawner>();
            foreach (LightSpawner light in this.lights)
            {
                if (light.IsParentDeleted(mesh))
                {
                    lightsToDelete.Add(light);
                }
            }

            foreach (LightSpawner light in lightsToDelete)
            {
                light.Delete();
            }
        }

        internal void Destroy()
        {
            foreach (Spawner spawner in this)
            {
                spawner.Destroy();
            }
        }

        internal void Reload()
        {
            // Not supported yet
        }

        internal void AutoSave()
        {
            if (this.ReadOnly)
            {
                return;
            }

            bool saveRequired = false;
            foreach (Spawner spawner in this)
            {
                saveRequired |= spawner.Dirty;
            }            

            if (saveRequired)
            {
                LightSniper.Logger.Debug("Auto-saving group {0}", this);
                this.Save();
            }
        }

        protected override void OnSaving()
        {
            Directory.CreateDirectory(this.Region.Dir);
        }

        protected override void OnSaved()
        {
            foreach (Spawner spawner in this)
            {
                spawner.Dirty = false;
            }
        }

        internal static Group Create(Region region, string fileName)
        {
            return Group.Load(region, fileName) ?? new Group(region, fileName);
        }

        internal static Group Load(Region region, string fileName)
        {
            return JsonStorage.Load<Group>(fileName)?.Awake(region, null);
        }

        internal static Group Load(Region region, Pack pack, string path)
        {
            return JsonStorage.Read<Group>(pack.OpenStream(path), path)?.Awake(region, pack);
        }

        public override string ToString()
        {
            return $"{this.Region.YardID}:{this.Name}";
        }

        private Group Awake(Region region, Pack pack)
        {
            this.Region = region;
            this.Pack = pack;

            if (this.lights == null)
            {
                this.lights = new List<LightSpawner>();
            }

            if (this.meshes == null)
            {
                this.meshes = new List<MeshSpawner>();
            }

            if (this.decorations == null)
            {
                this.decorations = new List<DecorationSpawner>();
            }

            foreach (Spawner spawner in this)
            {
                spawner.Awake(this);
            }

            if (this.Version < Config.BUILD_VERSION)
            {
                this.Upgrade();
            }

            if (!this.IsEmpty)
            {
                if (pack != null)
                {
                    LightSniper.Logger.Info("Loaded group {0} from pack {1} {2}", this, pack, this.FileName);
                }
                else
                {
                    LightSniper.Logger.Trace("Loaded local group {0} from {1}", this, this.FileName);
                }
            }

            return this;
        }

        private void Upgrade()
        {
            LightSniper.Logger.Info("Updating {0} from version {1} to version {2}", this, this.Version, Config.BUILD_VERSION);
            UpgradeHandler upgradeHandler = Group.GetUpgradeChain(this.Version);

            foreach (Spawner spawner in this)
            {
                spawner.UpgradeHandler = upgradeHandler;
            }

            this.Version = upgradeHandler.ToVersion;
            this.Save();
        }

        private static UpgradeHandler GetUpgradeChain(int fromVersion)
        {
            return new UpgradeHandler_1_92();
        }
    }
}
