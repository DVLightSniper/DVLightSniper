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

using DVLightSniper.Mod.GameObjects.Spawners.Upgrade;
using DVLightSniper.Mod.Util;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;
using UpdateTicket = DVLightSniper.Mod.GameObjects.SpawnerController.UpdateTicket;
using TimingSection = DVLightSniper.Mod.GameObjects.SpawnerController.TimingSection;
using TimingLevel = DVLightSniper.Mod.GameObjects.SpawnerController.TimingLevel;

using UnityEngine;

using Random = System.Random;

namespace DVLightSniper.Mod.GameObjects.Spawners
{
    /// <summary>
    /// Base dynamic spawner object, manages spawning a gameobject (a light, mesh or decoration)
    /// into the world based on the presence of a known parent transform.
    /// 
    /// Spawners exist in a 1:1 relationship with the objects they create, spawners do not spawn
    /// multiple objects though they do manage the lifecycle of the objects they create in terms of
    /// tracking when the spawned objects are destroyed and attempting to recreate them.
    /// 
    /// The purpose of a Spawner is to allow data-driven creation of objects in the world using a
    /// human-readable syntax (in this case json). Each spawner contains the full path of a parent
    /// transform, along with properties defining the object to spawn once the parent is located,
    /// and offset position and rotation from the parent.
    /// 
    /// Since searching for game objects is expensive, spawners are also internally rate-limited in
    /// multiple ways to avoid lagging the game with too many expensive lookups per frame. The
    /// following rate limits are applied:
    /// 
    /// * Each individual spawner is not allowed to search more often than UPDATE_RATE (5 seconds)
    /// * Each update tick is limited to a maximum number of searches based on the UpdateStrategy
    ///   defined in Settings, by default this is very low (3)
    /// * If all updates allowed are consumed in a single tick (eg. 3) then the updates are paused
    ///   for the back-off time defined in the UpdateStrategy
    /// * All update behaviour is limited to a maximum of SpawnerController.MAX_UPDATE_TIME (25ms)
    /// 
    /// Once the spawner has successfully located the parent transform, it spawns and manages a
    /// GameObject and configures its properties. The spawner also manages culling the object based
    /// on the defined DistanceCullingRadius by disabling the object when the player is far away.
    /// 
    /// For global objects with no parent transform which are spawned in absolute scene coordinates,
    /// hard culling (deletion of the GameObject) is also performed when the player exceeds the
    /// defined culling radius, which is independent of the DistanceCullingRadius used to simply
    /// hide objects which are far away.
    /// 
    /// In general, the parent transforms are torn down when the player moves far enough away from
    /// them, and the spawned objects will be destroyed when their parents are. Therefore there is
    /// no specific logic included for removing objects which are far away, since the game will
    /// handle this automatically. 
    /// </summary>
    [DataContract]
    internal abstract class Spawner
    {
        /// <summary>
        /// Random source
        /// </summary>
        private static readonly Random RANDOM_SOURCE = new Random();

        /// <summary>
        /// Update rate for unspawned objects which have not discovered their parent transforms yet
        /// </summary>
        private const int UPDATE_RATE = 5000; // ms

        /// <summary>
        /// Event fired when the GameObject is deleted
        /// </summary>
        internal event Action<Spawner> OnDeleted;

        private Group group;

        /// <summary>
        /// The Group which contains this spawner
        /// </summary>
        internal Group Group
        {
            get
            {
                return this.group;
            }

            set
            {
                if (this.group != null)
                {
                    this.group.EnabledChanged -= this.OnGroupToggled;
                }

                this.group = value;
                this.ParentPath = this.ParentPath; // reapply parent path to inline group reference

                if (this.group != null)
                {
                    this.group.EnabledChanged += this.OnGroupToggled;
                }
            }
        }

        /// <summary>
        /// The region which contains the group which contains this spawner
        /// </summary>
        internal Region Region { get { return this.Group.Region; } }

        /// <summary>
        /// Whether this spawner is editable (backed by JSON), false if the group was read from
        /// inside a zip file
        /// </summary>
        internal virtual bool Editable { get { return this.Group.Editable; } }

        /// <summary>
        /// The name of this spawner, computed by the relevant subclass
        /// </summary>
        internal abstract string Name { get; }

        /// <summary>
        /// The stored path of the parent transform, can be empty if the object is global and should
        /// be attached to the world mover instead
        /// </summary>
        [DataMember(Name = "parent", Order = 0)]
        private string parentPathRaw;

