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
    /// A material assignment, defining the material index to assign from the source prefab, and the
    /// name of the renderer on the target object to apply it to.
    /// </summary>
    [DataContract]
    internal class MaterialAssignment
    {
        /// <summary>
        /// Name of the renderer on the target object to which this material will be added
        /// </summary>
        [DataMember(Name = "renderer", Order = 0)]
        internal string RendererName { get; set; }

        /// <summary>
        /// Index on the source prefab from which the material should be copied
        /// </summary>
        [DataMember(Name = "fromIndex", Order = 1)]
        internal int FromIndex { get; set; }

        public MaterialAssignment(string rendererName, int fromIndex)
        {
            this.RendererName = rendererName;
            this.FromIndex = fromIndex;
        }
    }
}
