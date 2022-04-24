﻿//------------------------------------------------------------------------------
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
using System.IO;
using System.Runtime.Serialization;
using System.Text;

using CommandTerminal;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.Storage;
using DVLightSniper.Mod.Time;

using JetBrains.Annotations;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

using DVLightSniper.Mod.GameObjects.Library;
using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Spawners.Packs;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.Util;

using RedworkDE.DvTime;

using TemplateMatch = DVLightSniper.Mod.GameObjects.Library.DecorationTemplate.TemplateMatch;

using DateTime = System.DateTime;

namespace DVLightSniper.Mod.GameObjects
{
    /// <summary>
    /// Main singleton object which manages creation, spawning and updating of dynamic objects we
    /// want to add to the scene.
    /// </summary>
    public class SpawnerController : MonoBehaviour
    {
        /// <summary>
        /// An update ticket used to control rate-limiting of object spawn checks during each tick.
        /// The ticket can be seeded with the desired maximum number of updates for the tick in
        /// question and each object which performs an expensive operation (eg. a GameObject.Find to
        /// locate its desired parent transform) punches the ticket, decrementing the update
        /// counter. Each object which is active or orphaned punches the ticket as well for
        /// debugging purposes.
        /// </summary>
        internal class UpdateTicket
        {
            internal class TimingSection : IDisposable
            {
                private readonly Action<string, double> callback;

                private string name;

                private DateTime start = DateTime.Now;

                public TimingSection(string name, Action<string, double> callback)
                {
                    this.name = name;
                    this.callback = callback;
                }

                public void Next(string name)
                {
                    this.End();
                    this.name = name;
                    this.start = DateTime.Now;
                }

                public void End()
                {
                    double elapsedTime = (DateTime.Now - this.start).TotalMilliseconds;
                    this.callback(this.name, elapsedTime);
                }

                public void Dispose()
                {
                    this.End();
                }
            }

            /// <summary>
            /// Time this ticket was created
            /// </summary>
            private DateTime resetTime = DateTime.Now;

            /// <summary>
            /// Time this update pass was started, used to limit update time overall
            /// </summary>
            internal DateTime StartTime { get; private set; }

            /// <summary>
            /// Number of updates since the timings were reset
            /// </summary>
            private int updateCount = 1;

            /// <summary>
            /// Timings for the update, tracked for profiling
            /// </summary>
            private readonly Dictionary<string, double> timings = new Dictionary<string, double>();

            /// <summary>
            /// Section timings as string, for debug display
            /// </summary>
            internal string Timings { get; private set; }

            /// <summary>
            /// Number of objects which attempted to spawn this tick and could not locate their
            /// desired parent.
            /// </summary>
            internal int OrphanedObjects { get; private set; }

            /// <summary>
            /// Number of lights which are active (have spawned) and ticked their components (eg.
            /// DutyCycle updates)
            /// </summary>
            internal int ActiveLights { get; private set; }

            /// <summary>
            /// Number of lights which are active (have spawned) and are not currently culled
            /// DutyCycle updates)
            /// </summary>
            internal int VisibleLights { get; private set; }

            /// <summary>
            /// Number of meshes which are active (have spawned) and ticked their components
            /// </summary>
            internal int ActiveMeshes { get; private set; }

            /// <summary>
            /// Number of meshes which are active (have spawned) and are not currently culled
            /// </summary>
            internal int VisibleMeshes { get; private set; }

            /// <summary>
            /// Number of decorations which are active and ticking
            /// </summary>
            internal int ActiveDecorations { get; private set; }

            /// <summary>
            /// Number of decorations which are active and are not currently culled
            /// </summary>
            internal int VisibleDecorations { get; private set; }

            /// <summary>
            /// Number of updates remaining in this ticket
            /// </summary>
            internal int UpdatesRemaining { get; private set; }

            /// <summary>
            /// Whether the number of updates remaining in this ticket is greater than zero, which
            /// allows an object to perform an expensive operation
            /// </summary>
            internal bool HasUpdatesRemaining
            {
                get
                {
                    TimeSpan elapsed = DateTime.Now - this.StartTime;
                    return this.UpdatesRemaining > 0 && elapsed.TotalMilliseconds < SpawnerController.MAX_UPDATE_TIME_MS;
                }
            }

            /// <summary>
            /// Actual operations carried out (may be less than available updates since some
            /// operations, such as spawning meshes, are expensive and cost more than one ticket
            /// </summary>
            internal int Marks { get; private set; }

            /// <summary>
            /// The player location in XZ absolute world coordinates for this update
            /// </summary>
            internal Vector2 PlayerLocation
            {
                get; private set;
            }

            /// <summary>
            /// Begin a new update pass at the specified player location with the specified number
            /// of available updates
            /// </summary>
            /// <param name="playerLocation"></param>
            /// <param name="updates"></param>
            internal void Begin(Vector2 playerLocation, int updates)
            {
                this.StartTime = DateTime.Now;
                this.PlayerLocation = playerLocation;
                this.UpdatesRemaining = updates;
                this.Marks = 0;

                this.OrphanedObjects = 0;
                this.ActiveLights = 0;
                this.VisibleLights = 0;
                this.ActiveMeshes = 0;
                this.VisibleMeshes = 0;
                this.ActiveDecorations = 0;
                this.VisibleDecorations = 0;

                this.updateCount++;

                double sinceReset = (DateTime.Now - this.resetTime).TotalSeconds;
                if (sinceReset >= 1.0)
                {
                    if (DebugOverlay.Active)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendFormat("Updates: {0}", this.updateCount);
                        foreach (KeyValuePair<string, double> timing in this.timings)
                        {
                            sb.AppendFormat("\n{0,-20} {1,16:F4}ms ({2,8:F4}ms avg.)", timing.Key, timing.Value, (timing.Value / this.updateCount));
                        }
                        this.Timings = sb.ToString();
                    }
                    this.timings.Clear();
                    this.resetTime = DateTime.Now;
                    this.updateCount = 1;
                }
            }