        /// <summary>
        /// Computed parent path with tokens replaced, cached so we don't have to recompute it all
        /// the time
        /// </summary>
        private string parentPathReal;

        /// <summary>
        /// The transform path to the parent object for this spawner, internally the name of the
        /// group is replaced with a token so that the group file can be safely renamed and parent
        /// relationships will still work.
        /// </summary>
        internal string ParentPath
        {
            get
            {
                if (this.parentPathReal != null || this.parentPathRaw == null)
                {
                    return this.parentPathReal;
                }

                if (!this.parentPathRaw.Contains("%GROUP%_"))
                {
                    return this.parentPathReal = this.parentPathRaw;
                }

                if (this.Group == null)
                {
                    throw new InvalidOperationException("Attempted to read a raw parent path before Group is set");
                }

                return this.parentPathReal = this.parentPathRaw.Replace("%GROUP%_", this.Group.Prefix);
            }

            set
            {
                string oldRawPath = this.parentPathRaw;
                this.parentPathRaw = this.parentPathReal = value;
                if (this.Group != null && !string.IsNullOrEmpty(value))
                {
                    this.parentPathRaw = value.Replace(this.Group.Prefix, "%GROUP%_");
                }
                this.Dirty |= oldRawPath != this.parentPathRaw;
            }
        }

        /// <summary>
        /// The transform path of this object, based on the parent transform plus the name of the
        /// object
        /// </summary>
        internal string LocalPath
        {
            get
            {
                return this.IsGlobal ? this.Name : this.ParentPath + "/" + this.Name;
            }
        }

        /// <summary>
        /// True if this is a global (parented to the world mover) object
        /// </summary>
        internal bool IsGlobal { get { return string.IsNullOrEmpty(this.ParentPath); } }

        /// <summary>
        /// Prefix part for global elements
        /// </summary>
        protected string GlobalPrefix { get { return this.IsGlobal ? "Global" : ""; } }

        /// <summary>
        /// Position of the spawned object relative to the parent transform. For global objects this
        /// is the world coordinate. For decorations this value is ignored.
        /// </summary>
        [DataMember(Name = "position", Order = 1, EmitDefaultValue = false)]
        internal Vector3 LocalPosition { get; private set; }

        /// <summary>
        /// Rotation of the spawned object. For lights and decorations this value is ignored.
        /// </summary>
        [DataMember(Name = "rotation", Order = 2, EmitDefaultValue = false)]
        internal Quaternion Rotation { get; private set; }

        /// <summary>
        /// Hashed location of the parent and the object location, used as a sanity check
        /// </summary>
        [DataMember(Name = "hash", Order = 3, EmitDefaultValue = false)]
        private string Hash { get; set; }

        /// <summary>
        /// Whether this spawner is eligible to trigger autosave
        /// </summary>
        internal bool Dirty { get; set; }

        /// <summary>
        /// Hash of the parent location, used to verify the parent has been identified correctly
        /// </summary>
        internal string ParentHash
        {
            get
            {
                return this.GetHashPart(0);
            }

            private set
            {
                this.SetHashPart(0, value);
            }
        }

        /// <summary>
        /// Hash of the spawned object location, not currently used but intended to be used for
        /// upgrade handling since the original object location can be extracted from this field
        /// </summary>
        internal string TransformHash
        {
            get
            {
                return this.GetHashPart(1);
            }

            private set
            {
                this.SetHashPart(1, value);
            }
        }

        /// <summary>
        /// Manual culling distance, usually only set for global objects which will not be culled
        /// when their parent transforms despawn, though can be used for any object
        /// </summary>
        [DataMember(Name = "cullDistance", Order = 4, EmitDefaultValue = false)]
        internal int CullDistance { get; set; }

        /// <summary>
        /// Squared culling distance, used for range checks
        /// </summary>
        private int CullDistanceSq { get { return this.CullDistance * this.CullDistance; } }

        /// <summary>
        /// Flag which determines that the object ignores distance culling. Mainly used for lights
        /// which are visible from very long distances (eg. warning beacons on tall structures)
        /// </summary>
        protected bool DisableDistanceCulling { get; set; }

        /// <summary>
        /// Value from 0.0 to 1.0 indicating the amount to fade the object (usually light intensity)
        /// so that the light doesn't "pop in" at the edge of the culling radius and instead
        /// smoothly fades up/down as it enters and leaves the culling radius.
        /// </summary>
        internal float PreCullFade { get; private set; }

