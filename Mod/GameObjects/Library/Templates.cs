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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.GameObjects.Spawners.Packs;

namespace DVLightSniper.Mod.GameObjects.Library
{
    internal abstract class Templates<TStorage, TTemplate> where TStorage : TemplateStorage<TTemplate>, new() where TTemplate : Template
    {
        internal static readonly TTemplate CANCEL = Templates<TStorage, TTemplate>.CreateCancel();

        private static TTemplate CreateCancel()
        {
            try
            {
                ConstructorInfo ctor = typeof(TTemplate).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(string) }, null);
                return (TTemplate)ctor.Invoke(new object[] { "CANCEL", "CANCEL" });
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected readonly TStorage mainTemplates;

        protected readonly List<TStorage> templateStores = new List<TStorage>();

        protected readonly List<TTemplate> templates = new List<TTemplate>();

        protected int selectedIndex;

        public int SelectedIndex
        {
            get
            {
                return this.selectedIndex;
            }
            set
            {
                this.selectedIndex = Math.Min(Math.Max(value, 0), this.MaxIndex);
            }
        }

        internal void Next()
        {
            this.selectedIndex++;
            if (this.selectedIndex > this.MaxIndex)
            {
                this.selectedIndex = 0;
            }
        }

        internal void Previous()
        {
            this.selectedIndex--;
            if (this.selectedIndex < 0)
            {
                this.selectedIndex = this.MaxIndex;
            }
        }

        internal TTemplate SelectedTemplate
        {
            get { return this.SelectedIndex < this.templates.Count ? this.templates[this.SelectedIndex] : Templates<TStorage, TTemplate>.CANCEL; }
        }

        internal bool IsCancel { get { return this.SelectedIndex >= this.templates.Count; } }
        protected int MaxIndex { get { return this.templates.Count; } }

        protected Templates(params string[] path)
        {
            this.mainTemplates = TemplateStorage<TTemplate>.LoadTemplates<TStorage>(path);
            this.Register(this.mainTemplates);

            foreach (Pack pack in PackLoader.Packs)
            {
                string relativePath = Path.Combine(path).RelativeToBaseDir();
                Stream packTemplates = pack.OpenStream(relativePath);
                this.Register(TemplateStorage<TTemplate>.ReadTemplates<TStorage>(packTemplates, pack.Name + ":" + relativePath));
            }

            this.Update();
        }

        private void Register(TStorage templateStore)
        {
            if (templateStore != null)
            {
                this.templateStores.Add(templateStore);
                templateStore.Changed += this.Update;
            }
        }

        private void Update()
        {
            this.templates.Clear();
            foreach (TStorage templateStore in this.templateStores)
            {
                this.templates.AddRange(templateStore.templates);
            }
        }

        internal void Set(TTemplate template)
        {
            this.mainTemplates.Set(template);
        }

        internal void Save()
        {
            this.mainTemplates.Save();
        }
    }
}
