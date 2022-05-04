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

using Newtonsoft.Json.Linq;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// Wrapper for JObject which returns the pack too
    /// </summary>
    internal class PackJson
    {
        /// <summary>
        /// The pack which supplied the stream
        /// </summary>
        internal Pack Pack { get; }

        /// <summary>
        /// The filename within the pack
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// Modification time of the resource
        /// </summary>
        internal DateTime LastWriteTime { get; }

        /// <summary>
        /// The json object
        /// </summary>
        internal JObject JObject { get; }

        internal PackJson(Pack pack, string name, DateTime lastWriteTime, JObject jObject)
        {
            this.Pack = pack;
            this.Name = name;
            this.LastWriteTime = lastWriteTime;
            this.JObject = jObject;
        }
    }
}