            /// <summary>
            /// Stamp the ticket, remove the specified number of updates from the ticket
            /// </summary>
            /// <param name="cost"></param>
            internal void Mark(int cost = 1)
            {
                this.Marks++;
                this.UpdatesRemaining -= cost;
            }

            /// <summary>
            /// Register an orphan
            /// </summary>
            internal void Drift()
            {
                this.OrphanedObjects++;
            }

            /// <summary>
            /// Register a light tick
            /// </summary>
            internal void TickedLight(bool visible)
            {
                this.ActiveLights++;
                if (visible)
                {
                    this.VisibleLights++;
                }
            }

            /// <summary>
            /// Register a mesh tick
            /// </summary>
            internal void TickedMesh(bool visible)
            {
                this.ActiveMeshes++;
                if (visible)
                {
                    this.VisibleMeshes++;
                }
            }

            /// <summary>
            /// Register a decoration tick
            /// </summary>
            internal void TickedDecoration(bool visible)
            {
                this.ActiveDecorations++;
                if (visible)
                {
                    this.VisibleDecorations++;
                }
            }

            internal TimingSection Begin(string name)
            {
                return new TimingSection(name, this.End);
            }

            private void End(string name, double elapsed)
            {
                double existingValue = this.timings.ContainsKey(name) ? this.timings[name] : 0.0;
                this.timings[name] = existingValue + elapsed;
            }

            public override string ToString()
            {
                return $"Scanned: {this.Marks} (remaining {this.UpdatesRemaining}) Lights: {this.VisibleLights}/{this.ActiveLights} Meshes: {this.VisibleMeshes}/{this.ActiveMeshes} Decorations: {this.VisibleDecorations}/{this.ActiveDecorations} Orphans: {this.OrphanedObjects}";
            }
        }

        /// <summary>
        /// An undo buffer entry, used to keep track of operations (create and delete) which can be
        /// undone by the user
        /// </summary>
        internal class UndoBufferEntry
        {
            internal enum Action
            {
                Created,
                Deleted
            }

            /// <summary>
            /// The undo buffer step ID. Since multiple operations may happen as part of the same
            /// undo action (eg. creating a mesh with lights), the step ID for operations that occur
            /// together will be the same.
            /// </summary>
            internal readonly int stepId;

            /// <summary>
            /// The spawner which was created or deleted
            /// </summary>
            internal readonly Spawner spawner;

            /// <summary>
            /// The action type for this entry
            /// </summary>
            internal readonly Action action;

            internal UndoBufferEntry(int stepId, Spawner spawner, Action action)
            {
                this.stepId = stepId;
                this.spawner = spawner;
                this.action = action;
            }

            /// <summary>
            /// Perform the undo operation
            /// </summary>
            public void Undo()
            {
                switch (this.action)
                {
                    case Action.Created:
                        this.spawner.Delete();
                        break;
                    case Action.Deleted:
                        this.spawner.Undelete();
                        break;
                }
            }
        }

        /// <summary>
        /// Stats tracker, ticks every 10 seconds to report objects spawned or destroyed in the log
        /// </summary>
        internal class Stats
        {
            private static readonly DebugOverlay.Section debugSpawned = DebugOverlay.AddSection("Spawned", Color.cyan);
            private static readonly DebugOverlay.Section debugDestroyed = DebugOverlay.AddSection("Destroyed", Color.red);

            internal DateTime markTime = DateTime.Now;

            internal int SpawnedLights { get; private set; }
            internal int DestroyedLights { get; private set; }

            internal int SpawnedMeshes { get; private set; }
            internal int DestroyedMeshes { get; private set; }

            internal int SpawnedDecorations { get; private set; }
            internal int DestroyedDecorations { get; private set; }

            internal void NotifySpawned(Spawner spawner)
            {
                Stats.debugSpawned.AddMessage(spawner.Name + " on " + (string.IsNullOrEmpty(spawner.ParentPath) ? "<GLOBAL>" : spawner.ParentPath), 0.5);

                if (spawner is LightSpawner)
                {
                    this.SpawnedLights++;
                }
                else if (spawner is MeshSpawner)
                {
                    this.SpawnedMeshes++;
                }
                else if (spawner is DecorationSpawner)
                {
                    this.SpawnedDecorations++;
                }
            }

            internal void NotifyDestroyed(Spawner spawner)
            {
                Stats.debugDestroyed.AddMessage(spawner.Name, 0.5);

                if (spawner is LightSpawner)
                {
                    this.DestroyedLights++;
                }
                else if (spawner is MeshSpawner)
                {
                    this.DestroyedMeshes++;
                }
                else if (spawner is DecorationSpawner)
                {
                    this.DestroyedDecorations++;
                }
            }

            internal void Tick()
            {
                if ((DateTime.Now - this.markTime).TotalMilliseconds > 10000)
                {
                    this.markTime = DateTime.Now;
                    if (this.SpawnedLights > 0 || this.SpawnedMeshes > 0 || this.DestroyedLights > 0 || this.DestroyedMeshes > 0)
                    {
                        LightSniper.Logger.Info("Lights(Spawned:{0} Destroyed:{1}) Meshes(Spawned:{2} Destroyed:{3}) Decorations(Spawned:{4} Destroyed:{5})",
                            this.SpawnedLights, this.DestroyedLights, this.SpawnedMeshes, this.DestroyedMeshes, this.SpawnedDecorations, this.DestroyedDecorations);
                    }

                    this.SpawnedLights = 0;
                    this.DestroyedLights = 0;
                    this.SpawnedMeshes = 0;
                    this.DestroyedMeshes = 0;
                    this.SpawnedDecorations = 0;
                    this.DestroyedDecorations = 0;
                }
            }
        }

