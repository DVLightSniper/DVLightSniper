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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// A pack contained in a zip file
    /// </summary>
    internal class ZippedPack : Pack
    {
        private ZipArchive zip;

        internal ZippedPack(string path, PackMetaStorage metaStorage)
            : base(path, metaStorage)
        {
        }

        protected override bool Open()
        {
            try
            {
                this.zip = ZipFile.OpenRead(this.Path);
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
                return false;
            }
            return base.Open();
        }

        internal override IEnumerable<string> Find(string path, string extension = null)
        {
            path = ZippedPack.SanitiseZipPath(path);
            List<string> results = new List<string>();
            foreach (ZipArchiveEntry entry in this.zip.Entries)
            {
                if (entry.FullName.StartsWith(path) && (extension == null || entry.FullName.EndsWith(extension)))
                {
                    results.Add(entry.FullName);
                }
            }
            return results;
        }

        internal override Stream OpenStream(string path)
        {
            try
            {
                return this.zip.GetEntry(ZippedPack.SanitiseZipPath(path))?.Open();
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
                return null;
            }
        }

        internal override byte[] OpenResource(string path)
        {
            try
            {
                ZipArchiveEntry entry = this.zip.GetEntry(ZippedPack.SanitiseZipPath(path));
                if (entry == null)
                {
                    return null;
                }

                byte[] buffer = new byte[entry.Length];
                using (MemoryStream ms = new MemoryStream(buffer))
                {
                    entry.Open().CopyTo(ms);
                }
                return buffer;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
                return null;
            }
        }

        public override void Dispose()
        {
            this.zip?.Dispose();
        }

        private static string SanitiseZipPath(string path)
        {
            path = path.Replace('\\', '/');
            while (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }
            return path;
        }
    }
}
