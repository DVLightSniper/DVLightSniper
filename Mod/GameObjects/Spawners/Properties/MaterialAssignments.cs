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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// A collection of material assignments to apply when the decoration is active
    /// </summary>
    [DataContract]
    internal class MaterialAssignments
    {
        /// <summary>
        /// True if the source prefab should be hidden once the materials are assigned. This is
        /// useful if the source prefab is a simple cube which is just acting as a holder for the
        /// materials
        /// </summary>
        [DataMember(Name = "hideSourcePrefab", Order = 0)]
        internal bool HideSourcePrefab { get; set; } = true;

        /// <summary>
        /// Material assignments
        /// </summary>
        [DataMember(Name = "assignments", Order = 1, EmitDefaultValue = false)]
        internal List<MaterialAssignment> Assignments { get; set; } = new List<MaterialAssignment>();

        /// <summary>
        /// True to apply random alpha values (the material must use the Unlit-Alpha shader)
        /// </summary>
        [DataMember(Name = "randomAlpha", Order = 2, EmitDefaultValue = false)]
        internal float RandomAlpha { get; set; }

        internal int Count
        {
            get { return this.Assignments.Count; }
        }

        public float MinRandomAlpha
        {
            get
            {
                return 1.0F - Math.Min(Math.Max(this.RandomAlpha, 0.0F), 0.9F);
            }
        }

        public float MaxRandomAlpha
        {
            get
            {
                return 1.0F;
            }
        }

        internal MaterialAssignment this[int index]
        {
            get { return this.Assignments[index]; }
        }

        internal void Add(MaterialAssignment materialAssignment)
        {
            this.Assignments.Add(materialAssignment);
        }
    }
}
