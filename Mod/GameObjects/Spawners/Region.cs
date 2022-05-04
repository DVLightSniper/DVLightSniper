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
using DVLightSniper.Mod.GameObjects.Spawners.Packs;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using UpdateTicket = DVLightSniper.Mod.GameObjects.SpawnerController.UpdateTicket;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.GameObjects.Spawners
{
    /// <summary>
    /// Collection of Groups for a particular region. Regions are defined per the existing stations
    /// plus some extra regions (eg. for the "secret" garages and some landmarks like the castle and
    /// radio antenna) and fallback "wilderness" regions which cover 1km² tiles which are used when
    /// no region is available (eg. the user is placing a spawner too far from a station or
    /// landmark).
    /// </summary>
    internal class Region
    {
        // Radius within which active spawners in this region will be ticked
        internal const float TICK_RADIUS = 1536.0F;
        internal const float TICK_RADIUS_SQ = Region.TICK_RADIUS * Region.TICK_RADIUS;

        // Radius within which spawners in this region are allowed to spawn
        internal const float RADIUS = 1024.0F;
        internal const float RADIUS_SQ = Region.RADIUS * Region.RADIUS;

        internal const string DEFAULT_GROUP_NAME = "user";
        private const string DEFAULT_JSON = Region.DEFAULT_GROUP_NAME + ".json";

        /// <summary>
        /// Reference to the outer controller
        /// </summary>
        internal SpawnerController Controller { get; }

        /// <summary>
        /// The transform upon which this region is based (if any) used as the fallback transform
        /// when searching for a parent transform for sniped spawners
        /// </summary>
        internal Transform Anchor { get; set; }

        /// <summary>
        /// Yard ID for this region, usually the station yard ID but is custom for special regions.
        /// Defines the name of the directory in which this region's groups are stored.
        /// </summary>
        internal string YardID { get; }

        /// <summary>
        /// Absolute position in "world" (map) coordinates for the centre of this region. Can be
        /// zero for rectangular regions.
        /// </summary>
        internal Vector2 WorldLocation { get; }

        /// <summary>
        /// If WorldLocation is 0,0 then defines the rectangular area covered by this Region, used
        /// for wilderness (tile) regions
        /// </summary>
        internal Rect Area { get; }

        /// <summary>
        /// If using rectangular area instead of worldlocation + radius then this is the actual area
        /// which the region can tick
        /// </summary>
        private Rect TickArea { get; }

        /// <summary>
        /// Path to the storage
        /// </summary>
        internal string Dir { get; }

        /// <summary>
        /// Set of resources which have been loaded, to allow loading of fresh resources when
        /// reloading
        /// </summary>
        private readonly ISet<string> loadedResources = new HashSet<string>();

        /// <summary>
        /// Groups in this region
        /// </summary>
        private readonly List<Group> groups = new List<Group>();
        
        /// <summary>
        /// Default group (user) which is used when adding new spawners with no other group selected
        /// </summary>
        private readonly Group defaultGroup;

        /// <summary>
        /// Currently selected group to add new spawners to
        /// </summary>
        internal Group CurrentGroup { get; private set; }

        internal Region(SpawnerController controller, Vector2 worldLocation, string yardId, string yardGroup = null) :
            this(controller, worldLocation, Rect.zero, yardId, yardGroup)
        {
        }

        internal Region(SpawnerController controller, Rect area, string yardId, string yardGroup = null) :
            this(controller, Vector2.zero, area, yardId, yardGroup)
        {
        }

        private Region(SpawnerController controller, Vector2 worldLocation, Rect area, string yardId, string yardGroup)
        {
            this.Controller = controller;
            this.WorldLocation = worldLocation;
            this.Area = area;
            this.YardID = yardId;

            if (area.width * area.height > 0)
            {
                float padding = Math.Max(0.0F, Region.TICK_RADIUS - Region.RADIUS);
                this.TickArea = new Rect(area.x - padding, area.y - padding, area.width + padding, area.height + padding);
            }

            string groupDir = yardGroup == null ? SpawnerController.RegionsDir : Path.Combine(SpawnerController.RegionsDir, yardGroup);
            this.Dir = Path.Combine(groupDir, yardId);
            Directory.CreateDirectory(yardGroup != null ? groupDir : this.Dir);

            this.AddGroup(this.CurrentGroup = this.defaultGroup = Group.Create(this, Path.Combine(this.Dir, Region.DEFAULT_JSON)));
            this.LoadAllGroups();
        }

        private void LoadAllGroups()
        {
            DirectoryInfo dir = new DirectoryInfo(this.Dir);
            if (dir.Exists)
            {
                foreach (FileInfo jsonFile in dir.EnumerateFiles("*.json"))
                {
                    string resourceId = jsonFile.FullName;
                    if (jsonFile.Name == Region.DEFAULT_JSON || this.loadedResources.Contains(resourceId))
                    {
                        continue;
                    }

                    this.AddGroup(Group.Load(this, jsonFile.FullName));
                    this.loadedResources.Add(resourceId);
                }
            }

            // Path inside the pack which begins Regions/YARDGROUPID/REGIONID
            string relativeRegionsDir = this.Dir.RelativeToBaseDir() + "\\";

            // Path inside the pack which is just YARDGROUPID/REGIONID in case the person making the
            // pack accidentally just zipped the Regions directory
            string relativeBareDir = this.Dir.RelativeToDir(SpawnerController.RegionsDir) + "\\";

            foreach (Pack pack in PackLoader.Packs)
            {
                foreach (string json in pack.Find(relativeRegionsDir, ".json"))
                {
                    string resourceId = pack.Path + "!" + json;
                    if (!this.loadedResources.Contains(resourceId))
                    {
                        this.AddGroup(Group.Load(this, pack, json));
                        this.loadedResources.Add(resourceId);
                    }
                }

                foreach (string json in pack.Find(relativeBareDir, ".json"))
                {
                    string resourceId = pack.Path + "!" + json;
                    if (!this.loadedResources.Contains(resourceId))
                    {
                        this.AddGroup(Group.Load(this, pack, json));
                        this.loadedResources.Add(resourceId);
                    }
                }
            }
        }

        internal void Reload()
        {
            // First reload existing groups
            foreach (Group group in this.groups)
            {
                group.Reload();
            }

            // Now load any new groups
            this.LoadAllGroups();
        }

        private void AddGroup(Group group)
        {
            if (group != null)
            {
                this.groups.Add(group);
                this.SortGroups();
                group.MeshRemoved += this.OnMeshRemoved;
            }
        }

        private void SortGroups()
        {
            this.groups.Sort((a, b) => a.Priority == b.Priority ? string.Compare(a.Name, b.Name, StringComparison.Ordinal) : a.Priority - b.Priority);
        }

        internal void Save()
        {
            foreach (Group group in this.groups)
            {
                group.Save();
            }
        }

        internal void AutoSave()
        {
            foreach (Group group in this.groups)
            {
                group.AutoSave();
            }
        }

        internal void BeginGroup(string groupName)
        {
            this.CurrentGroup = this.GetGroup(groupName);
            this.CurrentGroup.Enabled = true;
        }

        internal Group GetGroup(string groupName, bool createIfAbsent = true)
        {
            if (groupName.Equals(Region.DEFAULT_GROUP_NAME, StringComparison.InvariantCultureIgnoreCase))
            {
                return this.defaultGroup;
            }

            foreach (Group group in this.groups)
            {
                if (group.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return group;
                }
            }

            if (!createIfAbsent)
            {
                return null;
            }

            Group newGroup = Group.Create(this, Path.Combine(this.Dir, groupName + ".json"));
            this.AddGroup(newGroup);
            return newGroup;
        }

        internal void EndGroup()
        {
            this.CurrentGroup = this.defaultGroup;
        }

        internal void EnableGroup(string groupName, bool enabled)
        {
            foreach (Group group in this.groups)
            {
                if (groupName == "*" || group.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase))
                {
                    group.Enabled = enabled;
                    group.Save();
                }
            }
        }

        internal void SetGroupPriority(string groupName, Priority priority)
        {
            foreach (Group group in this.groups)
            {
                if (group.Editable && (groupName == "*" || group.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    group.Priority = priority;
                    group.Save();
                }
            }

            this.SortGroups();
        }

        private void OnMeshRemoved(Group sender, MeshSpawner mesh)
        {
            foreach (Group group in this.groups)
            {
                group.OnMeshRemoved(mesh);
            }
        }

        internal T Add<T>(T spawner) where T : Spawner
        {
            return (T)this.CurrentGroup.Add(spawner);
        }

        internal bool Contains(Vector2 worldLocation, bool tickRadius, bool radiusOnly)
        {
            float radiusSq = tickRadius ? Region.TICK_RADIUS_SQ : Region.RADIUS_SQ;
            Rect area = tickRadius ? this.TickArea : this.Area;
            return this.WorldLocation == Vector2.zero ? !radiusOnly && area.Contains(worldLocation) : (this.WorldLocation - worldLocation).sqrMagnitude < radiusSq;
        }

        internal void Tick(UpdateTicket ticket)
        {
            if (this.Contains(ticket.PlayerLocation, true, false))
            {
                this.Controller.NotifyUpdated(this);
                foreach (Group group in this.groups)
                {
                    group.Tick(ticket);
                }
            }
            else
            {
                foreach (Group group in this.groups)
                {
                    // Global update is only used to update culling for global objects outside of the normal tick radius
                    group.GlobalUpdate(ticket);
                }
            }
        }

        internal void Destroy()
        {
            foreach (Group group in this.groups)
            {
                group.Destroy();
            }
        }

        internal void CleanUp()
        {
            if (this.groups.Any(group => group.Pack == null && !group.IsEmpty))
            {
                return;
            }

            try
            {
                if (Directory.Exists(this.Dir))
                {
                    Directory.Delete(this.Dir);
                }
            }
            catch
            {
                // don't care that much, probably another file in the directory
            }
        }

        internal LightSpawner CreateLight(string parentPath, Vector3 localPosition, Quaternion rotation, LightProperties properties)
        {
            LightSpawner light = this.CurrentGroup.CreateLight(parentPath, localPosition, rotation, properties);
            light.ForceUpdate = true;
            return light;
        }

        internal MeshSpawner CreateMesh(string parentPath, Vector3 localPosition, Quaternion rotation, MeshProperties properties)
        {
            MeshSpawner mesh = this.CurrentGroup.CreateMesh(parentPath, localPosition, rotation, properties);
            mesh.ForceUpdate = true;
            return mesh;
        }

        internal DecorationSpawner CreateDecoration(string parentPath, string id, MeshProperties properties, DutyCycle dutyCycle)
        {
            DecorationSpawner decoration = this.CurrentGroup.CreateDecoration(parentPath, id, properties, dutyCycle);
            decoration.ForceUpdate = true;
            return decoration;
        }

        internal void GetNearestLight(RaycastHit hitInfo, ref LightSpawner nearest, ref float distanceToNearest)
        {
            foreach (Group group in this.groups)
            {
                group.GetNearestLight(hitInfo, ref nearest, ref distanceToNearest);
            }
        }

        internal Spawner Find(string path)
        {
            foreach (Group group in this.groups)
            {
                Spawner spawner = group.Find(path);
                if (spawner != null)
                {
                    return spawner;
                }
            }

            return null;
        }
    }
}
