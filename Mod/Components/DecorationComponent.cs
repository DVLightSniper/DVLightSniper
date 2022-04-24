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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.GameObjects.Spawners;

using JetBrains.Annotations;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Component added to a spawned decoration in order to apply duty cycle effects, also hides the
    /// object if 
    /// </summary>
    internal class DecorationComponent : SpawnerComponent
    {
        internal event Action<DecorationComponent> OnDestroyed;

        internal DecorationSpawner Spawner { get; set; }

        internal bool Hidden { get; set; }

        private bool isHidden = false;

        private bool isOn = true;

        private MeshRenderer[] renderers;

        [UsedImplicitly]
        private void Start()
        {
            this.renderers = this.gameObject.GetComponentsInChildren<MeshRenderer>();
        }

        [UsedImplicitly]
        private void Update()
        {
            if (this.renderers == null)
            {
                return;
            }

            if (this.isHidden != this.Hidden)
            {
                this.isHidden = this.Hidden;
                foreach (MeshRenderer renderer in this.renderers)
                {
                    renderer.enabled = !this.isHidden;
                }
                return;
            }

            if (!this.isHidden && this.Spawner != null && this.Spawner.On != this.isOn)
            {
                this.isOn = this.Spawner.On;
                foreach (MeshRenderer renderer in this.renderers)
                {
                    renderer.enabled = this.isOn;
                }
            }
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            Action<DecorationComponent> onDestroyed = this.OnDestroyed;
            onDestroyed?.Invoke(this);
        }
    }
}
