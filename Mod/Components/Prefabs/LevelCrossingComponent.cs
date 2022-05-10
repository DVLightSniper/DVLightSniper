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
using System.Security.Cryptography;
using System.Text;

using DV;

using DVLightSniper.Mod.GameObjects.Library;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using JetBrains.Annotations;

using UnityEngine;

using Console = System.Console;
using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;
using TimingSection = DVLightSniper.Mod.GameObjects.SpawnerController.TimingSection;
using TimingLevel = DVLightSniper.Mod.GameObjects.SpawnerController.TimingLevel;

namespace DVLightSniper.Mod.Components.Prefabs
{
    /// <summary>
    /// Component added to spawned level crossings which manages the level crossing behaviour such
    /// as detecting train cars, activating the barrier and sounds, and linking with nearby barriers
    /// </summary>
    [PrefabComponent("LevelCrossing", "meshes_levelcrossings.assetbundle", "LevelCrossing")]
    internal class LevelCrossingComponent : MonoBehaviour
    {
        /// <summary>
        /// Radius to link our behaviour with other level crossings
        /// </summary>
        private const float LINK_RADIUS = 25.0F;

        /// <summary>
        /// Adjacent level crossings share their barrier state via this object, which keeps track
        /// of triggered crossings to share the triggered state such that all level crossings
        /// trigger when the first one is triggered, and open again once untriggered
        /// </summary>
        internal class State
        {
            private readonly ISet<LevelCrossingComponent> triggered = new HashSet<LevelCrossingComponent>();

            internal event Action<bool> Active;

            internal bool IsActive { get; private set; }

            public void Update(LevelCrossingComponent levelCrossing, bool state)
            {
                if (state)
                {
                    this.triggered.Add(levelCrossing);
                }
                else
                {
                    this.triggered.Remove(levelCrossing);
                }

                bool shouldBeActive = this.triggered.Count > 0;
                if (shouldBeActive != this.IsActive)
                {
                    this.IsActive = shouldBeActive;
                    this.Active?.Invoke(shouldBeActive);
                }
            }
        }

        private const int UPDATE_RATE_MS = 100;

        private const float ROTATION_DOWN = 270;
        private const float ROTATION_UP = 190;
        private const float ARM_VELOCITY = 30.0F;

        private float desiredRotation = LevelCrossingComponent.ROTATION_UP;
        private float currentRotation = LevelCrossingComponent.ROTATION_DOWN;

        private Transform arm;

        private BoxCollider trigger;

        private readonly Stopwatch updateTimer = new Stopwatch();

        private readonly Collider[] colliders = new Collider[1];

        private AudioSource audioSource;

        private AssetProperty<AudioClip> soundProperty;
        private IntProperty volumeProperty;

        /// <summary>
        /// Local triggered state, independent of whether the barrier is actually active, which is
        /// managed by the state
        /// </summary>
        private bool isTriggered;

        /// <summary>
        /// State manager, shared with other nearby level crossings
        /// </summary>
        private State state;

        private readonly TimingSection timings = TimingSection.Get("levelcrossing", TimingLevel.Dev);

        [UsedImplicitly]
        private void Start()
        {
            this.arm = this.gameObject.transform.Find("Arm");
            this.trigger = this.gameObject.GetComponent<BoxCollider>();

            this.audioSource = this.GetComponent<AudioSource>();

            string assetBundleName = this.GetComponent<MeshComponent>().Properties.AssetBundleName;

            this.soundProperty = new AssetProperty<AudioClip>("level_crossing.sound", "siren", this.audioSource.clip, AssetLoader.Meshes, assetBundleName, "level_crossing_");
            this.soundProperty.Changed += this.UpdateSound;
            this.UpdateSound(this.soundProperty.Value);

            this.volumeProperty = new IntProperty("level_crossing.volume", "100", 100);
            this.volumeProperty.Changed += this.UpdateVolume;
            this.UpdateVolume(this.volumeProperty.Value);

            this.SetActive(false);
            this.updateTimer.Start();
        }

