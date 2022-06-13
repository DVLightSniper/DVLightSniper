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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Storage;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// Since packs are read-only by design, any information we want to store about them needs to be
    /// external, hence we use this storage to keep such information
    /// </summary>
    [DataContract]
    internal class Metadata : JsonStorage
    {
        /// <summary>
        /// Key/value store used to keep external metadata for packs
        /// </summary>
        [DataContract]
        internal class Data
        {
            internal event Action Changed;

            [DataMember(Name = "flags", Order = 0, EmitDefaultValue = false)]
            private Dictionary<string, bool> flags = new Dictionary<string, bool>();

            [DataMember(Name = "strings", Order = 1, EmitDefaultValue = false)]
            private Dictionary<string, string> strings = new Dictionary<string, string>();

            [DataMember(Name = "numbers", Order = 2, EmitDefaultValue = false)]
            private Dictionary<string, int> numbers = new Dictionary<string, int>();

            internal bool GetFlag(string key, bool defaultValue = false)
            {
                return this.flags.ContainsKey(key) ? this.flags[key] : defaultValue;
            }

            internal void SetFlag(string key, bool value)
            {
                this.flags[key] = value;
                this.Changed?.Invoke();
            }

            internal string GetString(string key, string defaultValue = "")
            {
                return this.strings.ContainsKey(key) ? this.strings[key] : defaultValue;
            }

            internal void SetString(string key, string value)
            {
                this.strings[key] = value;
                this.Changed?.Invoke();
            }

            internal int GetNumber(string key, int defaultValue = 0)
            {
                return this.numbers.ContainsKey(key) ? this.numbers[key] : defaultValue;
            }

            internal void SetNumber(string key, int value)
            {
                this.numbers[key] = value;
                this.Changed?.Invoke();
            }
        }

        [DataMember(Name = "meta", Order = 0)]
        private Dictionary<string, Data> meta = new Dictionary<string, Data>();

        internal static Metadata Load(params string[] path)
        {
            return JsonStorage.Load<Metadata>(path) ?? new Metadata() { FileName = Path.Combine(path) };
        }

        internal Data Get(Pack pack)
        {
            if (!this.meta.ContainsKey(pack.Id))
            {
                Data meta = this.meta[pack.Id] = new Data();
                meta.Changed += this.OnChanged;
                return meta;
            }
            return this.meta[pack.Id];
        }

        protected override void OnLoaded()
        {
            foreach (Data packMeta in this.meta.Values)
            {
                packMeta.Changed += this.OnChanged;
            }
        }

        private void OnChanged()
        {
            this.Save();
        }
    }
}
