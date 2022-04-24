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
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Formatting = Newtonsoft.Json.Formatting;

namespace DVLightSniper.Mod.Storage
{
    /// <summary>
    /// Base for objects which are serialised to JSON
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal abstract class JsonStorage
    {
        /// <summary>
        /// Custom serialiser for colours which writes them as hex strings instead of separate
        /// properties
        /// </summary>
        internal class ColorConverter : JsonConverter<Color>
        {
            private static readonly Regex REGEX = new Regex(@"^#?(?<r>[0-9A-F]{2})(?<g>[0-9A-F]{2})(?<b>[0-9A-F]{2})(?<a>[0-9A-F]{2})?$", RegexOptions.IgnoreCase);

            public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
            {
                int r = (int)Math.Round(value.r * 255.0);
                int g = (int)Math.Round(value.g * 255.0);
                int b = (int)Math.Round(value.b * 255.0);
                int a = (int)Math.Round(value.a * 255.0);
                string alpha = a == 255 ? "" : $"{a:X2}";
                writer.WriteValue($"#{r:X2}{g:X2}{b:X2}{alpha}");
            }

            public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Match match = ColorConverter.REGEX.Match(reader.Value.ToString());
                if (!match.Success)
                {
                    return existingValue;
                }

                int.TryParse(match.Groups["r"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo, out int ir);
                int.TryParse(match.Groups["g"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo, out int ig);
                int.TryParse(match.Groups["b"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo, out int ib);

                if (!int.TryParse(match.Groups["a"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo, out int ia))
                {
                    ia = 255;
                }
                float r = (float)Math.Min(ir / 255.0, 1.0);
                float g = (float)Math.Min(ig / 255.0, 1.0);
                float b = (float)Math.Min(ib / 255.0, 1.0);
                float a = (float)Math.Min(ia / 255.0, 1.0);
                return new Color(r, g, b, a);
            }
        }

        /// <summary>
        /// Custom serialiser for vector which writes vectors as 3 comma-separated values in
        /// parentheses instead of 3 separate properties
        /// </summary>
        internal class VectorConverter : JsonConverter<Vector3>
        {
            private static readonly Regex REGEX = new Regex(@"^\(\s*(?<x>-?[0-9]+(\.[0-9]+)?(E-?[0-9]+)?)\s*,\s*(?<y>-?[0-9]+(\.[0-9]+)?(E-?[0-9]+)?)\s*,\s*(?<z>-?[0-9]+(\.[0-9]+)?(E-?[0-9]+)?)\s*\)$");

            public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
            {
                writer.WriteValue($"({value.x:r},{value.y:r},{value.z:r})");
            }

            public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Match match = VectorConverter.REGEX.Match(reader.Value.ToString());
                if (!match.Success)
                {
                    return existingValue;
                }

                float.TryParse(match.Groups["x"].Value, out float x);
                float.TryParse(match.Groups["y"].Value, out float y);
                float.TryParse(match.Groups["z"].Value, out float z);
                return new Vector3(x, y, z);
            }
        }

        /// <summary>
        /// Custom serialiser for quaternion which writes them as 4 comma-separated values in
        /// parentheses instead of 4 separate properties
        /// </summary>
        internal class QuaternionConverter : JsonConverter<Quaternion>
        {
            private static readonly Regex REGEX = new Regex(@"^\(\s*(?<x>-?[0-9]+(\.[0-9]+)?)\s*,\s*(?<y>-?[0-9]+(\.[0-9]+)?)\s*,\s*(?<z>-?[0-9]+(\.[0-9]+)?)\s*,\s*(?<w>-?[0-9]+(\.[0-9]+)?)\s*\)$");

            public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
            {
                writer.WriteValue($"({value.x:r},{value.y:r},{value.z:r},{value.w:r})");
            }

            public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                Match match = QuaternionConverter.REGEX.Match(reader.Value.ToString());
                if (!match.Success)
                {
                    return existingValue;
                }

                float.TryParse(match.Groups["x"].Value, out float x);
                float.TryParse(match.Groups["y"].Value, out float y);
                float.TryParse(match.Groups["z"].Value, out float z);
                float.TryParse(match.Groups["w"].Value, out float w);
                return new Quaternion(x, y, z, w);
            }
        }

        private static readonly JsonSerializerSettings SETTINGS = new JsonSerializerSettings()
        {
            Formatting = Formatting.Indented,
            Converters =
            {
                new ColorConverter(),
                new VectorConverter(),
                new QuaternionConverter(),
                new StringEnumConverter()
            }
        };

        internal bool ReadOnly { get; private set; }

        internal string FileName { get; set; }

        internal virtual bool IsEmpty { get => false; }

        /// <summary>
        /// Save this object to the file it was loaded from, if the object was loaded from a
        /// resource then the save will not proceed. If the object is empty (determined by IsEmpty
        /// returning true) then the file will instead be deleted.
        /// </summary>
        /// <returns></returns>
        internal bool Save()
        {
            if (this.ReadOnly)
            {
                return false;
            }

            if (this.FileName == null)
            {
                LightSniper.Logger.Error("Attempted to save JSON object {0} but FileName is null", this.GetType().FullName);
                return false;
            }

            if (this.IsEmpty)
            {
                if (File.Exists(this.FileName) && this.OnDeleting())
                {
                    File.Delete(this.FileName);
                }
                return true;
            }

            this.OnSaving();

            try
            {
                Directory.CreateDirectory(new FileInfo(this.FileName).DirectoryName);
                JsonSerializer serializer = JsonSerializer.Create(JsonStorage.SETTINGS);
                using (JsonWriter writer = new JsonTextWriter(new StreamWriter(new FileStream(this.FileName, FileMode.Create))))
                {
                    serializer.Serialize(writer, this);
                }
                this.OnSaved();
                return true;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
                return false;
            }
        }

        protected static T Load<T>(params string[] path) where T : JsonStorage
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
                    T obj = JsonStorage.Read<T>(stream);
                    if (obj != null)
                    {
                        obj.FileName = fileName;
                        obj.OnLoaded();
                    }
                    return obj;
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Error("Error reading JSON from file {0}\n{1}", fileName, e.Message);
                    return null;
                }
            }
        }

        protected static T Read<T>(Stream stream, string fileName) where T : JsonStorage
        {
            try
            {
                T obj = JsonStorage.Read<T>(stream);
                if (obj != null)
                {
                    obj.FileName = fileName;
                    obj.ReadOnly = true;
                    obj.OnLoaded();
                }
                return obj;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error("Error reading JSON from stream resource {0}\n{1}", fileName, e.Message);
                return null;
            }
        }

        private static T Read<T>(Stream stream) where T : JsonStorage
        {
            if (stream == null)
            {
                return null;
            }

            JsonSerializer serializer = JsonSerializer.Create(JsonStorage.SETTINGS);
            using (JsonReader reader = new JsonTextReader(new StreamReader(stream)))
            {
                return serializer.Deserialize<T>(reader);
            }
        }

        /// <summary>
        /// Called before an empty object's file is deleted from disk. Return false to cancel the
        /// deletion.
        /// </summary>
        protected virtual bool OnDeleting()
        {
            return true;
        }

        /// <summary>
        /// Called before a non-empty object is saved
        /// </summary>
        protected virtual void OnSaving()
        {
        }

        /// <summary>
        /// Called when a non-empty object has been saved
        /// </summary>
        protected virtual void OnSaved()
        {
        }

        /// <summary>
        /// Called when an object is loaded
        /// </summary>
        protected virtual void OnLoaded()
        {
        }
    }
}
