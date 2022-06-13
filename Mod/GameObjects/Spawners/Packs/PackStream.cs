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

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// Wrapper for Stream which returns the pack too
    /// </summary>
    internal class PackStream : IDisposable
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
        /// The stream
        /// </summary>
        internal Stream Stream { get; }

        /// <summary>
        /// The length of the underlying resource, if known, or -1 if not
        /// </summary>
        internal long ContentLength { get; }

        private byte[] data;

        internal PackStream(Pack pack, string name, DateTime lastWriteTime, Stream stream, long contentLength)
        {
            this.Pack = pack;
            this.Name = name;
            this.LastWriteTime = lastWriteTime;
            this.Stream = stream;
            this.ContentLength = contentLength;
        }

        internal byte[] GetData()
        {
            if (this.data != null)
            {
                return this.data;
            }

            if (this.ContentLength < 0)
            {
                throw new InvalidOperationException($"Content length not known for {this} in GetData");
            }

            long cursor = this.Stream.Position;
            this.Stream.Position = 0;

            this.data = new byte[this.ContentLength];
            using (MemoryStream ms = new MemoryStream(this.data))
            {
                this.Stream.CopyTo(ms);
            }

            this.Stream.Position = cursor;
            return this.data;
        }

        public override string ToString()
        {
            return this.Pack.Path + "!" + Name;
        }

        public void Dispose()
        {
            this.Stream?.Dispose();
        }
    }
}
