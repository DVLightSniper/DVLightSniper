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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// Properties defining a sniped mesh
    /// </summary>
    [DataContract]
    internal class MeshProperties : IProperties
    {
        public event Action<string> Changed;

        /// <summary>
        /// Filename for the asset bundle to load from
        /// </summary>
        [DataMember(Name = "file", Order = 0)]
        internal string AssetBundleName { get; set; }

        /// <summary>
        /// Name of the prefab within the asset bundle
        /// </summary>
        [DataMember(Name = "mesh", Order = 1)]
        internal string AssetName { get; set; }

        /// <summary>
        /// ID of a component to attach to spawned meshes
        /// </summary>
        [DataMember(Name = "componentId", Order = 2, EmitDefaultValue = false)]
        internal string ComponentId { get; set; }

        /// <summary>
        /// Settings for the attached component
        /// </summary>
        [DataMember(Name = "componentProperties", Order = 3, EmitDefaultValue = false)]
        private Dictionary<string, string> componentProperties;

        internal Dictionary<string, string> ComponentProperties
        {
            get
            {
                return this.componentProperties ?? (this.componentProperties = new Dictionary<string, string>());
            }
        }

        /// <summary>
        /// Material assignments to apply
        /// </summary>
        [DataMember(Name = "assignMaterials", Order = 4, EmitDefaultValue = false)]
        private MaterialAssignments materialAssignments;

        internal MaterialAssignments MaterialAssignments
        {
            get
            {
                return this.materialAssignments ?? (this.materialAssignments = new MaterialAssignments());
            }
            set
            {
                this.materialAssignments = value;
            }
        }

        [JsonConstructor]
        internal MeshProperties(string assetBundleName, string assetName)
        {
            this.AssetBundleName = assetBundleName;
            this.AssetName = assetName;
        }

        internal MeshProperties Clone()
        {
            MeshProperties newProperties = new MeshProperties(this.AssetBundleName, this.AssetName)
            {
                ComponentId = this.ComponentId,
                materialAssignments = this.materialAssignments,
                componentProperties = this.componentProperties != null ? new Dictionary<string, string>(this.componentProperties) : null
            };
            return newProperties;
        }

        public MeshProperties Expand()
        {
            if (this.componentProperties == null)
            {
                return this;
            }

            Regex propertyReplacement = new Regex(@"\$\{(?<property>[a-z_\-]+\.[a-z0-9_\-]+)\}", RegexOptions.IgnoreCase);
            foreach (string key in new List<string>(this.componentProperties.Keys))
            {
                string value = this.componentProperties[key];
                foreach (Match match in propertyReplacement.Matches(value).Cast<Match>().Reverse())
                {
                    string property = GlobalProperties.Instance.Get(match.Groups["property"].Value, "");
                    value = value.Substring(0, match.Index) + property + value.Substring(match.Index + match.Length);
                }
                this.componentProperties[key] = value;
            }

            return this;
        }

        internal string GetComponentProperty(string key, string defaultValue = "")
        {
            return this.componentProperties != null && this.componentProperties.ContainsKey(key) ? this.componentProperties[key] : GlobalProperties.Instance.Get(key, defaultValue);
        }

        internal int GetComponentProperty(string key, int defaultValue = 0)
        {
            string value = this.GetComponentProperty(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(value, out int iValue) ? iValue : defaultValue;
        }

        internal float GetComponentProperty(string key, float defaultValue = 0.0F)
        {
            string value = this.GetComponentProperty(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            return float.TryParse(value, out float fValue) ? fValue : defaultValue;
        }

        internal bool GetComponentProperty(string key, bool defaultValue = false)
        {
            string value = this.GetComponentProperty(key, defaultValue.ToString(CultureInfo.InvariantCulture));
            return bool.TryParse(value, out bool bValue) ? bValue : defaultValue;
        }

        string IProperties.Get(string key, string defaultValue)
        {
            return this.GetComponentProperty(key, defaultValue);
        }

        int IProperties.Get(string key, int defaultValue)
        {
            return this.GetComponentProperty(key, defaultValue);
        }

        float IProperties.Get(string key, float defaultValue)
        {
            return this.GetComponentProperty(key, defaultValue);
        }

        bool IProperties.Get(string key, bool defaultValue)
        {
            return this.GetComponentProperty(key, defaultValue);
        }

        void IProperties.SetDefault(string key, string value)
        {
            // not supported
        }

        void IProperties.Set(string key, string value)
        {
            if (this.componentProperties == null)
            {
                if (value == null)
                {
                    return;
                }

                this.componentProperties = new Dictionary<string, string>();
            }

            string oldValue = this.componentProperties.ContainsKey(key) ? this.componentProperties[key] : null;
            if (value != null)
            {
                this.componentProperties[key] = value;
            }
            else
            {
                this.componentProperties.Remove(key);
            }
            if (oldValue != value)
            {
                this.Changed?.Invoke(key);
            }
        }

        void IProperties.Set(string key, int value)
        {
            ((IProperties)this).Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        void IProperties.Set(string key, bool value)
        {
            ((IProperties)this).Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        internal MeshProperties HideSourcePrefab(bool value)
        {
            this.MaterialAssignments.HideSourcePrefab = value;
            return this;
        }

        internal MeshProperties WithComponent(string componentId)
        {
            this.ComponentId = componentId;
            return this;
        }

        internal MeshProperties WithComponentProperty(string key, string value)
        {
            this.ComponentProperties[key] = value;
            return this;
        }

        internal MeshProperties WithMaterialAssignment(string rendererName, int from = 0)
        {
            this.MaterialAssignments.Add(new MaterialAssignment(rendererName, from));
            return this;
        }

        internal MeshProperties WithRandomAlpha(float randomAlpha)
        {
            this.MaterialAssignments.RandomAlpha = randomAlpha;
            return this;
        }

        internal void OnSaving(bool clearMaterialAssignments)
        {
            if (this.materialAssignments?.Count == 0 || clearMaterialAssignments)
            {
                this.materialAssignments = null;
            }

            if (string.IsNullOrEmpty(this.ComponentId))
            {
                this.ComponentId = null;
                this.componentProperties = null;
            }
        }

        public override string ToString()
        {
            return $"MeshProperties(File={this.AssetBundleName} Mesh={this.AssetName})";
        }
    }

}
