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
using System.IO;

using DVLightSniper.Mod.GameObjects;

using JetBrains.Annotations;

using UnityEngine;

using VRTK;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Handles loading a sprite texture (atlas) and keeping the sprite oriented toward the player
    /// camera in a billboard fashion. Used for the light designer and selection sprites as well as
    /// the placement gizmo.
    /// </summary>
    internal class SpriteComponent : SpawnerComponent
    {
        internal interface ISpriteOwner
        { 
            bool ShowSprites { get; }

            int SpriteIndex { get; }
        }

        private static Texture2D spriteTexture;

        internal static Texture2D SpriteTexture
        {
            get
            {
                if (SpriteComponent.spriteTexture == null)
                {
                    SpriteComponent.spriteTexture = new Texture2D(128, 128);
                    SpriteComponent.spriteTexture.LoadImage(Properties.Resources.Sprites);
                    SpriteComponent.spriteTexture.filterMode = FilterMode.Point;
                }

                return SpriteComponent.spriteTexture;
            }
        }

        private static readonly Sprite[] sprites = new Sprite[16];

        private int spriteIndex = -1;

        private SpriteRenderer spriteRenderer;

        internal virtual ISpriteOwner Owner { get; set; }

        internal float Scale { get; set; } = 2.0F;

        internal bool Billboard { get; set; } = true;

        internal event Action<SpriteComponent> OnDestroyed;

        private static Sprite GetSprite(int spriteIndex)
        {
            if (SpriteComponent.sprites[spriteIndex] == null)
            {
                int spriteX = spriteIndex % 4 * 32;
                int spriteY = 128 - (spriteIndex / 4 * 32) - 32;
                SpriteComponent.sprites[spriteIndex] = Sprite.Create(SpriteComponent.SpriteTexture, new Rect(spriteX, spriteY, 32, 32), new Vector2(0.5F, 0.5F));
            }

            return SpriteComponent.sprites[spriteIndex];
        }

        [UsedImplicitly]
        private void Update()
        {
            this.UpdateSprites();
        }

        protected void UpdateSprites()
        {
            if (this.Owner == null)
            {
                return;
            }

            if (this.Owner.ShowSprites)
            {
                if (this.spriteRenderer == null)
                {
                    this.spriteRenderer = this.gameObject.AddComponent<SpriteRenderer>();
                }

                if (this.Owner.SpriteIndex != this.spriteIndex)
                {
                    this.spriteIndex = this.Owner.SpriteIndex;
                    this.spriteRenderer.sprite = SpriteComponent.GetSprite(this.spriteIndex);
                }

                this.spriteRenderer.enabled = true;
                if (this.Billboard)
                {
                    this.spriteRenderer.transform.forward = SpawnerController.PlayerCameraTransform.forward;
                }
                this.spriteRenderer.transform.SetGlobalScale(Vector3.one * this.Scale);
            }
            else if (this.spriteRenderer != null)
            {
                this.spriteRenderer.enabled = false;
            }
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            Action<SpriteComponent> onDestroyed = this.OnDestroyed;
            onDestroyed?.Invoke(this);
        }
    }
}