        /// <summary>
        /// True if culled
        /// </summary>
        internal bool Culled { get; private set; }

        /// <summary>
        /// Backing field for the spawned GameObject
        /// </summary>
        private GameObject gameObject;

        /// <summary>
        /// Handle to the spawned GameObject
        /// </summary>
        internal GameObject GameObject
        {
            get
            {
                return this.gameObject;
            }

            private set
            {
                this.gameObject = value;
                this.rateLimit.Restart();
                if (value != null)
                {
                    this.TransformHash = value.transform.AsWorldCoordinate().AsHex(true);
                }
            }
        }

        /// <summary>
        /// Transform of the spawned GameObject
        /// </summary>
        internal Transform Transform
        {
            get
            {
                return this.gameObject?.transform;
            }
        }

        /// <summary>
        /// True if the spawner has a GameObject
        /// </summary>
        internal bool Active
        {
            get
            {
                return this.GameObject != null;
            }
        }

        /// <summary>
        /// Flag to override normal rate-limiting of updates, for example when the object has been
        /// restored via an "undo" action so that the object is recreated immediately rather than
        /// waiting for an available update.
        /// </summary>
        internal bool ForceUpdate { get; set; }

        /// <summary>
        /// True if this spawner is selected, for example when designing
        /// </summary>
        internal bool Selected { get; set; }

        /// <summary>
        /// Selection colour to display the object when selected, eg. wireframe colour for
        /// highlighted meshes
        /// </summary>
        internal Color SelectionColour { get; set; } = Color.white;

        /// <summary>
        /// True if the object has been deleted
        /// </summary>
        internal bool Deleted { get; set; }

        /// <summary>
        /// True if the object experiences an unexpected error (throws an exception) during the
        /// update process. This flag is set to indicate that the group should delete the spawner.
        /// </summary>
        internal bool Errored { get; private set; }

        /// <summary>
        /// Stub for upgrade functionality
        /// </summary>
        internal UpgradeHandler UpgradeHandler { get; set; }

        /// <summary>
        /// Used to keep track of the last time the object was updated while inactive, in order to
        /// honour the rate-limiting contract
        /// </summary>
        private readonly Stopwatch rateLimit = new Stopwatch();

        /// <summary>
        /// Culling is one of the more expensive operations when a spawner is active, so we avoid it
        /// where possible. We already inhibit culling checks when the player is not moving, but
        /// when spawners are far beyond the culling radius we also update their culling state less
        /// frequently based on how far away they are. This is particularly good at reducing
        /// overhead while the player is moving, giving back on average about 30-35% of time at the
        /// default radius, and as much as 50% or more for smaller radii. This stopwatch keeps track
        /// of the inhibit time when spawners are far away, and works with cullCheckInhibitFor to
        /// impement cull check inhibiting.
        /// </summary>
        private readonly Stopwatch cullCheckInhibitTimer = new Stopwatch();

        /// <summary>
        /// Number of milliseconds to inhibit cull checking for, see cullCheckInhibitTimer
        /// </summary>
        private int cullCheckInhibitFor = 0;

        /// <summary>
        /// Base number of milliseconds to inhibit checking for, computed as a random spread so that
        /// inhibited spawners are randomly ticked to reduce spikes
        /// </summary>
        private readonly int cullCheckInhibitSpread;

        /// <summary>
        /// Debug overlay marker
        /// </summary>
        private DebugOverlay.Marker marker;

        private readonly TimingSection tickTimings, spawnTimings, cullTimings, updateTimings;

        protected Spawner(string parentPath, Vector3 localPosition, Quaternion rotation)
        {
            this.ParentPath = parentPath;
            this.LocalPosition = localPosition;
            this.Rotation = rotation;
            this.rateLimit.Start();

            this.cullCheckInhibitSpread = Spawner.RANDOM_SOURCE.Next(230, 270);

            string timingSectionPrefix = this.GetType().Name.ToLowerInvariant().Substring(0, this.GetType().Name.IndexOf("Spawner", StringComparison.Ordinal));
            this.tickTimings   = TimingSection.Get(timingSectionPrefix + ".tick", TimingLevel.Spawner);
            this.spawnTimings  = TimingSection.Get(timingSectionPrefix + ".spawn", TimingLevel.Spawner);
            this.cullTimings   = TimingSection.Get(timingSectionPrefix + ".cull", TimingLevel.SpawnerDetailed);
            this.updateTimings = TimingSection.Get(timingSectionPrefix + ".update", TimingLevel.SpawnerDetailed);
        }

