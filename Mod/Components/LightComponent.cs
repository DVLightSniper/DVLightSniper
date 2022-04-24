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

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners;

using JetBrains.Annotations;

using UnityEngine;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Attached to spawned lights to provide designer sprites and apply duty cycle and pre-cull
    /// fading to the Light component
    /// </summary>
    internal class LightComponent : SpriteComponent
    {
        internal LightSpawner Spawner { get; set; }

        internal override ISpriteOwner Owner
        {
            get
            {
                return this.Spawner;
            }
            set
            {
                this.Spawner = value as LightSpawner;
            }
        }

        private Light light;

        [UsedImplicitly]
        private void Start()
        {
            this.light = this.gameObject.GetComponent<Light>();
        }

        [UsedImplicitly]
        private void Update()
        {
            if (this.Spawner != null)
            {
                this.light.enabled = this.Spawner.On;
                this.light.intensity = this.Spawner.Properties.Intensity * this.Spawner.PreCullFade;
                this.light.renderMode = LightSniper.Settings.RenderMode;

                this.UpdateSprites();
            }
        }
    }
}
