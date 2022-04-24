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

using System.Runtime.Serialization;

using DVLightSniper.Mod.Components;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// Properties bundle for corona sprite
    /// </summary>
    [DataContract]
    internal class CoronaProperties
    {
        [DataMember(Name = "sprite", Order = 0)]
        internal string Sprite { get; set; }

        [DataMember(Name = "scale", Order = 1)]
        internal float Scale { get; set; }

        [DataMember(Name = "range", Order = 2)]
        internal float Range { get; set; }

        public CoronaProperties(string sprite, float scale = 1.0F, float range = 100.0F)
        {
            this.Range = range;
            this.Scale = scale;
            this.Sprite = sprite;
        }

        internal CoronaComponent Configure(CoronaComponent corona)
        {
            if (corona == null)
            {
                return null;
            }

            corona.Corona = this.Sprite;
            corona.Scale = this.Scale;
            corona.Range = this.Range;
            return corona;
        }

        internal CoronaProperties Clone()
        {
            return new CoronaProperties(this.Sprite, this.Scale, this.Range);
        }

        public override string ToString()
        {
            return $"CoronaProperties(Sprite={this.Sprite} Scale={this.Scale:F3} Range={this.Range:F3})";
        }
    }
}