        private void OnGroupToggled(bool enabled)
        {
            this.rateLimit.Restart();
        }

        /// <summary>
        /// Called immediately after the spawner is created and added to the group, used to
        /// initialise any custom properties required by the spawner
        /// </summary>
        /// <returns></returns>
        internal virtual Spawner Initialise()
        {
            this.CullDistance = this.IsGlobal ? (int)Region.RADIUS : 0;
            return this;
        }

        /// <summary>
        /// Called immediately after the group is deserialised, assigns the group reference and any
        /// other functionality required after deserialisation is completed.
        /// </summary>
        /// <param name="group"></param>
        /// <returns>fluent</returns>
        internal virtual Spawner Awake(Group group)
        {
            this.Group = group;
            return this;
        }

        protected void SetGameObject(GameObject gameObject)
        {
            this.GameObject = gameObject;
            if (gameObject == null)
            {
                this.OnDestroyed();
            }
        }

        /// <summary>
        /// Update for global objects which is called even when outside ticking radius for the
        /// parent group
        /// </summary>
        internal void GlobalUpdate(UpdateTicket ticket)
        {
            if (this.Active)
            {
                this.UpdateCulling(ticket);
            }
        }

        /// <summary>
        /// Update for all objects which is called when the containing group is active
        /// </summary>
        /// <param name="ticket"></param>
        internal void Tick(UpdateTicket ticket)
        {
            try
            {
                this.TryUpdate(ticket);
            }
            catch (Exception e)
            {
                this.Errored = true;
                LightSniper.Logger.Debug(e);
            }
        }

        internal void TryUpdate(UpdateTicket ticket)
        {
            // Don't update if deleted
            if (this.Deleted)
            {
                if (this.marker != null)
                {
                    DebugOverlay.RemoveMarker(this.marker);
                    this.marker = null;
                }

                return;
            }

            // Disable the debug marker if the GameObject despawns
            if (!this.Active && this.marker != null && !SpawnerController.MarkOrphans)
            {
                this.marker.Enabled = false;
            }

            if (this.Active)
            {
                this.tickTimings.Start();
                this.ActiveTick(ticket);
                this.tickTimings.End();
            }
            else if (!this.Active && (this.ForceUpdate || this.rateLimit.ElapsedMilliseconds > Spawner.UPDATE_RATE && ticket.HasUpdatesRemaining))
            {
                this.spawnTimings.Start();
                this.SpawnerTick(ticket);
                this.spawnTimings.End();
            }
            // else if (this.rateLimit.ElapsedMilliseconds > 300000)
            // {
            //     LightSniper.Logger.Warn("{0}({1}) is update starved, waited {2}ms without update", this.GetType().Name, this.Name, this.rateLimit.ElapsedMilliseconds);
            // }
        }

        private void ActiveTick(UpdateTicket ticket)
        {
            if (this.marker == null && LightSniper.Settings.ShowDebugMarkers)
            {
                this.marker = DebugOverlay.AddMarker(this.Name, this is LightSpawner ? Color.yellow : Color.cyan);
            }

            if (this.marker != null)
            {
                this.marker.Enabled = true;
                this.marker.Position = this.Transform.position;
            }

            if (LoadingScreenManager.IsLoading || FastTravelController.IsFastTravelling)
            {
                return;
            }

            this.cullTimings.Start();
            bool visible = this.UpdateCulling(ticket);
            this.cullTimings.End();

            this.updateTimings.Start();
            this.Tick(ticket, visible);
            this.updateTimings.End();
        }

        protected abstract void Tick(UpdateTicket ticket, bool visible);

