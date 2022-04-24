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
using System.Runtime.Serialization;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.Storage;
using DVLightSniper.Mod.Util;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Library
{
    /// <summary>
    /// A store of templates backed by JSON
    /// </summary>
    [DataContract]
    internal class TemplateStorage<TTemplate> : JsonStorage where TTemplate : Template
    {
        internal event Action Changed;

        [DataMember(Name = "version", Order = 0)]
        internal int Version { get; set; }

        [DataMember(Name = "templates", Order = 1)]
        internal readonly List<TTemplate> templates = new List<TTemplate>();

        internal static TStorage LoadTemplates<TStorage>(params string[] path) where TStorage : TemplateStorage<TTemplate>, new()
        {
            TStorage templates = JsonStorage.Load<TStorage>(path);
            if (templates?.templates == null || templates.templates.Count < 1)
            {
                templates = new TStorage { FileName = Path.Combine(path) };
            }

            templates.AddDefaults();
            return templates;
        }

        internal static TStorage ReadTemplates<TStorage>(Stream stream, string fileName) where TStorage : TemplateStorage<TTemplate>
        {
            return JsonStorage.Read<TStorage>(stream, fileName);
        }

        protected virtual void AddDefaults()
        {
        }

        protected override void OnLoaded()
        {
            foreach (TTemplate template in this.templates)
            {
                template.ReadOnly = this.ReadOnly;
            }
        }

        internal virtual void Add(TTemplate template)
        {
            if (this.templates.Find(existingTemplate => existingTemplate.Name == template.Name) != null)
            {
                return;
            }
            template.ReadOnly = this.ReadOnly;
            this.templates.Add(template);
            this.Changed?.Invoke();
        }

        internal virtual void Set(TTemplate template)
        {
            this.templates.RemoveAll(existingTemplate => existingTemplate.Name == template.Name);
            template.ReadOnly = this.ReadOnly;
            this.templates.Add(template);
            this.Changed?.Invoke();
        }

        protected virtual void OnChanged()
        {
            this.Changed?.Invoke();
        }
    }
}
