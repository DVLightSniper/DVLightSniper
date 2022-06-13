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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// An unpacked pack, in a folder
    /// </summary>
    internal class FolderPack : Pack
    {
        public FolderPack(string path, Metadata metaStore)
            : base(path, metaStore)
        {
        }

        internal IEnumerable<FileInfo> Files
        {
            get => new DirectoryInfo(this.Path).EnumerateFiles("*.*", SearchOption.AllDirectories);
        }

        internal override IEnumerable<string> Find(string path, string extension = null)
        {
            if (extension != null && extension.StartsWith("*"))
            {
                extension = extension.Substring(1);
            }

            List<string> results = new List<string>();
            foreach (FileInfo file in this.Files)
            {
                string name = file.RelativeToDir(this.Path);
                if (name.StartsWith(path) && (extension == null || name.EndsWith(extension)))
                {
                    results.Add(name);
                }
            }
            return results;
        }

        internal override bool Contains(string path)
        {
            try
            {
                return File.Exists(System.IO.Path.Combine(this.Path, path));
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
                return false;
            }
        }

        internal override DateTime GetLastWriteTime(string path)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(this.Path, path);
                if (File.Exists(fullPath))
                {
                    return File.GetLastWriteTimeUtc(fullPath);
                }
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
            }
            return DateTime.MinValue;
        }

        internal override PackStream OpenStream(string path)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(this.Path, path);
                if (File.Exists(fullPath))
                {
                    FileInfo fileInfo = new FileInfo(fullPath);
                    return new PackStream(this, path.ConformSlashes(), fileInfo.LastWriteTime, new FileStream(fullPath, FileMode.Open), fileInfo.Length);
                }
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
            }
            return null;
        }

        internal override PackResource OpenResource(string path)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(this.Path, path);
                if (File.Exists(fullPath))
                {
                    return new PackResource(this, path.ConformSlashes(), File.GetLastWriteTime(fullPath), File.ReadAllBytes(fullPath));
                }
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
            }
            return null;
        }
    }
}