        private void SpawnerTick(UpdateTicket ticket)
        {
            this.rateLimit.Restart();
            this.ForceUpdate = false;

            for (; this.UpgradeHandler != null; this.UpgradeHandler = this.UpgradeHandler.Next)
            {
                LightSniper.Logger.Debug("UPGRADE: Upgrading {0} {1} from version {2} to version {3}", this.GetType().Name, this.Name, this.UpgradeHandler.FromVersion, this.UpgradeHandler.ToVersion);
                this.UpgradeHandler.Upgrade(this);
            }

            GameObject parent = this.FindParent();
            if (parent == null)
            {
                if (this.IsGlobal)
                {
                    // If this is a global object, FindParent should return the world mover. If the
                    // parent is null then something went wrong so just bug out now.
                    return;
                }

                ticket.Mark(1);

                if (LoadingScreenManager.IsLoading || FastTravelController.IsFastTravelling)
                {
                    // If we're fast travelling or loading then there's a good chance that parents
                    // just aren't loaded yet, so let's not panic right now, save panic for later
                    return;
                }

                LightSniper.Logger.Trace("TICK: {0} {1} cannot find parent {2} (previous position {3})", this.GetType().Name, this.Name, this.ParentPath, this.TransformHash.HexToVector());
                if (!string.IsNullOrEmpty(this.ParentHash))
                {
                    if (SpawnerController.KillOrphans)
                    {
                        this.Errored = true;
                        LightSniper.Logger.Trace("TICK: Orphaned {0} {1} is being killed", this.GetType().Name, this.Name);
                        return;
                    }

                    if (SpawnerController.MarkOrphans)
                    {
                        if (this.marker == null)
                        {
                            this.marker = DebugOverlay.AddMarker("ORPHAN " + this.Name, Color.red);
                        }
                        this.marker.Important = true;

                        if (!string.IsNullOrEmpty(this.TransformHash))
                        {
                            this.marker.Position = this.TransformHash.HexToVector().AsOffsetCoordinate();
                        }
                        else
                        {
                            this.marker.Position = (this.ParentHash.HexToVector().AsOffsetCoordinate()) + this.LocalPosition;
                        }
                    }
                }

                ticket.Drift();
                return;
            }

            if (!this.CheckHash(parent))
            {
                // Position sanity check failed
                return;
            }

            if (this.IsGlobal && this.CullDistance > 0 && (this.LocalPosition.AsOffsetCoordinate() - PlayerManager.PlayerTransform.position).sqrMagnitude > this.CullDistanceSq)
            {
                // Global object is outside defined culling radius
                return;
            }

            if (this.Spawn(ticket, parent) && !LoadingScreenManager.IsLoading)
            {
                this.UpdateCulling(ticket, true);
            }
        }

        private bool CheckHash(GameObject parent)
        {
            string parentHash = parent.transform.position.AsWorldCoordinate().AsHex(true);
            if (string.IsNullOrEmpty(this.ParentHash))
            {
                this.ParentHash = parentHash;
            }
            else if (this.ParentHash != parentHash)
            {
                LightSniper.Logger.Trace("+++> Parent {0} of {1} has moved!!! Previous {2}, new {3}, previous pos {4}, new pos {5}", this.ParentPath, this.Name, this.ParentHash, parentHash, this.ParentHash.HexToVector(), parent.transform.position.AsWorldCoordinate());

                if (SpawnerController.FreeOrphans)
                {
                    this.ParentHash = parentHash;
                }

                return false;
            }

            return true;
        }

        protected abstract bool Spawn(UpdateTicket ticket, GameObject parent);

        private bool UpdateCulling(UpdateTicket ticket, bool spawnCheck = false)
        {
            if (this.cullCheckInhibitFor > 0)
            {
                if (this.cullCheckInhibitTimer.ElapsedMilliseconds < this.cullCheckInhibitFor)
                {
                    return !this.Culled;
                }
            }
            else if (!ticket.HasMoved)
            {
                return !this.Culled;
            }

            this.cullCheckInhibitFor = 0;
            this.cullCheckInhibitTimer.Reset();

            if (SpawnerController.CullEverything)
            {
                this.PreCullFade = 0.0F;
                this.SetCulled(true);
                return false;
            }

            float distanceSq = this.Transform.position.DistanceSq2d(PlayerManager.PlayerTransform.position);

            if (this.CullDistance > 0 && distanceSq > this.CullDistanceSq)
            {
                if (this.GameObject != null)
                {
                    LightSniper.Logger.Trace("+++> Manually culling {0} because distance exceeds cull radius of {1}", this.Name, this.CullDistance);

                    if (spawnCheck)
                    {
                        LightSniper.Logger.Warn("Attempted to cull {0} immediately after spawn, this should not happen and indicates something is wrong.", this.Name);
                        return true;
                    }

                    Object.Destroy(this.GameObject);
                }
                return false;
            }

            if (this.DisableDistanceCulling || LightSniper.Settings.DistanceCullingRadiusSq == 0 || SpawnerController.IsFreeCamActive)
            {
                this.PreCullFade = 1.0F;
                this.SetCulled(false);
                return true;
            }

            if (distanceSq > LightSniper.Settings.DistanceCullingRadiusSq)
            {
                this.PreCullFade = 0.0F;
                this.SetCulled(true);

                int cullingRadius = LightSniper.Settings.DistanceCullingRadius;

                int inhibitRadius1 = Math.Max(200, (int)(cullingRadius * 1.2)); // at 20% out or 200m, whichever is largest, inhibit the culling check for around 250ms (randomly spread)
                if (distanceSq > (inhibitRadius1 * inhibitRadius1))
                {
                    this.cullCheckInhibitFor = this.cullCheckInhibitSpread;

                    int inhibitRadius2 = Math.Max(400, (int)(cullingRadius * 1.4)); // at 50% out or 400m, whichever is largest, inhibit the check for around 500ms (randomly spread)
                    if (distanceSq > (inhibitRadius2 * inhibitRadius2))
                    {
                        this.cullCheckInhibitFor *= 2;
                    }

                    this.cullCheckInhibitTimer.Restart();
                }
                return false;
            }

            this.SetCulled(false);
            double beginFadeDistanceSq = LightSniper.Settings.DistanceCullingRadiusSq * 0.9;
            if (distanceSq < beginFadeDistanceSq)
            {
                this.PreCullFade = 1.0F;
                return true;
            }

            this.PreCullFade = (float)Math.Min(Math.Max(1.0 - (distanceSq - beginFadeDistanceSq) / (LightSniper.Settings.DistanceCullingRadiusSq - beginFadeDistanceSq), 0.0), 1.0);
            return true;
        }

