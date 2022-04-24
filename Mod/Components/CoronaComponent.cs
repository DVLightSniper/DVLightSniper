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

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Provides an old-school-UT lighting corona effect using a Sprite which is constantly oriented
    /// toward the player camera and fades in & out depending on the viewer's distance from the
    /// light source.
    /// </summary>
    internal class CoronaComponent : MonoBehaviour
    {
        /// <summary>
        /// Handle to a texture resource which is (probably) loaded from a file on disk
        /// </summary>
        internal class TextureHandle
        {
            /// <summary>
            /// Filename to load the texture from
            /// </summary>
            internal string Filename { get; }

            /// <summary>
            /// Texture resource to load the texture data into
            /// </summary>
            internal Texture2D Texture { get; }

            /// <summary>
            /// Flag which indicates the file was changed on disk, used to trigger a reload of the
            /// sprite the next time it's updated
            /// </summary>
            internal bool Changed { get; private set; }

            /// <summary>
            /// True if the file was loaded successfully
            /// </summary>
            internal bool Valid { get; private set; }

            private readonly string path;

            public TextureHandle(string fileName)
            {
                this.Filename = fileName;
                this.Texture = new Texture2D(CoronaComponent.TEXTURE_SIZE, CoronaComponent.TEXTURE_SIZE)
                {
                    filterMode = FilterMode.Point
                };
                
                this.path = Path.Combine(CoronaComponent.Dir, fileName);

                FileSystemWatcher watcher = new FileSystemWatcher(CoronaComponent.Dir)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = fileName
                };

                this.ReloadSprite();

                watcher.Changed += (sender, e) => this.Changed = true;
                watcher.EnableRaisingEvents = true;
            }

            internal void ReloadSprite()
            {
                if (!File.Exists(this.path))
                {
                    object data = Resources.ResourceManager.GetObject(this.Filename, Resources.Culture);
                    if (data != null)
                    {
                        this.Texture.LoadImage((byte[])data);
                        this.Valid = true;
                    }
                    else
                    {
                        byte[] resource = PackLoader.OpenResource(this.path.RelativeToBaseDir());
                        if (resource != null)
                        {
                            this.Texture.LoadImage(resource);
                            this.Valid = true;
                        }
                    }

                    this.Changed = false;
                    return;
                }

                this.Valid = false;

                try
                {
                    byte[] data = File.ReadAllBytes(this.path);
                    this.Texture.LoadImage(data);
                    this.Changed = false;
                    this.Valid = true;
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Error(e);
                }
            }
        }

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

        internal const int TEXTURE_SIZE = 256;

        private const float SCALE_ADJUST = 0.6F;

        private const string BUILTIN_SHADER_NAME = "assets/builtin/shaders/corona.shader";
        private const string FALLBACK_SHADER_NAME = "Hidden/Internal-GUITexture";

        private const int LINGER_FRAMES = 15;

        private static IList<string> availableCoronas = null;

        internal static IList<string> AvailableCoronas
        {
            get
            {
                if (CoronaComponent.availableCoronas == null)
                {
                    CoronaComponent.availableCoronas = new List<string> { "" };
                    foreach (string file in Directory.EnumerateFiles(CoronaComponent.Dir, "*.png"))
                    {
                        CoronaComponent.availableCoronas.Add(Path.GetFileName(file));
                    }
                }

                return CoronaComponent.availableCoronas;
            }
        }

        private static readonly IDictionary<string, TextureHandle> coronaTextures = new Dictionary<string, TextureHandle>();

        private static TextureHandle GetCoronaTexture(string fileName)
        {
            if (!CoronaComponent.coronaTextures.ContainsKey(fileName))
            {
                CoronaComponent.coronaTextures[fileName] = new TextureHandle(fileName);
            }

            return CoronaComponent.coronaTextures[fileName];
        }

        internal static string Dir { get; }

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

        private TextureHandle texture;
        private Material material;
        private SpriteRenderer spriteRenderer;

        private bool useAlphaScale = true;
        private int fadeOutFrames = 0;

        private bool failedToLoadShader;

        static CoronaComponent()
        {
            CoronaComponent.Dir = AssetLoader.Coronas.Dir;
        }

        [UsedImplicitly]
        private void Update()
        {
            if (this.Corona == null || this.failedToLoadShader || (LightSniper.Settings.CoronaOpacity == 0))
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
                this.texture.ReloadSprite();
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
                    this.useAlphaScale = true;
                    Shader shader = AssetLoader.Builtin.LoadBuiltin<Shader>(CoronaComponent.BUILTIN_SHADER_NAME);
                    if (shader == null)
                    {
                        LightSniper.Logger.Warn("Failed to load corona shader from internal resources, using fallback shader");
                        this.useAlphaScale = false;
                        shader = Shader.Find(CoronaComponent.FALLBACK_SHADER_NAME);
                        if (shader == null)
                        {
                            LightSniper.Logger.Error("Failed to load fallback shader " + CoronaComponent.FALLBACK_SHADER_NAME + " for corona sprite renderer");
                            this.failedToLoadShader = true;
                            return;
                        }
                    }

                    this.material = new Material(shader);
                    this.material.hideFlags = HideFlags.HideAndDontSave;
                    this.spriteRenderer = this.gameObject.AddComponent<SpriteRenderer>();

                    this.texture = CoronaComponent.GetCoronaTexture(this.Corona);
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

                this.texture = CoronaComponent.GetCoronaTexture(this.Corona);
                Sprite sprite = Sprite.Create(this.texture.Texture, new Rect(0, 0, CoronaComponent.TEXTURE_SIZE, CoronaComponent.TEXTURE_SIZE), new Vector2(0.5F, 0.5F));
                this.spriteRenderer.sprite = sprite;

                Object.Destroy(oldSprite);
            }

            if (this.spriteRenderer == null || SpawnerController.PlayerCameraTransform == null)
            {
                return;
            }

            float preCullFade = 0.0F;

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

            float cameraDistance = Vector3.Distance(SpawnerController.PlayerCameraTransform.position, this.spriteRenderer.transform.position);

            if (cameraDistance > this.MaxDistance)
            {
                this.spriteRenderer.enabled = false;
                return;
            }

            Vector3 rayDirection = SpawnerController.PlayerCameraTransform.position - this.spriteRenderer.transform.position;
            Ray ray = new Ray(this.spriteRenderer.transform.position, rayDirection);
            Physics.Raycast(ray, out RaycastHit hitInfo, this.MaxDistance, CoronaComponent.LAYER_MASK);

            float fadeScale = 1.0F;
            bool visibleToPlayer = false;
            if (hitInfo.transform?.tag == "Player")
            {
                visibleToPlayer = true;
            }
            else if (SpawnerController.IsFreeCamActive && hitInfo.transform != null)
            {
                float traceDistance = Vector3.Distance(hitInfo.transform.position, this.spriteRenderer.transform.position);
                visibleToPlayer = traceDistance >= cameraDistance;
            }

            if (visibleToPlayer)
            {
                this.fadeOutFrames = CoronaComponent.LINGER_FRAMES;
            }
            else
            {
                if (--this.fadeOutFrames < 1)
                {
                    this.spriteRenderer.enabled = false;
                    return;
                }

                fadeScale = (float)this.fadeOutFrames / CoronaComponent.LINGER_FRAMES;
            }

            this.spriteRenderer.transform.forward = SpawnerController.PlayerCameraTransform.forward;
            this.spriteRenderer.enabled = true;

            float spriteScale = 2.0F + (cameraDistance / 4.0F);
            float spriteDistanceFade = 1.0F;
            if (cameraDistance > this.PeakDistance)
            {
                spriteDistanceFade = (this.MaxDistance - cameraDistance) / (this.MaxDistance - this.PeakDistance);
            }

            if (this.useAlphaScale)
            {
                this.material.SetFloat("_AlphaScale", spriteDistanceFade * fadeScale);
                fadeScale = (LightSniper.Settings.CoronaOpacity * 0.01F);
                spriteDistanceFade = Math.Min(1.0F, spriteDistanceFade * 2.0F);
            }

            this.spriteRenderer.transform.SetGlobalScale(Vector3.one * this.Scale * CoronaComponent.SCALE_ADJUST * spriteScale * spriteDistanceFade * fadeScale * preCullFade);
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            Object.Destroy(this.material);
        }
    }
}
