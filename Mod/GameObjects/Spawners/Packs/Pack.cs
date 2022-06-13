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

using DV;

using DVLightSniper.Mod.Storage;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// Basically a wrapper for a ZipArchive which tracks
    /// </summary>
    internal abstract class Pack : IDisposable
    {
        /// <summary>
        /// When enabled state changes
        /// </summary>
        internal event Action<bool> EnabledChanged;

        /// <summary>
        /// Full path to this pack
        /// </summary>
        internal string Path { get; }

        /// <summary>
        /// The name part of the path for this pack, the zip filename or folder name
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// The ID of this pack, which is either the filename or the value specified in Info.json
        /// </summary>
        internal string Id { get; set; }

        /// <summary>
        /// The display name of this pack, read from the Info.json
        /// </summary>
        internal string DisplayName { get; private set; }

        /// <summary>
        /// The author of this pack, read from the Info.json
        /// </summary>
        internal string Author { get; private set; } = "Unknown";

        /// <summary>
        /// The version of this pack, read from the Info.json, this will be used to select the
        /// latest version of the pack if multiple conflicting versions exist
        /// </summary>
        internal string Version { get; private set; } = "Unknown";

        /// <summary>
        /// The Derail Valley Build this pack is for, specify this value in Info.json if you wish to
        /// restrict loading the pack to a specific version (force users to update the pack if it
        /// becomes outdated)
        /// </summary>
        internal int DVBuild { get; private set; } = Config.BUILD_VERSION;

        /// <summary>
        /// True if the pack was successfully opened, has a valid Info.json, and DVVersion matches
        /// the current DV build
        /// </summary>
        public bool Valid { get; protected set; }

        /// <summary>
        /// Get whether the pack is enabled
        /// </summary>
        internal bool Enabled
        {
            get
            {
                return this.GetFlag("enabled", true);
            }

            set
            {
                bool oldValue = this.Enabled;
                this.SetFlag("enabled", value);
                if (oldValue != value)
                {
                    this.EnabledChanged?.Invoke(value);
                }
            }
        }

        private readonly Metadata.Data meta;

        internal Pack(string path, Metadata metaStore)
        {
            this.Path = path;
            this.Name = System.IO.Path.GetFileName(path);
            this.DisplayName = this.Id = System.IO.Path.GetFileNameWithoutExtension(path);

            // ReSharper disable once VirtualMemberCallInConstructor
            this.Valid = this.Open();

            if (this.Valid)
            {
                this.meta = metaStore.Get(this);
                this.SetString("version", this.Version);
            }
        }

        protected virtual bool Open()
        {
            try
            {
                PackJson info = this.OpenJson("Info.json");
                if (info == null)
                {
                    return false;
                }
                this.ReadInfo(info.JObject);
                if (this.DVBuild != Config.BUILD_VERSION)
                {
                    LightSniper.Logger.Error("{0} with ID {1} and Version {2} is not compatible with this Derail Valley build ({3}) DVVersion={4}", this.Name, this.Id, this.Version, Config.BUILD_VERSION_STR, this.DVBuild);
                }
                return true;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
            }

            return false;
        }

        protected void ReadInfo(JObject info)
        {
            this.Id          = info["Id"]?          .Value<string>() ?? this.Id;
            this.DisplayName = info["DisplayName"]? .Value<string>() ?? this.DisplayName;
            this.Author      = info["Author"]?      .Value<string>() ?? this.Author;
            this.Version     = info["Version"]?     .Value<string>() ?? this.Version;
            this.DVBuild     = info["DVBuild"]?     .Value<int>()    ?? this.DVBuild;
        }

        internal abstract IEnumerable<string> Find(string path, string extension = null);

        internal abstract bool Contains(string path);

        internal abstract DateTime GetLastWriteTime(string path);

        internal abstract PackStream OpenStream(string path);

        internal abstract PackResource OpenResource(string path);

        internal virtual PackJson OpenJson(string path)
        {
            PackStream resource = this.OpenStream(path);
            if (resource == null)
            {
                return null;
            }
            try
            {
                using (StreamReader sr = new StreamReader(resource.Stream))
                {
                    return new PackJson(this, resource.Name, resource.LastWriteTime, JObject.Load(new JsonTextReader(sr)));
                }
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
                return null;
            }
        }

        internal virtual PackMetaStorage OpenMeta(string path)
        {
            PackStream resource = this.OpenStream(path);
            if (resource == null)
            {
                return null;
            }
            try
            {
                return new PackMetaStorage(this, resource.Name, resource.LastWriteTime, MetaStorage.Read(resource.Stream));
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
                return null;
            }
        }

        public virtual void Dispose()
        {
        }

        internal bool GetFlag(string key, bool defaultValue = false)
        {
            return this.meta.GetFlag(key, defaultValue);
        }

        internal void SetFlag(string key, bool value)
        {
            this.meta.SetFlag(key, value);
        }

        internal string GetString(string key, string defaultValue = "")
        {
            return this.meta.GetString(key, defaultValue);
        }

        internal void SetString(string key, string value)
        {
            this.meta.SetString(key, value);
        }

        internal int GetNumber(string key, int defaultValue = 0)
        {
            return this.meta.GetNumber(key, defaultValue);
        }

        internal void SetNumber(string key, int value)
        {
            this.meta.SetNumber(key, value);
        }

        public override string ToString()
        {
            return this.Name;
        }

        public override bool Equals(object obj)
        {
            return (obj as Pack)?.Id == this.Id;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
