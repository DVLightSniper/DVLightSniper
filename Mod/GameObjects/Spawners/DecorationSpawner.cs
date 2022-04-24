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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;

using DV;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Library;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

using ISpriteOwner = DVLightSniper.Mod.Components.SpriteComponent.ISpriteOwner;
using UpdateTicket = DVLightSniper.Mod.GameObjects.SpawnerController.UpdateTicket;

using Newtonsoft.Json;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Spawners
{
    [DataContract]
    internal class DecorationSpawner : Spawner, ISpriteOwner
    {
        private string name;

        internal override string Name
        {
            get
            {
                return this.name ?? (this.name = this.Group.Prefix + this.GlobalPrefix + "Decoration" + this.Group.NextDecorationId);
            }
        }

        [DataMember(Name = "id", Order = 4)]
        internal string Id { get; private set; }

        [DataMember(Name = "properties", Order = 5)]
        private MeshProperties properties;

        internal MeshProperties Properties
        {
            get
            {
                return this.properties;
            }

            set
            {
                this.properties = value;
                this.Apply();
                this.Group?.Save();
            }
        }

        [DataMember(Name = "dutyCycle", Order = 6)]
        private string dutyCycleDef;

        private DutyCycle dutyCycle;

        internal DutyCycle DutyCycle
        {
            get
            {
                return this.dutyCycle ?? (this.dutyCycle = DutyCycle.Parse(this.dutyCycleDef));
            }
            set
            {
                this.dutyCycle = (value ?? DutyCycle.ALWAYS);
                this.dutyCycleDef = this.dutyCycle.ToString();
            }
        }

        internal bool On { get; private set; }

        public bool ShowSprites { get { return this.Selected || this.Region.Controller.ShowSprites; } }

        private int spriteIndex;

        public int SpriteIndex
        {
            get
            {
                return this.spriteIndex + (this.On ? 0 : 8);
            }
            set
            {
                this.spriteIndex = value;
            }
        }

        private DateTime lastLoadAttempt;
        private int failedLoadAttempts;

        internal bool MissingResource { get; private set; }

        internal bool Duplicate { get; private set; }

        internal GameObject Parent { get; private set; }

        [JsonConstructor]
        internal DecorationSpawner(string parentPath, string id, MeshProperties properties)
            : base(parentPath, Vector3.zero, new Quaternion(0.0F, 0.0F, 0.0F, 0.0F))
        {
            this.Id = id;
            this.properties = properties;
        }

        protected override void Tick(UpdateTicket ticket, bool visible)
        {
            ticket.TickedDecoration(visible);
            if (this.dutyCycle != null)
            {
                this.dutyCycle.Update();
                this.On = this.dutyCycle.On;
            }
        }

        protected override void Spawn(UpdateTicket ticket, GameObject parent)
        {
            if ((this.MissingResource || this.Duplicate) && (this.failedLoadAttempts > 4 || (DateTime.Now - this.lastLoadAttempt).TotalSeconds < 60))
            {
                return;
            }

            this.Parent = parent;

            DecorationsComponent decorations = this.Parent.GetOrCreate<DecorationsComponent>();
            if (decorations.Has(this.Id) != DecorationsComponent.Match.None)
            {
                this.Duplicate = true;
                return;
            }

            decorations.Add(this);

            GameObject decorationHolder = this.GameObject;
            if (decorationHolder == null)
            {
                this.lastLoadAttempt = DateTime.Now;

                GameObject meshTemplate = AssetLoader.Meshes.Load<GameObject>(this.Properties.AssetBundleName, this.properties.AssetName);
                if (meshTemplate == null)
                {
                    this.MissingResource = true;
                    this.failedLoadAttempts++;
                    return;
                }

                this.MissingResource = false;
                this.failedLoadAttempts = 0;

                decorationHolder = Object.Instantiate(meshTemplate);
                decorationHolder.name = this.Name;
                decorationHolder.transform.parent = parent.transform;
                decorationHolder.transform.position = parent.transform.position;
                decorationHolder.transform.rotation = parent.transform.rotation;
                decorationHolder.transform.localPosition += this.LocalPosition;

                DecorationComponent behaviour = decorationHolder.GetComponent<DecorationComponent>();
                if (behaviour == null)
                {
                    behaviour = decorationHolder.AddComponent<DecorationComponent>();
                    behaviour.Spawner = this;
                    behaviour.OnDestroyed += this.OnDecorationDestroyed;
                }

                if (this.Properties?.MaterialAssignments?.Count > 0)
                {
                    parent.GetOrCreate<MeshHighlightComponent>();
                    parent.GetOrCreate<MaterialAssignmentComponent>().Add(this, decorationHolder, this.Properties.MaterialAssignments);
                }

                ticket.Mark(2);
            }

            this.Configure(decorationHolder);
            this.Region.Controller.NotifySpawned(this);
            ticket.Mark(1);
        }

        internal override bool Delete()
        {
            if (!this.Editable)
            {
                return false;
            }

            this.RemoveDecorationFromParent();
            base.Delete();
            return true;
        }

        internal void Paint(MeshProperties properties)
        {
        }

        internal override void Configure(GameObject decorationHolder)
        {
            if (decorationHolder == null)
            {
                return;
            }

            this.SetGameObject(decorationHolder);
            this.dutyCycle = this.DutyCycle;
        }

        private void OnDecorationDestroyed(DecorationComponent behaviour)
        {
            this.RemoveDecorationFromParent();
            if (this.Active)
            {
                this.Region.Controller.NotifyDestroyed(this);
            }
            this.SetGameObject(null);
        }

        private void RemoveDecorationFromParent()
        {
            DecorationsComponent decorations = this.Parent?.GetComponent<DecorationsComponent>();
            if (decorations != null)
            {
                decorations.Remove(this);
            }

            MaterialAssignmentComponent materialAssigner = this.Parent?.GetComponent<MaterialAssignmentComponent>();
            if (materialAssigner != null)
            {
                materialAssigner.Remove(this);
            }


            this.Region.Controller.NotifyDestroyed(this);
        }

        public override string ToString()
        {
            return $"Decoration(Name={this.Name} Properties={this.Properties})";
        }
    }

}