        private void UpdateSound(AudioClip clip)
        {
            this.audioSource.clip = Object.Instantiate(clip);
            this.audioSource.enabled = this.state != null && this.state.IsActive && this.soundProperty.PropertyValue != "none";
            if (this.audioSource.enabled)
            {
                this.audioSource.Play();
            }
        }

        private void UpdateVolume(int volume)
        {
            this.audioSource.volume = (volume * 0.01F).Clamp(0.0F, 1.0F);
        }

        /// <summary>
        /// Initialise the shared state object by finding other nearby level crossings and linking
        /// with them, we don't do this until we are triggered as the OverlapSphere won't work if
        /// we're outside culling radius
        /// </summary>
        private void InitState()
        {
            foreach (Collider collider in Physics.OverlapSphere(this.gameObject.transform.position, LevelCrossingComponent.LINK_RADIUS))
            {
                LevelCrossingComponent levelCrossing = collider.gameObject.GetComponent<LevelCrossingComponent>();
                if (collider.gameObject != this.gameObject && levelCrossing != null)
                {
                    if (levelCrossing.state == null)
                    {
                        levelCrossing.state = this.state ?? new State();
                        levelCrossing.ConnectToState(levelCrossing.state);
                    }

                    if (this.state == null)
                    {
                        this.state = levelCrossing.state;
                    }
                }
            }

            if (this.state == null)
            {
                this.state = new State();
            }

            this.ConnectToState(this.state);
        }

        private void ConnectToState(State state)
        {
            state.Active += this.SetActive;
            this.SetActive(state.IsActive);
        }

        private void SetActive(bool active)
        {
            // If there are train cars in the trigger, drop the barrier
            this.desiredRotation = active ? LevelCrossingComponent.ROTATION_DOWN : LevelCrossingComponent.ROTATION_UP;

            // and enable the flashing lights
            this.GetComponent<MeshComponent>().Spawner.Inhibit = !active;

            if (this.soundProperty.PropertyValue != "none" || !active)
            {
                // and the siren
                this.audioSource.enabled = active;
            }

            // and fire the arm mechanical noise
            this.arm.GetComponent<AudioSource>().Play();
        }

        private void UpdateTriggered(bool triggered)
        {
            if (this.isTriggered != triggered)
            {
                this.isTriggered = triggered;
                if (this.state == null)
                {
                    this.InitState();
                }
                this.state.Update(this, triggered);
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            if (this.arm == null)
            {
                return;
            }

            float deltaAngle = UnityEngine.Time.deltaTime * LevelCrossingComponent.ARM_VELOCITY;
            if (this.desiredRotation < this.currentRotation)
            {
                this.currentRotation -= deltaAngle;
            }
            else if (this.desiredRotation > this.currentRotation)
            {
                this.currentRotation += deltaAngle;
            }

            this.currentRotation = Mathf.Min(Mathf.Max(this.currentRotation, LevelCrossingComponent.ROTATION_UP), LevelCrossingComponent.ROTATION_DOWN);
            this.arm.localRotation = Quaternion.Euler(this.currentRotation, 0, 0);
        }

        [UsedImplicitly]
        private void FixedUpdate()
        {
            this.timings.Start();
            if (this.updateTimer.ElapsedMilliseconds >= LevelCrossingComponent.UPDATE_RATE_MS)
            {
                Vector3 triggerCentre = this.transform.TransformPoint(this.trigger.center);
                int count = Physics.OverlapBoxNonAlloc(triggerCentre, this.trigger.size * 0.5F, this.colliders, this.trigger.transform.rotation, LayerMask.GetMask(Layers.Train_Big_Collider));
                this.UpdateTriggered(count > 0);
                this.updateTimer.Restart();
            }
            this.timings.End();
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            this.UpdateTriggered(false);
        }
    }
}
