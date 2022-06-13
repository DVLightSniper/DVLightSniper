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
using DVLightSniper.Mod.GameObjects.Library.Assets;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

using ISpriteOwner = DVLightSniper.Mod.Components.SpriteComponent.ISpriteOwner;
using UpdateTicket = DVLightSniper.Mod.GameObjects.SpawnerController.UpdateTicket;

using Newtonsoft.Json;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Spawners
{
    /// <summary>
    /// A spawner which spawns a prefab onto an existing mesh in the world to "decorate" it. We
    /// mainly use this to apply lit windows to existing building meshes in the world. When spawning
    /// objects via the comms radio the user does not have any way to change the transform and the
    /// decoration is always applied with a local offset of 0,0,0 and zero rotation. Setting these
    /// offsets in the json is supported however.
    /// 
    /// Normal decorations are spawned in the world and simply parented to the target transform. It
    /// is also possible to spawn decorations which copy materials from themselves onto the target
    /// meshes, using MaterialAssigments.
    /// 
    /// Decorations also manage dutycycles in the same way that lights do, with an attached
    /// DecorationComponent forming the link between the spawned object and this spawner.
    /// 
    /// Since it doesn't make sense to have 2 copies of the same decoration on a single target, we
    /// also attach a DecorationsComponent to the target object which keeps track of applied
    /// decorations in order to manage exclusivity.
    /// </summary>
    [DataContract]
    internal class DecorationSpawner : Spawner
    {
        /// <summary>
        /// Number of times to attempt to load the asset before giving up
        /// </summary>
        private const int MAX_LOAD_ATTEMPTS = 4;

        /// <summary>
        /// Number of seconds to wait between load attempts when loading fails
        /// </summary>
        private const int RELOAD_ATTEMPT_TIME_SECONDS = 60;

        private string name;

        /// <summary>
        /// The name for this decoration
        /// </summary>
        internal override string Name
        {
            get
            {
                return this.name ?? (this.name = this.Group.Prefix + this.GlobalPrefix + "Decoration" + this.Group.NextDecorationId);
            }
        }

        /// <summary>
        /// The 
        /// </summary>
        [DataMember(Name = "id", Order = 4)]
        internal string Id { get; private set; }

        /// <summary>
        /// Stored properties for this decoration
        /// </summary>
        [DataMember(Name = "properties", Order = 5)]
        private MeshProperties properties;

        /// <summary>
        /// Properties for this decoration
        /// </summary>
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

        /// <summary>
        /// The duty cycle for this decoration
        /// </summary>
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

        /// <summary>
        /// Whether the decoration is on, determined by the duty cycle
        /// </summary>
        internal bool On { get; private set; }

        /// <summary>
        /// Keep track of failed asset loads so we can retry a few times and give up if necessary
        /// </summary>
        private DateTime lastLoadAttempt;
        private int failedLoadAttempts;

        /// <summary>
        /// Flag which indicates we tried to load the asset but were unsuccessful
        /// </summary>
        internal bool MissingResource { get; private set; }

        /// <summary>
        /// We attempted to apply the decoration but it was already applied by another spawner. This
        /// can happen if the same spawner exists in multiple groups.
        /// </summary>
        internal bool Duplicate { get; private set; }

        /// <summary>
        /// Unlike other spawners, we keep track of the parent gameobject because we want to manage
        /// the DecorationsComponent to register and unregister ourself
        /// </summary>
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

        /// <summary>
        /// The spawner successfully located the parent transform, attempt to spawn the decoration
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="parent"></param>
        protected override bool Spawn(UpdateTicket ticket, GameObject parent)
        {
            if ((this.MissingResource || this.Duplicate) && (this.failedLoadAttempts > DecorationSpawner.MAX_LOAD_ATTEMPTS || (DateTime.UtcNow - this.lastLoadAttempt).TotalSeconds < DecorationSpawner.RELOAD_ATTEMPT_TIME_SECONDS))
            {
                return false;
            }

            this.Parent = parent;

            DecorationsComponent decorations = this.Parent.GetOrCreate<DecorationsComponent>();
            if (decorations.Has(this.Id) != DecorationsComponent.Match.None)
            {
                // Decoration is already applied
                this.Duplicate = true;
                return false;
            }

            // Register ourself with the parent
            decorations.Add(this);

            GameObject decorationHolder = this.GameObject;
            if (decorationHolder == null)
            {
                this.lastLoadAttempt = DateTime.UtcNow;

                GameObject meshTemplate = AssetLoader.Meshes.Load<GameObject>(this.Properties.AssetBundleName, this.Properties.AssetName);
                if (meshTemplate == null)
                {
                    this.MissingResource = true;
                    this.failedLoadAttempts++;
                    return false;
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
            return true;
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

        public override void OnSaving()
        {
            this.Properties?.OnSaving(false);
        }

        public override string ToString()
        {
            return $"Decoration(Name={this.Name} Properties={this.Properties})";
        }
    }

}
