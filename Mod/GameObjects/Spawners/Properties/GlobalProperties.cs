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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Storage;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    [DataContract]
    internal class GlobalProperties : JsonStorage, IProperties
    {
        private static GlobalProperties instance;

        internal static GlobalProperties Instance
        {
            get
            {
                if (GlobalProperties.instance == null)
                {
                    GlobalProperties.instance = JsonStorage.Load<GlobalProperties>(LightSniper.ResourcesPath, "global_properties.json");
                    if (GlobalProperties.instance == null)
                    {
                        GlobalProperties.instance = new GlobalProperties()
                        {
                            FileName = Path.Combine(LightSniper.ResourcesPath, "global_properties.json")
                        };
                    }
                }
                return GlobalProperties.instance;
            }
        }

        public event Action<string> Changed;

        [DataMember(Name = "properties", Order = 0)]
        private Dictionary<string, string> properties = new Dictionary<string, string>();

        public IEnumerable<KeyValuePair<string, string>> GetAll()
        {
            return this.properties.ToList();
        }

        public IEnumerable<KeyValuePair<string, string>> GetAll(string group)
        {
            return this.properties.Where(kv => kv.Key.StartsWith(group + ".")).ToList();
        }

        public string Get(string key, string defaultValue = "")
        {
            return this.properties != null && this.properties.ContainsKey(key) ? this.properties[key] : defaultValue;
        }

        public int Get(string key, int defaultValue = 0)
        {
            string value = this.Get(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(value, out int iValue) ? iValue : defaultValue;
        }

        public float Get(string key, float defaultValue = 0.0F)
        {
            string value = this.Get(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            return float.TryParse(value, out float fValue) ? fValue : defaultValue;
        }

        public bool Get(string key, bool defaultValue = false)
        {
            string value = this.Get(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            return bool.TryParse(value, out bool bValue) ? bValue : defaultValue;
        }

        public void SetDefault(string key, string value)
        {
            if (!this.properties.ContainsKey(key))
            {
                this.properties[key] = value;
                this.Save();
            }
        }

        public void Set(string key, string value)
        {
            string oldValue = this.properties.ContainsKey(key) ? this.properties[key] : null;
            if (value != null)
            {
                this.properties[key] = value;
            }
            else
            {
                this.properties.Remove(key);
            }
            this.Save();
            if (oldValue != value)
            {
                this.Changed?.Invoke(key);
            }
        }

        public void Set(string key, int value)
        {
            this.Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void Set(string key, bool value)
        {
            this.Set(key, value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
