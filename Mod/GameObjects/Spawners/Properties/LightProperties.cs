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
using System.IO;
using System.Runtime.Serialization;

using DVLightSniper.Mod.Components;

using Extensions.SystemRandom;

using Newtonsoft.Json;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Random = System.Random;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// Properties to apply to a sniped light
    /// </summary>
    [DataContract]
    internal class LightProperties
    {
        internal static readonly LightProperties DEFAULT = new LightProperties(1.0F, 20.0F, Color.white);

        private static readonly Random RANDOM = new Random();

        [DataMember(Name = "type", Order = 0)]
        internal LightType Type { get; set; }

        [DataMember(Name = "intensity", Order = 1)]
        internal float Intensity { get; set; }

        [DataMember(Name = "range", Order = 2)]
        internal float Range { get; set; }

        [DataMember(Name = "colour", Order = 3)]
        internal Color Colour { get; set; }

        [DataMember(Name = "corona", Order = 4)]
        internal CoronaProperties Corona { get; set; }

        [DataMember(Name = "dutyCycle", Order = 6)]
        private string dutyCycleDef;

        private DutyCycle dutyCycle;

        internal DutyCycle DutyCycle
        {
            get
            {
                return this.dutyCycle ?? (this.dutyCycle = DutyCycle.Parse(this.dutyCycleDef));
            }
            set
            {
                this.dutyCycle = (value ?? DutyCycle.ALWAYS);
                this.dutyCycleDef = this.dutyCycle.ToString();
            }
        }

        [DataMember(Name = "neverCull", Order = 7, EmitDefaultValue = false)]
        internal bool NeverCull { get; set; }

        [JsonConstructor]
        internal LightProperties(float intensity, float range, Color colour, LightType type = LightType.Point)
        {
            this.Type = type;
            this.Intensity = intensity;
            this.Range = range;
            this.Colour = colour;
        }

        internal LightProperties Clone()
        {
            LightProperties newProperties = new LightProperties(this.Intensity, this.Range, this.Colour, this.Type)
            {
                dutyCycle = null,
                dutyCycleDef = this.dutyCycleDef,
                Corona = this.Corona?.Clone(),
                NeverCull = this.NeverCull
            };
            return newProperties;
        }

        internal Light Configure(Light light)
        {
            if (light == null)
            {
                return null;
            }

            float randomVariance = Math.Min(20, LightSniper.Settings.randomVariancePct) * 0.01F;
            float randomMultiplier = 1.0F + LightProperties.RANDOM.NextFloat(-randomVariance, randomVariance);

            light.type = this.Type;
            light.intensity = this.Intensity * randomMultiplier;
            light.range = this.Range * randomMultiplier;
            light.color = this.Colour;

            return light;
        }

        public override string ToString()
        {
            return $"LightProperties(Type={this.Type} Intensity={this.Intensity:F3} Range={this.Range:F3} Colour={this.Colour} Corona={this.Corona} DutyCycle={this.dutyCycleDef})";
        }
    }
}