        /// <summary>
        /// Maximum duration allowed for a single update pass
        /// </summary>
        internal const double MAX_UPDATE_TIME_MS = 25.0;

        /// <summary>
        /// Time to trigger auto-save of regions
        /// </summary>
        internal const long AUTOSAVE_INTERVAL_SECONDS = 60;

        // Debug overlay sections
        private static readonly DebugOverlay.Section debugTime = DebugOverlay.AddSection("Time");
        private static readonly DebugOverlay.Section debugPerformance = DebugOverlay.AddSection("Performance");
        private static readonly DebugOverlay.Section debugCamera = DebugOverlay.AddSection("FreeCam", Color.magenta);
        private static readonly DebugOverlay.Section debugRegion = DebugOverlay.AddSection("Region");
        private static readonly DebugOverlay.Section debugGroup = DebugOverlay.AddSection("Group");
        private static readonly DebugOverlay.Section debugUpdates = DebugOverlay.AddSection("Updated");
        private static readonly DebugOverlay.Section debugTick = DebugOverlay.AddSection("Tick");
        // private static readonly DebugOverlay.Section debugErrored   = DebugOverlay.AddSection("Errored", new Color(1.0F, 0.4F, 0.0F));

        /// <summary>
        /// Stat tracker
        /// </summary>
        private readonly Stats stats = new Stats();

        /// <summary>
        /// The current real player camera, unlike PlayerManager.PlayerCamera, this keeps track of
        /// the player camera when FlyCam is active as well
        /// </summary>
        internal static Camera PlayerCamera { get; private set; }

        /// <summary>
        /// The current real player camera transform, unlike PlayerManager.PlayerCamera.transform,
        /// this keeps track of the player camera when FlyCam is active as well
        /// </summary>
        internal static Transform PlayerCameraTransform { get; private set; }

        /// <summary>
        /// Whether the FlyCam is in use
        /// </summary>
        internal static bool IsFreeCamActive { get; private set; }

        /// <summary>
        /// The current world time according to the active time source
        /// </summary>
        internal static DateTime CurrentTime { get; private set; }

        /// <summary>
        /// Current second since midnight in world time
        /// </summary>
        internal static int CurrentSecond { get; private set; }

        internal static bool MarkOrphans { get; set; }

        private static DateTime killOrphanTime;

        internal static bool KillOrphans
        {
            get
            {
                return DateTime.Now < SpawnerController.killOrphanTime;
            }

            set
            {
                SpawnerController.killOrphanTime = value ? DateTime.Now + new TimeSpan(0, 0, 10) : DateTime.MinValue;
            }
        }

        private static DateTime freeOrphanTime;

        internal static bool FreeOrphans
        {
            get
            {
                return DateTime.Now < SpawnerController.freeOrphanTime;
            }

            set
            {
                SpawnerController.freeOrphanTime = value ? DateTime.Now + new TimeSpan(0, 0, 10) : DateTime.MinValue;
            }
        }

        /// <summary>
        /// The currently active group for creating new objects
        /// </summary>
        internal string CurrentGroup
        {
            get => LightSniper.Settings.CurrentGroup;
        }

        /// <summary>
        /// The base region storage directory
        /// </summary>
        internal static readonly string RegionsDir = Path.Combine(LightSniper.Path, "Regions");

        /// <summary>
        /// True if sprites should be globally enabled, for example when the LightSniper mode in the
        /// comms radio is active, shows the location of all lights, even those which are currently
        /// off
        /// </summary>
        internal bool ShowSprites { get; set; }

        /// <summary>
        /// Region storage, all regions indexed by YardID
        /// </summary>
        private readonly IDictionary<string, Region> regions = new Dictionary<string, Region>();

        /// <summary>
        /// Region override, used when the user wishes to store created lights in a specific region
        /// instead of the nearest region. For example to store crreated lights in MF instead of
        /// MFMB
        /// </summary>
        private Region forceRegion;

        /// <summary>
        /// The undo buffer
        /// </summary>
        private readonly List<UndoBufferEntry> undoBuffer = new List<UndoBufferEntry>();

        /// <summary>
        /// The current undo buffer step ID, undo buffer steps are assigned an ID so that multiple
        /// steps which should be undone together can share a step id
        /// </summary>
        private int undoBufferStep;

        /// <summary>
        /// Time the last tick occurred
        /// </summary>
        private DateTime lastTickTime = DateTime.Now;

        /// <summary>
        /// Time for the next allowed (expensive) update. If this value is in the future then only
        /// basic tick operations are allowed, no expensive operations such as GameObject.Find or
        /// mesh spawning are allowed. This value is advanced when an update ticket is depleted, and
        /// will be set to the current time + SpawnerController::BACKOFF_TIME_MS.
        /// </summary>
        private DateTime nextUpdate = DateTime.Now;

        /// <summary>
        /// Time last auto-save was triggered
        /// </summary>
        private DateTime autoSaveTime = DateTime.Now;

        /// <summary>
        /// Update ticket
        /// </summary>
        private readonly UpdateTicket ticket = new UpdateTicket();

        private double frameRate = 0.0;
        private int frameCounter;
        private DateTime frameMarker = DateTime.Now;

        public SpawnerController()
        {
            Directory.CreateDirectory(SpawnerController.RegionsDir);
        }