        protected virtual void SetCulled(bool culled, bool updateGameObject = true)
        {
            this.Culled = culled;
            if (updateGameObject)
            {
                this.GameObject.SetActive(!culled);
            }
        }

        private GameObject FindParent()
        {
            if (!this.IsGlobal)
            {
                return this.FindParent(this.ParentPath);
            }

            // Empty parent means we need to parent to the world mover, but only spawn if we are in range
            if (this.CullDistance > 0)
            {
                // "local" position is world position in this situation since the stored location is the world coordinate
                float distanceSq = (this.LocalPosition.AsOffsetCoordinate() - PlayerManager.PlayerTransform.position).sqrMagnitude;
                if (distanceSq > this.CullDistanceSq)
                {
                    // LightSniper.Logger.Debug(">>> Global object {0} is out of range for world position {1}", this.Name, this.LocalPosition);
                    return null;
                }
            }

            return SingletonBehaviour<WorldMover>.Instance.originShiftParent.gameObject;
        }

        private GameObject FindParent(string parentPath)
        {
            GameObject parent = GameObjectPath.Find(parentPath);
            return parent ?? this.Region.Find(this.LocalPath)?.GameObject;
        }

        /// <summary>
        /// Delete the spawner, destroy the spawned object as well
        /// </summary>
        internal virtual bool Delete()
        {
            if (!this.Editable)
            {
                return false;
            }

            this.marker?.Remove();

            this.Deleted = true;
            try
            {
                this.Group?.Remove(this);
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }

            Action<Spawner> onDeleted = this.OnDeleted;
            onDeleted?.Invoke(this);

            this.Destroy();
            return true;
        }

        internal virtual void Destroy()
        {
            if (this.GameObject != null)
            {
                Object.Destroy(this.GameObject);
                this.SetGameObject(null);
            }
        }

        protected virtual void OnDestroyed()
        {
            if (this.marker != null)
            {
                DebugOverlay.RemoveMarker(this.marker);
                this.marker = null;
            }
        }

        internal virtual void Undelete()
        {
            this.Deleted = false;
            this.ForceUpdate = true;
            this.Group.Add(this);
        }

        public virtual void OnSaving()
        {
        }

        internal virtual Spawner Save()
        {
            this.Group?.Save();
            return this;
        }

        internal void Apply()
        {
            if (this.GameObject != null)
            {
                this.Configure(this.GameObject);
            }
        }

        internal abstract void Configure(GameObject gameObject);

        private string GetHashPart(int index)
        {
            string[] parts = this.Hash?.Split(':');
            return parts == null || parts.Length < index + 1 ? null : parts[index];
        }

        private void SetHashPart(int index, string value)
        {
            string[] parts = this.Hash?.Split(':');
            if (parts == null || parts.Length < 2)
            {
                parts = new [] { parts?.Length == 1 ? parts[0] : "", "" };
            }
            parts[index] = value;
            this.Hash = string.Join(":", parts);
            this.Dirty = true;
        }
    }
}
