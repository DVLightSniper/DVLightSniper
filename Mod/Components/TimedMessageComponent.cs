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

using JetBrains.Annotations;

using TMPro;

using UnityEngine;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// Manages displaying messages for a limited time on the HoloDisplay
    /// </summary>
    internal class TimedMessageComponent : MonoBehaviour
    {
        private DateTime messageTime;

        private bool displayed;

        private TextMeshPro text;

        [UsedImplicitly]
        private void Start()
        {
            this.text = this.gameObject.GetComponent<TextMeshPro>();
            this.text.SetText("");
        }

        [UsedImplicitly]
        private void Update()
        {
            if (this.displayed && (DateTime.UtcNow - this.messageTime).TotalSeconds > 3)
            {
                this.text.SetText("");
                this.displayed = false;
            }
        }

        internal void SetText(string text)
        {
            this.SetText(text, Color.red);
        }

        internal void SetText(string text, Color colour)
        {
            this.text.SetText(text ?? "");
            this.text.color = colour;
            this.displayed = text != null;
            this.messageTime = DateTime.UtcNow;
        }
    }
}