        [UsedImplicitly]
        private void Start()
        {
            // Tile areas cover all areas not close to a station or other landmark
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    this.CreateRegion(new Rect(x * 1024.0F, z * 1024.0F, 1024.0F, 1024.0F), $"TILE_X{x:00}_Z{z:00}", "WILDERNESS");
                }
            }

            // Additional non-station areas
            this.CreateRegion(new Vector2(8444.0F, 7950.0F),  "TUTORIAL");
            this.CreateRegion(new Vector2(1591.0F, 12222.0F), "OLD_BOBS_GARAGE");
            this.CreateRegion(new Vector2(4396.0F, 2463.0F),  "REGINALDS_GARAGE");
            this.CreateRegion(new Vector2(5709.0F, 7620.0F),  "PLAYER_HOUSE");
            this.CreateRegion(new Vector2(7152.0F, 9595.0F),  "RADAR_DOME");
            this.CreateRegion(new Vector2(9785.0F, 3860.0F),  "RADIO_TOWER");
            this.CreateRegion(new Vector2(12318.0F, 8448.0F), "CASTLE_RUINS");

            foreach (StationController station in StationController.allStations)
            {
                Region region = this.CreateRegion(station.transform.parent.AsMapLocation(), station.stationInfo.YardID);
                region.Anchor = station.transform;
            }

            if (LightSniper.Settings.CurrentGroup != Region.DEFAULT_GROUP_NAME)
            {
                this.BeginGroup(LightSniper.Settings.CurrentGroup);
            }
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            foreach (Region region in this.regions.Values)
            {
                region.Destroy();
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            if (PlayerManager.PlayerCamera.enabled)     // In first person mode
            {
                SpawnerController.PlayerCamera = PlayerManager.PlayerCamera;
                SpawnerController.PlayerCameraTransform = PlayerManager.PlayerCamera.transform;
                SpawnerController.IsFreeCamActive = false;
                SpawnerController.debugCamera.Enabled = false;
            }
            else                                        // In third person mode (FlyCam)
            {
                if (!SpawnerController.IsFreeCamActive)
                {
                    SpawnerController.IsFreeCamActive = true;
                    FlyCam flyCam = Object.FindObjectOfType<FlyCam>();
                    if (flyCam != null)
                    {
                        SpawnerController.PlayerCamera = flyCam.gameObject.GetComponent<Camera>();
                        SpawnerController.PlayerCameraTransform = flyCam.transform;
                        SpawnerController.debugCamera.Enabled = true;
                    }
                }

                if (DebugOverlay.Active)
                {
                    Vector3 pos = SpawnerController.PlayerCameraTransform.position - WorldMover.currentMove;
                    SpawnerController.debugCamera.Text = $"X: {pos.x:F3} Y: {pos.y:F3} Z: {pos.z:F3}";
                }
            }

            this.frameCounter++;
            TimeSpan elapsed = DateTime.Now - this.frameMarker;
            if (elapsed.TotalMilliseconds >= 1000.0)
            {
                this.frameRate = this.frameCounter / elapsed.TotalSeconds;
                this.frameMarker = DateTime.Now;
                this.frameCounter = 0;
            }

            this.Tick();

            if (DebugOverlay.Active)
            {
                string period = SpawnerController.CurrentSecond < (LightSniper.Settings.DawnTime * 3600) || SpawnerController.CurrentSecond > (LightSniper.Settings.DuskTime * 3600) ? "Night" : "Day";
                SpawnerController.debugTime.Text = SpawnerController.CurrentTime.ToLongTimeString() + " (" + SpawnerController.CurrentSecond + ") (" + period + ")";
                SpawnerController.debugTick.Text = this.ticket.ToString();
            }
        }

        [UsedImplicitly]
        private void FixedUpdate()
        {
            this.Tick();
        }

        private void Tick()
        {
            if ((DateTime.Now - this.lastTickTime).TotalSeconds < 1.0 / LightSniper.Settings.DesiredTickRate)
            {
                return;
            }
            this.lastTickTime = DateTime.Now;

            bool allowUpdate = DateTime.Now > this.nextUpdate;
            Vector2 playerPosition = PlayerManager.GetWorldAbsolutePlayerPosition().AsMapLocation();
            this.ticket.Begin(playerPosition, allowUpdate ? LightSniper.Settings.AllowedUpdates : 0);

            if (DebugOverlay.Active)
            {
                SpawnerController.debugRegion.Text = this.FindRegion(playerPosition).YardID + (this.forceRegion != null ? " Override: " + this.forceRegion.YardID : "");
                SpawnerController.debugGroup.Text = LightSniper.Settings.CurrentGroup;
                SpawnerController.debugUpdates.Text = "";
            }

            SpawnerController.CurrentTime = TimeSource.GetCurrentTime();
            SpawnerController.CurrentSecond = (((SpawnerController.CurrentTime.Hour * 60) + SpawnerController.CurrentTime.Minute) * 60) + SpawnerController.CurrentTime.Second;


            foreach (Region region in this.regions.Values)
            {
                region.Tick(this.ticket);
            }

            if (this.ticket.UpdatesRemaining <= 0 && allowUpdate)
            {
                this.nextUpdate = DateTime.Now + TimeSpan.FromMilliseconds(LightSniper.Settings.BackoffTimeMs);
            }

            this.stats.Tick();

            if ((DateTime.Now - this.autoSaveTime).TotalSeconds > SpawnerController.AUTOSAVE_INTERVAL_SECONDS)
            {
                UpdateTicket.TimingSection section = this.ticket.Begin("autosave");
                this.autoSaveTime = DateTime.Now;
                foreach (Region region in this.regions.Values)
                {
                    region.AutoSave();
                }
                section.End();
            }

            if (DebugOverlay.Active)
            {
                double updateTime = (DateTime.Now - this.ticket.StartTime).TotalMilliseconds;
                string cullingState = LightSniper.Settings.EnableDistanceCulling && !SpawnerController.IsFreeCamActive ? "Enabled " + LightSniper.Settings.DistanceCullingRadius + "m" : "Disabled";
                SpawnerController.debugPerformance.Text = $"FPS: {this.frameRate:F1} Update: {updateTime:F3}ms Culling: {cullingState} {(SpawnerController.IsFreeCamActive ? "(FreeCam) " : "")}{this.ticket.Timings}";
                if (updateTime > (LoadingScreenManager.IsLoading ? 500.0F : 50.0F))
                {
                    LightSniper.Logger.Error("LightSniper update took {0:F3}ms!", updateTime);
                }
            }
        }

        /// <summary>
        /// Find the region for the specified raycast hit
        /// </summary>
        /// <param name="hitInfo">Raycast hit info</param>
        /// <returns>Hit region, forced region or fallback (wilderness) region</returns>
        internal Region FindRegion(RaycastHit hitInfo)
        {
            return this.forceRegion ?? this.FindRegion((hitInfo.point - WorldMover.currentMove).AsMapLocation());
        }

        /// <summary>
        /// Find the region for the specified location in absolute XZ world coordinates
        /// </summary>
        /// <param name="worldLocation">World location to check</param>
        /// <returns>Closest region at the specified location, forced region or fallback
        /// (wilderness) region</returns>
        internal Region FindRegion(Vector2 worldLocation)
        {
            Region nearestRegion = this.GetFallbackRegion(worldLocation);

            float minDistanceSq = Region.RADIUS_SQ;
            foreach (Region region in this.regions.Values)
            {
                if (region.WorldLocation == Vector2.zero)
                {
                    continue;
                }

                float distanceSq = (region.WorldLocation - worldLocation).sqrMagnitude;
                if (distanceSq < minDistanceSq)
                {
                    nearestRegion = region;
                    minDistanceSq = distanceSq;
                }
            }
            return nearestRegion;
        }

        /// <summary>
        /// Get the fallback (wilderness) region at the specified world location
        /// </summary>
        /// <param name="worldLocation">World location to check</param>
        /// <returns>Relevant wilderness tile region based on supplied world coordinates</returns>
        private Region GetFallbackRegion(Vector2 worldLocation)
        {
            int xTile = Math.Min(Math.Max((int)Math.Floor(worldLocation.x) >> 10, 0), 15);
            int zTile = Math.Min(Math.Max((int)Math.Floor(worldLocation.y) >> 10, 0), 15);
            return this.regions[$"TILE_X{xTile:00}_Z{zTile:00}"];
        }

        internal Region GetRegion(string yardId)
        {
            if (!this.regions.ContainsKey(yardId))
            {
                return this.CreateRegion(Vector2.zero, yardId);
            }

            return this.regions[yardId];
        }

        private Region CreateRegion(Vector2 worldLocation, string yardId, string yardGroup = null)
        {
            Region newRegion = new Region(this, worldLocation, yardId, yardGroup);
            this.regions.Add(yardId, newRegion);
            return newRegion;
        }

        private Region CreateRegion(Rect area, string yardId, string yardGroup = null)
        {
            Region newRegion = new Region(this, area, yardId, yardGroup);
            this.regions.Add(yardId, newRegion);
            return newRegion;
        }

        /// <summary>
        /// Set all regions to use the specified group
        /// </summary>
        /// <param name="groupName">Group name to use</param>
        internal void BeginGroup(string groupName)
        {
            LightSniper.Settings.CurrentGroup = groupName;
            foreach (Region region in this.regions.Values)
            {
                region.BeginGroup(groupName);
            }
            Terminal.Log(TerminalLogType.ShellMessage, $"Selected group {groupName}");
        }

        /// <summary>
        /// Set all regions back to the default (user) group
        /// </summary>
        internal void EndGroup()
        {
            foreach (Region region in this.regions.Values)
            {
                region.EndGroup();
            }
            LightSniper.Settings.CurrentGroup = Region.DEFAULT_GROUP_NAME;
        }

        /// <summary>
        /// Enable or disable the specified group in all regions
        /// </summary>
        /// <param name="yardId">Region yard id or * for all regions</param>
        /// <param name="groupName">Group name</param>
        /// <param name="enabled">Whether to enable or disable the group</param>
        internal void EnableGroup(string yardId, string groupName, bool enabled)
        {
            foreach (Region region in this.regions.Values)
            {
                if (yardId == "*" || yardId.Equals(region.YardID, StringComparison.InvariantCultureIgnoreCase))
                {
                    region.EnableGroup(groupName, enabled);
                }
            }

            string action = enabled ? "Enabled" : "Disabled";
            Terminal.Log(TerminalLogType.Message, groupName == "*" ? $"{action} all groups" : $"{action} group {groupName}");
        }

        /// <summary>
        /// Unpack the default regions if they don't exist already
        /// </summary>
        /// <returns></returns>
        internal bool InstallDefaultPack()
        {
            if (PackLoader.UnpackBuiltin())
            {
                foreach (Region region in this.regions.Values)
                {
                    region.Reload();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Forces the current region to the value specified by YardID
        /// </summary>
        /// <param name="yardId">Region yard id to select</param>
        /// <returns></returns>
        internal string SetRegion(string yardId)
        {
            if (string.IsNullOrEmpty(yardId))
            {
                this.forceRegion = null;
                return "<AUTO>";
            }

            foreach (Region region in this.regions.Values)
            {
                if (yardId.Equals(region.YardID, StringComparison.InvariantCultureIgnoreCase))
                {
                    this.forceRegion = region;
                    return region.YardID;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the light currently hit or nearest light in specified range
        /// </summary>
        /// <param name="signalOrigin">Raycast signal origin</param>
        /// <param name="scanRange">Range to scan near cursor</param>
        /// <param name="nearestLight">Nearest light to cursor, can be null</param>
        /// <param name="hitMesh">Mesh which was hit, can be null</param>
        internal void GetLightUnderCursor(Transform signalOrigin, float scanRange, out LightSpawner nearestLight, out MeshSpawner hitMesh)
        {
            nearestLight = null;
            hitMesh = null;

            if (scanRange <= 0.0F || !SpawnerController.CastRay(signalOrigin, out RaycastHit hitInfo))
            {
                return;
            }

            if (hitInfo.transform?.gameObject != null)
            {
                LightComponent light = hitInfo.transform.gameObject.GetComponent<LightComponent>();
                if (light != null && light.Spawner.Editable)
                {
                    nearestLight = light.Spawner;
                    return;
                }
            }
        
            float distanceToNearest = 0.25F * scanRange;
            foreach (Region region in this.regions.Values)
            {
                region.GetNearestLight(hitInfo, ref nearestLight, ref distanceToNearest);
            }

            if (nearestLight != null)
            {
                return;
            }

            MeshComponent meshBehaviour = hitInfo.transform?.gameObject?.GetComponentInParent<MeshComponent>();
            if (meshBehaviour != null && meshBehaviour.Spawner.Editable)
            {
                hitMesh = meshBehaviour.Spawner;
            }
        }

        /// <summary>
        /// Paint the specified light with the supplied properties
        /// </summary>
        /// <param name="light">Light to paint</param>
        /// <param name="properties">Properties to apply</param>
        internal void Paint(LightSpawner light, LightProperties properties)
        {
            if (light != null && properties != null)
            {
                light.Paint(properties.Clone());
            }
        }

        /// <summary>
        /// Paint all lights attached to the specified mesh with the supplied properties
        /// </summary>
        /// <param name="mesh">Mesh to paint attached lights</param>
        /// <param name="properties">Paramters to apply</param>
        internal void Paint(MeshSpawner mesh, LightProperties properties)
        {
            mesh?.Paint(properties);
        }

        /// <summary>
        /// delete the specfied light and add undo buffer entry
        /// </summary>
        /// <param name="light"></param>
        internal void Delete(LightSpawner light)
        {
            if (light != null)
            {
                if (light.Delete())
                {
                    this.undoBuffer.Add(new UndoBufferEntry(this.undoBufferStep++, light, UndoBufferEntry.Action.Deleted));
                }
            }
        }

        /// <summary>
        /// Delete the specified mesh and add undo buffer entry
        /// </summary>
        /// <param name="mesh"></param>
        internal void Delete(MeshSpawner mesh)
        {
            if (mesh != null)
            {
                if (mesh.Delete())
                {
                    this.undoBuffer.Add(new UndoBufferEntry(this.undoBufferStep++, mesh, UndoBufferEntry.Action.Deleted));
                }
            }
        }

        /// <summary>
        /// Undo the next available operation to undo
        /// </summary>
        internal void Undo()
        {
            if (this.undoBuffer.Count < 1)
            {
                return;
            }

            for (int index = this.undoBuffer.Count - 1; index > -1; index--)
            {
                UndoBufferEntry entry = this.undoBuffer[index];
                this.undoBuffer.RemoveAt(index);
                entry.Undo();
                if (index == 0 || this.undoBuffer[index - 1].stepId != entry.stepId)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Snipe a light or mesh based on available information in the supplied template
        /// </summary>
        /// <param name="signalOrigin"></param>
        /// <param name="template"></param>
        /// <param name="randomLightIndex"></param>
        internal ActionResult Snipe(Transform signalOrigin, LightTemplate template, int randomLightIndex)
        {
            if (template.HasMeshes)
            {
                return this.SnipeMesh(signalOrigin, template);
            }
            else if (template.HasLights)
            {
                return this.SnipeLight(signalOrigin, template.Lights[randomLightIndex], template.SnipeOffset);
            }

            return new ActionResult(false, "Selected template is empty");
        }

        /// <summary>
        /// Snipe a decoration based on the supplied template
        /// </summary>
        /// <param name="signalOrigin"></param>
        /// <param name="template"></param>
        internal ActionResult Snipe(Transform signalOrigin, DecorationTemplate template)
        {
            if (!SpawnerController.CastRay(signalOrigin, out RaycastHit hitInfo))
            {
                return new ActionResult(false, "No target");
            }

            if (hitInfo.transform == null || !hitInfo.transform.gameObject.HasUniquePath())
            {
                return new ActionResult(false, "Invalid target");
            }

            TemplateMatch templateMatch = template.MatchRecursive(hitInfo.transform.gameObject);
            if (templateMatch == null)
            {
                // No available templates for this target
                return new ActionResult(false, "Selected template cannot be applied to target", Color.yellow);
            }

            try
            {
                DecorationsComponent decorations = templateMatch.gameObject.GetOrCreate<DecorationsComponent>();
                DecorationsComponent.Match match = decorations.Has(template.Id);

                // We found an existing decoration in the same group
                if (match != DecorationsComponent.Match.None)
                {
                    DecorationSpawner existingDecoration = decorations.Find(template.Id);
                    if (existingDecoration != null && existingDecoration.Delete())
                    {
                        // And removed it
                        this.undoBuffer.Add(new UndoBufferEntry(this.undoBufferStep, existingDecoration, UndoBufferEntry.Action.Deleted));
                    }
                    else
                    {
                        // But failed to remove it
                        return new ActionResult(false, "Could not remove existing decoration");
                    }

                    // the existing decoration is actually the one we had selected, so the user
                    // was trying to remove it, therefore our work here is done
                    if (match == DecorationsComponent.Match.Exact)
                    {
                        return ActionResult.SUCCESS;
                    }
                }

                // Readonly templates are from packs, so try to extract the asset bundle
                // locally so we can keep using the assets if the user removes the pack
                if (template.ReadOnly)
                {
                    template.ExtractAssetBundles();
                }

                Region region = this.FindRegion(hitInfo);
                DecorationSpawner decoration = region.CreateDecoration(templateMatch.gameObject.GetObjectPath(), templateMatch.template.Id, templateMatch.target.Properties, templateMatch.target.DutyCycle);
                this.undoBuffer.Add(new UndoBufferEntry(this.undoBufferStep++, decoration, UndoBufferEntry.Action.Created));
                return ActionResult.SUCCESS;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
                return new ActionResult(false, "ERROR:\n" + e.Message);
            }
        }

        /// <summary>
        /// Snipe a mesh and attached lights
        /// </summary>
        /// <param name="signalOrigin"></param>
        /// <param name="template"></param>
        internal ActionResult SnipeMesh(Transform signalOrigin, LightTemplate template)
        {
            if (template == null || !SpawnerController.CastRay(signalOrigin, out RaycastHit hitInfo))
            {
                return new ActionResult(false, "No target");
            }

            try
            {
                if (template.ReadOnly)
                {
                    template.ExtractAssetBundles();
                }

                Region region = this.FindRegion(hitInfo);
                Transform transformParent = SpawnerController.FindParentTransform(hitInfo, region.Anchor, region.CurrentGroup.Name);
                string parentPath = transformParent?.GetObjectPath() ?? "";
                Vector3 localPosition = transformParent?.InverseTransformPoint(hitInfo.point) ?? hitInfo.point.AsWorldCoordinate();

                LightSniper.Logger.Info("Sniping mesh with parent: " + (transformParent?.GetObjectPath() ?? "GLOBAL") + " at " + localPosition);

                MeshOrientation orientation = hitInfo.GetMeshOrientation();
                Quaternion rotation;
                if (orientation == MeshOrientation.Wall)
                {
                    orientation = MeshOrientation.Wall;
                    rotation = Quaternion.LookRotation(Vector3.up, hitInfo.normal);
                    if (LightSniper.Settings.EnableEnhancedPlacer)
                    {
                        Quaternion opposite = Quaternion.LookRotation(Vector3.down, hitInfo.normal);
                        if (LightSniper.Settings.Perpendicular.Pressed())
                        {
                            float rotationAmount = LightSniper.Settings.Shift.Pressed() ? 90.0F : -90.0F;
                            rotation = Quaternion.RotateTowards(rotation, opposite, rotationAmount);
                        }
                        else if (LightSniper.Settings.Shift.Pressed())
                        {
                            rotation = opposite;
                        }
                    }
                }
                else
                {
                    Vector3 floorientation = (template.GetMesh(orientation).Alignment == MeshTemplate.AlignmentType.AlignToNormal ? hitInfo.normal : Vector3.up);
                    rotation = Quaternion.LookRotation(LightSniper.ModifiedPlayerDirection, orientation == MeshOrientation.Floor ? floorientation : Vector3.down);
                }

                MeshTemplate meshTemplate = template.GetMesh(orientation);

                GameObject prefab = AssetLoader.Meshes.Load<GameObject>(meshTemplate.Properties.AssetBundleName, meshTemplate.Properties.AssetName);
                if (prefab == null)
                {
                    return new ActionResult(true, $"Specified prefab:\n{meshTemplate.Properties.AssetName}\ncould not be loaded", Color.yellow);
                }

                MeshSpawner mesh = region.CreateMesh(parentPath, localPosition, rotation, meshTemplate.Properties);
                this.undoBuffer.Add(new UndoBufferEntry(this.undoBufferStep, mesh, UndoBufferEntry.Action.Created));

                List<Vector3> lightOffsets = new List<Vector3>();
                foreach (Transform child in prefab.transform)
                {
                    if (child.name.StartsWith("LightOffset("))
                    {
                        lightOffsets.Add(child.localPosition);
                    }
                }

                int numOffsets = lightOffsets.Count;
                if (numOffsets == 0)
                {
                    return ActionResult.SUCCESS;
                }

                int numLights = template.Lights.Count;
                for (int offsetIndex = 0; offsetIndex < numOffsets; offsetIndex++)
                {
                    Vector3 lightOffset = lightOffsets[offsetIndex];
                    int lightIndex = numOffsets == numLights ? offsetIndex : offsetIndex % numLights;

                    Quaternion lightRotation = Quaternion.LookRotation(hitInfo.normal, Vector3.up);
                    LightSpawner light = region.CreateLight(mesh.LocalPath, lightOffset, lightRotation, template.Lights[lightIndex].Clone());
                    this.undoBuffer.Add(new UndoBufferEntry(this.undoBufferStep, light, UndoBufferEntry.Action.Created));

                    if (mesh.IsGlobal)
                    {
                        light.CullDistance = mesh.CullDistance;
                    }
                }

                this.undoBufferStep++;
                return ActionResult.SUCCESS;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
                return new ActionResult(false, "ERROR:\n" + e.Message);
            }
        }

        /// <summary>
        /// Snipe a light
        /// </summary>
        /// <param name="signalOrigin"></param>
        /// <param name="lightProperties"></param>
        /// <param name="snipeOffset"></param>
        internal ActionResult SnipeLight(Transform signalOrigin, LightProperties lightProperties, float snipeOffset)
        {
            if (lightProperties == null || !SpawnerController.CastRay(signalOrigin, out RaycastHit hitInfo))
            {
                return new ActionResult(false, "No Target Found");
            }

            try
            {
                Region region = this.FindRegion(hitInfo);

                Transform transformParent = SpawnerController.FindParentTransform(hitInfo, region.Anchor, region.CurrentGroup.Name);
                string parentPath = transformParent?.GetObjectPath() ?? "";
                Vector3 transformPosition = hitInfo.point + (hitInfo.normal * snipeOffset);
                Vector3 localPosition = transformParent?.InverseTransformPoint(transformPosition) ?? transformPosition.AsWorldCoordinate();

                LightSniper.Logger.Info("Sniping light with parent: " + (transformParent?.GetObjectPath() ?? "GLOBAL") + " at " + localPosition);

                Quaternion lightRotation = Quaternion.LookRotation(hitInfo.normal, Vector3.up);
                LightSpawner light = region.CreateLight(parentPath, localPosition, lightRotation, lightProperties.Clone());
                this.undoBuffer.Add(new UndoBufferEntry(this.undoBufferStep++, light, UndoBufferEntry.Action.Created));
                return ActionResult.SUCCESS;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
                return new ActionResult(false, "ERROR:\n" + e.Message);
            }
        }

        /// <summary>
        /// Raycast from player
        /// </summary>
        /// <param name="signalOrigin"></param>
        /// <param name="hitInfo"></param>
        /// <returns></returns>
        internal static bool CastRay(Transform signalOrigin, out RaycastHit hitInfo)
        {
            if (SpawnerController.IsFreeCamActive)
            {
                signalOrigin = SpawnerController.PlayerCameraTransform;
            }

            Ray ray = new Ray(signalOrigin.position, signalOrigin.forward);
            if (!Physics.Raycast(ray, out hitInfo, 2000F))
            {
                return false;
            }

            if (hitInfo.collider.tag == "Player")
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Find a suitable parent transform based on the supplied raycast hit. Attempts to find a
        /// transform in the hierarchy of the hit object which is uniquely identifyable by its
        /// transformation path (that is, GameObject.Find returns the same object as the one hit)
        /// which has uniform scale, and is a suitable anchor based on our requirements (eg. isn't
        /// the player, has the SceneSplitManager somwhere in its hierarchy). If a suitable object
        /// isn't found in the hierarchy of the hit object, an overlap sphere is used to find nearby 
        /// objects which may be suitable candidates. If no suitable nearby transform is found then
        /// the specified fallback transform is returned.
        /// </summary>
        /// <param name="hitInfo">Raycast hit info</param>
        /// <param name="fallbackTransform">Fallback transform to use if no suitable transforms can
        /// be found</param>
        /// <param name="allowedMeshGroup">Name of allowed group for mesh parents if our own spawned
        /// objects are allowed to be returned, which is fine for lights but we don't really want
        /// meshes to be parented off eachother, and we only want to parent off objects in the same
        /// group, otherwise we could end up parented to an object which is disabled/removed</param>
        /// <returns></returns>
        private static Transform FindParentTransform(RaycastHit hitInfo, Transform fallbackTransform, string allowedMeshGroup)
        {
            if (!hitInfo.transform.gameObject.IsSuitableAnchor(allowedMeshGroup))
            {
                return SpawnerController.FindNearbyTransform(hitInfo, fallbackTransform, allowedMeshGroup);
            }

            GameObject parent = SpawnerController.FindUniqueParent(hitInfo.transform.gameObject);
            if (!parent.IsSuitableAnchor(allowedMeshGroup))
            {
                return SpawnerController.FindNearbyTransform(hitInfo, fallbackTransform, allowedMeshGroup);
            }

            return parent?.transform ?? fallbackTransform;
        }

        /// <summary>
        /// Find a suitable nearby transform using OverlapSphere
        /// </summary>
        /// <param name="fallbackTransform">Fallback transform to use if no suitable transforms can
        /// be found</param>
        /// <param name="allowedMeshGroup">Name of allowed group for mesh parents if our own spawned
        /// objects are allowed to be returned, which is fine for lights but we don't really want
        /// meshes to be parented off eachother, and we only want to parent off objects in the same
        /// group, otherwise we could end up parented to an object which is disabled/removed</param>
        /// <param name="permissive">True if we are expanding the search because the small sphere
        /// returned no results</param>
        /// <returns></returns>
        private static Transform FindNearbyTransform(RaycastHit hitInfo, Transform fallbackTransform, string allowedMeshGroup, bool permissive = false)
        {
            Vector3 hitPosition = hitInfo.transform.position;
            float minDistanceSq = float.MaxValue;
            GameObject closestUniqueObject = null;
            foreach (Collider candidate in Physics.OverlapSphere(hitPosition, permissive ? Region.TICK_RADIUS : Region.RADIUS))
            {
                float distanceSq = (candidate.transform.position - hitPosition).sqrMagnitude;
                if (distanceSq < minDistanceSq)
                {
                    GameObject obj = SpawnerController.FindUniqueParent(candidate.gameObject);
                    if (obj != null && obj.IsSuitableAnchor(allowedMeshGroup))
                    {
                        closestUniqueObject = obj;
                        minDistanceSq = distanceSq;
                    }
                }
            }

            if (closestUniqueObject != null)
            {
                return closestUniqueObject.transform;
            }

            return permissive ? fallbackTransform : SpawnerController.FindNearbyTransform(hitInfo, fallbackTransform, allowedMeshGroup, true);
        }

        private static GameObject FindUniqueParent(GameObject objectInHierarchy)
        {
            GameObject objectById = GameObjectPath.Find(objectInHierarchy.GetObjectPath());
            while (objectById != objectInHierarchy && objectInHierarchy.transform.localScale != Vector3.one)
            {
                if (objectInHierarchy.transform.parent == null)
                {
                    break;
                }

                objectInHierarchy = objectInHierarchy.transform.parent.gameObject;
                objectById = GameObjectPath.Find(objectInHierarchy.GetObjectPath());
            }

            if (!object.ReferenceEquals(objectById, objectInHierarchy))
            {
                return null;
            }

            MeshComponent parentMesh = objectById.GetComponentInParent<MeshComponent>();
            return parentMesh != null ? parentMesh.gameObject : objectById;
        }

        internal void NotifySpawned(Spawner spawner)
        {
            this.stats.NotifySpawned(spawner);
        }

        internal void NotifyDestroyed(Spawner spawner)
        {
            this.stats.NotifyDestroyed(spawner);
        }

        internal void NotifyUpdated(Region region)
        {
            if (DebugOverlay.Active)
            {
                SpawnerController.debugUpdates.Text += "[" + region.YardID + "] ";
            }
        }
    }
}
