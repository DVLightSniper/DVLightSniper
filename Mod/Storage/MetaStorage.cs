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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DVLightSniper.Mod.Storage
{
    /// <summary>
    /// Full meta files are YAML, but including an entire YAML parser for the tiny subset I want to
    /// use is kind of daft, so this just parses basic meta files as dictionary of scalars.
    /// </summary>
    public class MetaStorage
    {
        public class MetaValue
        {
            public static readonly MetaValue Empty = new MetaValue("");

            public string Value { get; }

            public MetaValue(string value)
            {
                this.Value = value;
            }

            public int ValueOrDefault(int defaultValue)
            {
                return int.TryParse(this.Value, out int i) ? i : defaultValue;
            }

            public float ValueOrDefault(float defaultValue)
            {
                return float.TryParse(this.Value, out float f) ? f : defaultValue;
            }

            public string ValueOrDefault(string defaultValue)
            {
                return !string.IsNullOrEmpty(this.Value) ? this.Value : defaultValue;
            }

            public static implicit operator string(MetaValue v)
            {
                return v.Value;
            }

            public static implicit operator int(MetaValue v)
            {
                return v.ValueOrDefault(0);
            }

            public static implicit operator float(MetaValue v)
            {
                return v.ValueOrDefault(0.0F);
            }

            public static implicit operator MetaValue(string value)
            {
                return new MetaValue(value);
            }
        }

        private static readonly Regex REGEX_KV = new Regex(@"^(?<key>[^:]+):(?<value>.+)$");

        private readonly Dictionary<string, MetaValue> values = new Dictionary<string, MetaValue>();

        private readonly bool mutable;

        private MetaStorage()
        {
            this.mutable = false;
        }

        public MetaStorage(IEnumerable<(string, string)> values)
        {
            foreach ((string key, string value) in values)
            {
                this.values[key] = value;
            }
            this.mutable = true;
        }

        public MetaValue this[string key]
        {
            get
            {
                return this.values.ContainsKey(key) ? this.values[key] : MetaValue.Empty;
            }
        }

        public void Add(string key, string value)
        {
            if (!this.mutable)
            {
                throw new InvalidOperationException("MetaStorage is not mutable");
            }

            this.values[key] = new MetaValue(value);
        }

        public void Set(string key, string value)
        {
            if (!this.values.ContainsKey(key))
            {
                throw new ArgumentException("MetaStorage does not contain the specified key: " + key);
            }

            this.Add(key, value);
        }

        public static MetaStorage Load(params string[] path)
        {
            string fileName = Path.Combine(path);
            if (!File.Exists(fileName))
            {
                return null;
            }

            using (FileStream stream = new FileStream(fileName, FileMode.Open))
            {
                try
                {
                    return MetaStorage.Read(stream);
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Error("Error reading meta from file {0}\n{1}", fileName, e.Message);
                    return null;
                }
            }
        }

        public static MetaStorage Read(Stream stream)
        {
            if (stream == null)
            {
                return null;
            }

            MetaStorage meta = new MetaStorage();
            using (StreamReader reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#") || line.StartsWith(" ")) // ignore comments and dicts
                    {
                        continue;
                    }

                    Match match = MetaStorage.REGEX_KV.Match(line);
                    if (!match.Success)
                    {
                        continue;
                    }

                    string key = match.Groups["key"].Value.Unquote();
                    string value = match.Groups["value"].Value.Unquote();
                    meta.values[key] = new MetaValue(value);
                }
            }

            return meta;
        }
    }
}
