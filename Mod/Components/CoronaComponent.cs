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
using System.Diagnostics;
using System.Globalization;
using System.IO;

using DV;

using DVLightSniper.Mod.GameObjects;
using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Library;
using DVLightSniper.Mod.GameObjects.Spawners.Packs;

using JetBrains.Annotations;

using UnityEngine;

using VRTK;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;
using Resources = DVLightSniper.Properties.Resources;
using TimingSection = DVLightSniper.Mod.GameObjects.SpawnerController.TimingSection;
using TimingLevel = DVLightSniper.Mod.GameObjects.SpawnerController.TimingLevel;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Provides an old-school-UT lighting corona effect using a Sprite which is constantly oriented
    /// toward the player camera and fades in & out depending on the viewer's distance from the
    /// light source.
    /// </summary>
    internal class CoronaComponent : MonoBehaviour
    {
        internal static readonly int LAYER_MASK = LayerMask.GetMask(
            Layers.Default,
            Layers.Terrain,
            Layers.Player,
            Layers.Train_Walkable,
            Layers.Train_Interior,
            Layers.Interactable,
            Layers.World_Item,
            Layers.Grabbed_Item,
            Layers.Render_Elements
        );

        /// <summary>
        /// Texture size for loaded corona textures
        /// </summary>
        internal const int TEXTURE_SIZE = 256;

        /// <summary>
        /// Global scale adjustment, for science
        /// </summary>
        private const float SCALE_ADJUST = 0.6F;

        /// <summary>
        /// Corona shader resource name to load from builtin assets
        /// </summary>
        private const string SHADER_NAME = "assets/builtin/shaders/corona.shader";

        /// <summary>
        /// Number of frames to fade out corona when it becomes occluded
        /// </summary>
        private const int LINGER_FRAMES = 15;

        /// <summary>
        /// Min time between position updates
        /// </summary>
        private const int POSITION_UPDATE_RATE = 25;

        private static IList<string> availableCoronas = null;

        internal static IList<string> AvailableCoronas
        {
            get
            {
                if (CoronaComponent.availableCoronas == null)
                {
                    CoronaComponent.availableCoronas = new List<string> { "" };
                    foreach (string file in AssetLoader.Coronas.ListFiles())
                    {
                        CoronaComponent.availableCoronas.Add(Path.GetFileName(file));
                    }
                }

                return CoronaComponent.availableCoronas;
            }
        }

        internal String Corona { get; set; }

        private string currentCorona;

        internal float Scale { get; set; } = 1.0F;

        internal float Range { get; set; } = 100.0F;

        private float MaxDistance
        {
            get
            {
                return Math.Max(this.Range, 25.0F);
            }
        }

        private float PeakDistance
        {
            get
            {
                return Math.Max(this.Range * 0.3F, 7.5F);
            }
        }

        internal LightSpawner Spawner { get; set; }

        private AssetLoader.TextureHandle texture;
        private Material material;
        private SpriteRenderer spriteRenderer;

        private int fadeOutFrames = 0;

        private bool failedToLoadShader;

        private readonly TimingSection timings = TimingSection.Get("corona", TimingLevel.Others);

        private Vector3 lastCameraPosition;
        private Stopwatch lastCameraComputeTime;
        private float lastCameraDistance;

        private float GetCameraDistance()
        {
            Vector3 cameraPosition = SpawnerController.PlayerCameraTransform.position;
            if (cameraPosition != this.lastCameraPosition && this.lastCameraComputeTime.ElapsedMilliseconds > CoronaComponent.POSITION_UPDATE_RATE)
            {
                this.lastCameraDistance = Vector3.Distance(cameraPosition, this.spriteRenderer.transform.position);
                this.lastCameraPosition = cameraPosition;
                this.lastCameraComputeTime.Restart();
            }
            return this.lastCameraDistance;
        }

        [UsedImplicitly]
        private void Start()
        {
            this.lastCameraComputeTime = new Stopwatch();
            this.lastCameraComputeTime.Start();
        }

        [UsedImplicitly]
        private void Update()
        {
            this.timings.Start();
            this.UpdateCorona();
            this.timings.End();
        }

        private void UpdateCorona()
        {
            if (this.Corona == null || this.failedToLoadShader || LightSniper.Settings.CoronaOpacity == 0)
            {
                if (this.currentCorona != null)
                {
                    this.spriteRenderer.enabled = false;
                }

                return;
            }

            if (this.texture != null && this.texture.Changed)
            {
                CoronaComponent.availableCoronas = null;
                this.texture.Reload();
            }

            if (this.texture != null && !this.texture.Valid)
            {
                return;
            }

            if (this.material == null)
            {
                this.currentCorona = this.Corona;

                try
                {
                    Shader shader = AssetLoader.Builtin.LoadBuiltin<Shader>(CoronaComponent.SHADER_NAME);
                    if (shader == null)
                    {
                        LightSniper.Logger.Warn("Failed to load corona shader");
                        this.failedToLoadShader = true;
                        return;
                    }

                    this.material = new Material(shader);
                    this.material.hideFlags = HideFlags.HideAndDontSave;
                    this.spriteRenderer = this.gameObject.AddComponent<SpriteRenderer>();

                    this.texture = AssetLoader.Coronas.GetTexture(this.Corona, CoronaComponent.TEXTURE_SIZE);
                    Sprite sprite = Sprite.Create(this.texture.Texture, new Rect(0, 0, CoronaComponent.TEXTURE_SIZE, CoronaComponent.TEXTURE_SIZE), new Vector2(0.5F, 0.5F));
                    this.spriteRenderer.sprite = sprite;
                    this.spriteRenderer.material = this.material;
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Error(e);
                    Object.Destroy(this.gameObject);
                    return;
                }
            }
            else if (this.Corona != this.currentCorona)
            {
                this.currentCorona = this.Corona;

                Sprite oldSprite = this.spriteRenderer.sprite;

                this.texture = AssetLoader.Coronas.GetTexture(this.Corona, CoronaComponent.TEXTURE_SIZE);
                Sprite sprite = Sprite.Create(this.texture.Texture, new Rect(0, 0, CoronaComponent.TEXTURE_SIZE, CoronaComponent.TEXTURE_SIZE), new Vector2(0.5F, 0.5F));
                this.spriteRenderer.sprite = sprite;

                Object.Destroy(oldSprite);
            }

            if (this.spriteRenderer == null || SpawnerController.PlayerCameraTransform == null)
            {
                return;
            }

            float preCullFade = 1.0F;

            if (this.Spawner != null)
            {
                if (!this.Spawner.On)
                {
                    this.fadeOutFrames = 0;
                    this.spriteRenderer.enabled = false;
                    return;
                }

                this.material.SetColor("_TintColor", this.Spawner.Properties.Colour);
                preCullFade = this.Spawner.PreCullFade;
            }

            float cameraDistance = this.GetCameraDistance();

            if (cameraDistance > this.MaxDistance)
            {
                this.spriteRenderer.enabled = false;
                return;
            }

            Vector3 rayDirection = SpawnerController.PlayerCameraTransform.position - this.spriteRenderer.transform.position;
            Ray ray = new Ray(this.spriteRenderer.transform.position, rayDirection);
            Physics.Raycast(ray, out RaycastHit hitInfo, this.MaxDistance, CoronaComponent.LAYER_MASK);

            float fadeOutFade = 1.0F;

            // First figure out whether the player can see the centre point of the light
            bool visibleToPlayer = false;
            if (hitInfo.transform?.tag == "Player")
            {
                visibleToPlayer = true;
            }
            else if (SpawnerController.IsFreeCamActive)
            {
                visibleToPlayer = hitInfo.transform == null || hitInfo.distance >= cameraDistance;
            }

            if (visibleToPlayer)
            {
                // If the light is visible, render at full alpha
                this.fadeOutFrames = CoronaComponent.LINGER_FRAMES;
            }
            else
            {
                // Otherwise fade out the corona, this is less visually jarring than just turning it
                // off because if coronas disappear for only a couple of frames they don't "flicker"
                // and it also emulates a kind of after-image of the corona
                if (--this.fadeOutFrames < 1)
                {
                    this.spriteRenderer.enabled = false;
                    return;
                }

                fadeOutFade = (float)this.fadeOutFrames / CoronaComponent.LINGER_FRAMES;
            }

            // billboard behaviour, orient the sprite toward the current camera
            this.spriteRenderer.transform.forward = SpawnerController.PlayerCameraTransform.forward;
            this.spriteRenderer.enabled = true;

            // Sprite should stay (roughly) the same size on screen regardless of distance, though
            // it does change a little
            float spriteScale = 2.0F + (cameraDistance / 4.0F);
            float spriteDistanceFade = 1.0F;
            if (cameraDistance > this.PeakDistance)
            {
                // once we reach the "peak" distance (where the corona is largest and opaquest) any
                // distance further away causes the sprite to fade and shrink
                spriteDistanceFade = (this.MaxDistance - cameraDistance) / (this.MaxDistance - this.PeakDistance);
            }

            // Compute and apply the opacity based on the distance from the camera and the settings
            float opacity = LightSniper.Settings.CoronaOpacity * 0.01F;
            this.material.SetFloat("_AlphaScale", spriteDistanceFade * fadeOutFade * opacity);

            // clamp the distance fade before applying it to the scale
            spriteDistanceFade = Math.Min(1.0F, spriteDistanceFade * 2.0F);

            // set the sprite scale based on our computed values
            this.spriteRenderer.transform.SetGlobalScale(Vector3.one * this.Scale * CoronaComponent.SCALE_ADJUST * spriteScale * spriteDistanceFade * opacity * preCullFade);
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            Object.Destroy(this.material);
        }
    }
}
