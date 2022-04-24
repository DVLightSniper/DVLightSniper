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
using System.Windows.Forms.VisualStyles;

using DV;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Library;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.Storage;
using DVLightSniper.Mod.Util;

using TMPro;

using UnityEngine;

using TemplateMatch = DVLightSniper.Mod.GameObjects.Library.DecorationTemplate.TemplateMatch;

using ISpriteOwner = DVLightSniper.Mod.Components.SpriteComponent.ISpriteOwner;
using CommsRadioConnection = DVLightSniper.Mod.LightSniper.CommsRadioConnection;
using Console = System.Console;
using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.GameObjects
{
    /// <summary>
    /// The LightSniper comms radio mode, which forms the core of interaction with LightSniper. This
    /// class also stores light, decoration and helper templates
    /// </summary>
    public class CommsRadioLightSniper : MonoBehaviour, ICommsRadioMode, ISpriteOwner
    {
        internal enum State
        {
            Inactive,

            MaybePlace,
                Place,

            MaybeDelete,
                Delete,

            MaybePaint,
                Paint,

            MaybeDecorate,
                Decorate,

            MaybeDesigner,
                DesignSelect,
                DesignCancel,
                    DesignerActive,

            MaybeHelper,
                PlaceHelper,

            Exit
        }

        private CommsRadioConnection radio;

        internal CommsRadioConnection Radio
        {
            get
            {
                return this.radio;
            }

            set
            {
                this.radio?.Release(this);
                this.radio = value;
                this.radio?.Connect(this);
            }
        }

        internal CommsRadioDisplay display;
        internal ArrowLCD lcd;
        internal Transform signalOrigin;
        internal LaserBeamLineRenderer laserBeam;
        internal GameObject holoDisplay;
        internal AudioClip hoverOverSwitch;
        internal AudioClip switchSound;
        internal AudioClip cancelSound;

        private State state = State.Inactive;
        private State lastActiveState = State.MaybePlace;

        private MeshSpawner selectedMesh;

        private MeshSpawner SelectedMesh
        {
            get
            {
                return this.selectedMesh;
            }

            set
            {
                if (this.selectedMesh != null)
                {
                    this.selectedMesh.Selected = false;
                }

                this.selectedMesh = value;

                if (this.selectedMesh != null)
                {
                    this.selectedMesh.Selected = true;
                    this.selectedMesh.SelectionColour = this.state == State.Delete ? Color.red : Color.cyan;
                }
            }
        }

        private LightSpawner selectedLight;

        internal LightSpawner SelectedLight
        {
            get
            {
                return this.selectedLight;
            }

            set
            {
                if (this.selectedLight != null)
                {
                    this.selectedLight.SpriteIndex = 0;
                }

                this.selectedLight = value;

                if (this.selectedLight != null)
                {
                    this.selectedLight.SpriteIndex = this.state == State.Delete ? 2 : this.state == State.DesignerActive ? 1 : 3;
                }
            }
        }

        private Color meshHighlightColor;

        private GameObject highlightedObject;

        internal GameObject HighlightedObject
        {
            get
            {
                return this.highlightedObject;
            }

            set
            {
                if (this.highlightedObject != null)
                {
                    this.highlightedObject.GetComponent<MeshHighlightComponent>().Highlighted = false;
                }

                this.highlightedObject = value;

                if (this.highlightedObject != null)
                {
                    MeshHighlightComponent highlight = this.highlightedObject.GetOrCreate<MeshHighlightComponent>();
                    highlight.HighlightColour = this.meshHighlightColor;
                    highlight.Highlighted = true;
                }
            }
        }

        private Designer designer;

        private int deleteRange = 1;

        private static readonly float[] DELETE_RANGES =
        {
            0, 1.0F, 2.0F, 3.3F, 6.6F, 10.0F
        };

        private static readonly string[] DELETE_RANGE_STRINGS =
        {
            "CANCEL", "RANGE: PINPOINT", "RANGE: CLOSE", "RANGE: MEDIUM", "RANGE: FURTHER", "RANGE: FAR"
        };

        public ButtonBehaviourType ButtonBehaviour { get; private set; } = ButtonBehaviourType.Regular;

        public bool ShowSprites { get; private set;  }
        public int SpriteIndex { get; private set; }

        private readonly System.Random random = new System.Random();

        private int lastSelectedTemplateIndex, lastSelectedDecorationIndex;

        private SpawnerController controller;

        private LightTemplates templates;

        private DecorationTemplates decorationTemplates;

        private readonly Helpers helpers = new Helpers();

        private GameObject target;

        private string decorateActionText = "DECORATE";

        public CommsRadioLightSniper()
        {
            this.ReloadTemplates();
        }

        internal void ReloadTemplates()
        {
            try
            {
                if (CommandLineOption.RESET_TEMPLATES)
                {
                    File.Delete(Path.Combine(LightSniper.Path, "Resources", "templates.lights.json"));
                }

                if (CommandLineOption.RESET_DECORATION_TEMPLATES)
                {
                    File.Delete(Path.Combine(LightSniper.Path, "Resources", "templates.decorations.json"));
                }

            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }

            try
            {
                this.templates = new LightTemplates(LightSniper.Path, "Resources", "templates.lights.json");
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }

            try
            {
                this.decorationTemplates = new DecorationTemplates(LightSniper.Path, "Resources", "templates.decorations.json");
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }

            this.lastSelectedTemplateIndex = 0;
            this.lastSelectedDecorationIndex = 0;
        }

        public void Enable()
        {
            if (this.target == null)
            {
                this.target = new GameObject("LightSniperTargetMarker");

                SpriteComponent sprite = this.target.AddComponent<SpriteComponent>();
                sprite.Owner = this;
                sprite.Billboard = false;
                sprite.OnDestroyed += this.OnSpriteDestroyed;
            }

            this.SelectedLight = null;
            this.designer?.Release();
            this.designer = null;

            this.target.transform.parent = this.transform;

            this.display.content.alignment = TextAlignmentOptions.TopLeft;
            this.display.content.fontSize = 0.037F;

            this.controller = LightSniper.SpawnerController;
            this.UpdateRadio();

            this.holoDisplay.GetComponent<TimedMessageComponent>().SetText(null);
        }

        private void OnSpriteDestroyed(SpriteComponent obj)
        {
            this.target = null;
        }

        public void Disable()
        {
            this.display.content.alignment = TextAlignmentOptions.TopJustified;
            this.display.content.fontSize = 0.04F;

            if (this.state != State.Inactive)
            {
                this.ExitMode();
                this.lastActiveState = this.state;
                this.state = State.Inactive;
            }
            this.controller.ShowSprites = false;
            this.ShowSprites = false;

            if (this.target != null)
            {
                this.target.transform.parent = this.transform;
            }

            this.SelectedLight = null;
            this.SelectedMesh = null;
            this.HighlightedObject = null;
            this.designer?.Release();
            this.designer = null;

            this.helpers.End();

            this.holoDisplay.GetComponent<TimedMessageComponent>().SetText(null);
        }

        public void OverrideSignalOrigin(Transform signalOrigin)
        {
            this.signalOrigin = signalOrigin;
            this.helpers.SignalOrigin = signalOrigin;
        }

        public void OnUpdate()
        {
            if (this.controller == null)
            {
                this.SelectedLight = null;
                this.SelectedMesh = null;
                this.HighlightedObject = null;
                return;
            }

            this.helpers.SignalOrigin = this.signalOrigin;

            this.ShowSprites = false;
            this.SpriteIndex = 0;

            if (this.HandleKeyboardShortcuts())
            {
                return;
            }

            if (this.state != State.Decorate)
            {
                DecorationTemplate.debug.Enabled = false;
            }

            if (this.state == State.Delete || this.state == State.Paint || this.state == State.DesignSelect)
            {
                try
                {
                    float range = this.state == State.Delete ? CommsRadioLightSniper.DELETE_RANGES[this.deleteRange] : 5.0F;
                    this.controller.GetLightUnderCursor(this.signalOrigin, range, out LightSpawner nearestLight, out MeshSpawner hitMesh);
                    this.SelectedLight = nearestLight;
                    string hitName = nearestLight?.Name;
                    if (nearestLight == null)
                    {
                        if (this.state != State.DesignSelect)
                        {
                            this.SelectedMesh = hitMesh;
                            hitName = hitMesh?.Name;
                        }
                    }
                    else if (this.SelectedMesh != null)
                    {
                        this.SelectedMesh = null;
                    }
                    this.UpdateRadio(hitName);
                }
                catch (Exception e)
                {
                    LightSniper.Logger.Error(e);
                }
            }
            else if (this.state == State.Decorate)
            {
                this.HighlightedObject = null;
                this.decorateActionText = "";

                if (!this.decorationTemplates.IsCancel)
                {
                    if (!SpawnerController.CastRay(this.signalOrigin, out RaycastHit hitInfo))
                    {
                        return;
                    }

                    if (hitInfo.transform != null && hitInfo.transform.gameObject.HasUniquePath())
                    {
                        DecorationTemplate template = this.decorationTemplates.SelectedTemplate;
                        TemplateMatch templateMatch = template.MatchRecursive(hitInfo.transform.gameObject);
                        if (templateMatch != null)
                        {
                            DecorationsComponent.Match match = templateMatch.gameObject.GetOrCreate<DecorationsComponent>().Has(template.Id);
                            switch (match)
                            {
                                case DecorationsComponent.Match.None:
                                    this.meshHighlightColor = Color.green;
                                    this.decorateActionText = "ADD";
                                    break;
                                case DecorationsComponent.Match.Group:
                                    this.meshHighlightColor = Color.yellow;
                                    this.decorateActionText = "REPLACE";
                                    break;
                                case DecorationsComponent.Match.Exact:
                                    this.meshHighlightColor = Color.red;
                                    this.decorateActionText = "REMOVE";
                                    break;
                            }
                            this.HighlightedObject = templateMatch.gameObject;
                        }
                        else if (hitInfo.transform?.gameObject != null)
                        {
                            DecorationsComponent decorations = hitInfo.transform.gameObject.GetComponentInParent<DecorationsComponent>();
                            if (decorations != null && decorations.Count > 0)
                            {
                                this.meshHighlightColor = Color.cyan;
                                this.decorateActionText = "<INVALID>";
                                this.HighlightedObject = decorations.gameObject;
                            }
                            else
                            {
                                TemplateMatch otherTemplateMatch = this.decorationTemplates.MatchRecursive(hitInfo.transform.gameObject, template);
                                if (otherTemplateMatch != null)
                                {
                                    this.meshHighlightColor = Color.magenta;
                                    this.decorateActionText = "<INVALID>";
                                    this.HighlightedObject = otherTemplateMatch.gameObject;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                this.SelectedMesh = null;
                this.HighlightedObject = null;
            }

            if (this.state == State.Place)
            {
                this.UpdateRadio(this.templates.SelectedTemplate.Name);
                if (!SpawnerController.CastRay(this.signalOrigin, out RaycastHit hitInfo))
                {
                    if (this.target != null)
                    {
                        this.target.transform.parent = this.transform;
                    }
                }
                else
                {
                    if (this.target != null)
                    {
                        this.UpdateTarget(hitInfo);
                    }

                    this.ShowSprites = true;
                    this.SpriteIndex = 4;
                }
            }
            else if (this.state == State.Decorate)
            {
                this.UpdateRadio(this.decorationTemplates.SelectedTemplate.Name);
            }
            else if (this.state == State.PlaceHelper)
            {
                this.UpdateRadio(this.helpers.SelectedName);
                this.helpers.Update();
            }
            else if (this.target != null)
            {
                this.target.transform.parent = this.transform;
            }

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
            {
                if (this.state == State.Delete || this.state == State.Place)
                {
                    this.controller.Undo();
                }
                else if (this.state == State.PlaceHelper)
                {
                    this.helpers.Undo();
                }
            }
        }

        private bool HandleKeyboardShortcuts()
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.RightShift))
            {
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                return this.GoIntoState(State.MaybeDelete);
            }

            if (Input.GetKeyDown(KeyCode.Insert))
            {
                return this.GoIntoState(State.MaybePlace);
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                return this.GoIntoState(State.MaybePaint);
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                return this.GoIntoState(State.MaybeDesigner);
            }

            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                return this.GoIntoState(State.MaybeDecorate);
            }

            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                return this.GoIntoState(State.MaybeHelper);
            }

            return false;
        }

        private void UpdateTarget(RaycastHit hitInfo)
        {
            this.target.transform.parent = SingletonBehaviour<WorldMover>.Instance.originShiftParent;
            this.target.transform.position = hitInfo.point + (hitInfo.normal * this.templates.SelectedTemplate.SnipeOffset);

            MeshOrientation orientation = hitInfo.GetMeshOrientation();
            if (orientation == MeshOrientation.Wall)
            {
                this.target.transform.rotation = Quaternion.LookRotation(hitInfo.normal, Vector3.up);
                if (LightSniper.Settings.EnableEnhancedPlacer)
                {
                    Quaternion opposite = Quaternion.LookRotation(hitInfo.normal, Vector3.down);
                    if (LightSniper.Settings.Perpendicular.Pressed())
                    {
                        float rotationAmount = LightSniper.Settings.Shift.Pressed() ? -90.0F : 90.0F;
                        this.target.transform.rotation = Quaternion.RotateTowards(this.target.transform.rotation, opposite, rotationAmount);
                    }
                    else if (LightSniper.Settings.Shift.Pressed())
                    {
                        this.target.transform.rotation = opposite;
                    }
                }
            }
            else
            {
                Vector3 floorientation = this.templates.SelectedTemplate.GetMesh(orientation)?.Alignment == MeshTemplate.AlignmentType.AlignToNormal ? hitInfo.normal : Vector3.up;
                this.target.transform.rotation = Quaternion.LookRotation(orientation == MeshOrientation.Floor ? floorientation : Vector3.down, LightSniper.ModifiedPlayerDirection);
            }
        }

        public void OnUse()
        {
            switch (this.state)
            {
                case State.Inactive:
                    this.SetState(this.lastActiveState);
                    break;

                case State.MaybePlace:
                    this.templates.SelectedIndex = this.lastSelectedTemplateIndex;
                    this.SetState(State.Place);
                    break;
                case State.Place:
                    if (this.templates.IsCancel)
                    {
                        this.ExitMode();
                    }
                    else
                    {
                        LightTemplate template = this.templates.SelectedTemplate;
                        int selectedIndex = template.Lights.Count > 1 ? this.random.Next(0, template.Lights.Count) : 0;
                        ActionResult result = this.controller.Snipe(this.signalOrigin, template, selectedIndex);
                        CommsRadioController.PlayAudioFromRadio(result.Success ? this.switchSound : this.cancelSound, this.transform);
                        this.holoDisplay.GetComponent<TimedMessageComponent>().SetText(result.Message, result.Colour);
                        this.lastSelectedTemplateIndex = this.templates.SelectedIndex;
                    }
                    break;

                case State.MaybePaint:
                    this.templates.SelectedIndex = this.lastSelectedTemplateIndex;
                    this.SetState(State.Paint);
                    break;
                case State.Paint:
                    if (this.templates.IsCancel)
                    {
                        this.ExitMode();
                    }
                    else
                    {
                        LightTemplate template = this.templates.SelectedTemplate;
                        if (template.HasLights)
                        {
                            CommsRadioController.PlayAudioFromRadio(this.hoverOverSwitch, this.transform);
                            int selectedIndex = template.Lights.Count > 1 ? this.random.Next(0, template.Lights.Count) : 0;
                            LightProperties lightProperties = template.Lights[selectedIndex];

                            this.controller.Paint(this.SelectedLight, lightProperties);
                            this.controller.Paint(this.SelectedMesh, lightProperties);
                            this.lastSelectedTemplateIndex = this.templates.SelectedIndex;
                        }
                    }
                    break;

                case State.MaybeDecorate:
                    this.decorationTemplates.SelectedIndex = this.lastSelectedDecorationIndex;
                    this.SetState(State.Decorate);
                    break;
                case State.Decorate:
                    if (this.decorationTemplates.IsCancel)
                    {
                        this.ExitMode();
                    }
                    else
                    {
                        DecorationTemplate template = this.decorationTemplates.SelectedTemplate;
                        ActionResult result = this.controller.Snipe(this.signalOrigin, template);
                        CommsRadioController.PlayAudioFromRadio(result.Success ? this.switchSound : this.cancelSound, this.transform);
                        this.holoDisplay.GetComponent<TimedMessageComponent>().SetText(result.Message, result.Colour);
                        this.lastSelectedDecorationIndex = this.decorationTemplates.SelectedIndex;
                    }
                    break;

                case State.MaybeDelete:
                    this.deleteRange = 1;
                    this.SetState(State.Delete);
                    break;
                case State.Delete:
                    if (this.deleteRange == 0)
                    {
                        this.ExitMode();
                    }
                    else
                    {
                        this.controller.Delete(this.SelectedLight);
                        this.controller.Delete(this.SelectedMesh);
                        this.SelectedLight = null;
                    }
                    break;

                case State.MaybeDesigner:
                    this.SetState(State.DesignSelect);
                    break;
                case State.DesignCancel:
                    this.ExitMode();
                    break;
                case State.DesignSelect:
                    if (this.SelectedLight != null)
                    {
                        LightSpawner selected = this.SelectedLight;
                        this.SelectedLight = null;
                        this.designer = new Designer(selected);
                        this.designer.OnExit += this.ExitDesigner;
                        this.controller.ShowSprites = false;
                        this.SetState(State.DesignerActive);
                    }
                    break;

                case State.DesignerActive:
                    this.designer?.OnUse();
                    this.UpdateRadio();
                    break;

                case State.MaybeHelper:
                    this.helpers.Begin();
                    this.SetState(State.PlaceHelper);
                    break;

                case State.PlaceHelper:
                    if (this.helpers.IsCancel)
                    {
                        this.ExitMode();
                    }
                    else
                    {
                        CommsRadioController.PlayAudioFromRadio(this.switchSound, this.transform);
                        this.helpers.Place();
                    }
                    break;

                case State.Exit:
                    this.holoDisplay.GetComponent<TimedMessageComponent>().SetText(null);
                    this.SetState(State.Inactive);
                    break;
            }
        }

        private void ExitMode()
        {
            switch (this.state)
            {
                case State.PlaceHelper:
                    this.helpers.End();
                    this.SetState(State.MaybeHelper);
                    return;
                case State.DesignerActive:
                    this.designer?.Exit();
                    return;
                case State.Exit:
                case State.Inactive:
                case State.Place:
                    this.SetState(State.MaybePlace);
                    return;
                case State.Delete:
                    this.SetState(State.MaybeDelete);
                    return;
                case State.Paint:
                    this.SetState(State.MaybePaint);
                    return;
                case State.Decorate:
                    this.SetState(State.MaybeDecorate);
                    return;
                case State.DesignSelect:
                case State.DesignCancel:
                    this.SetState(State.MaybeDesigner);
                    return;
            }
        }

        public bool ButtonACustomAction()
        {
            switch (this.state)
            {
                case State.MaybePlace:
                    this.SetState(State.MaybeDelete);
                    return true;
                case State.MaybeDelete:
                    this.SetState(State.MaybePaint);
                    return true;
                case State.MaybePaint:
                    this.SetState(State.MaybeDecorate, State.MaybeDesigner, State.MaybeHelper, State.Exit);
                    return true;
                case State.MaybeDecorate:
                    this.SetState(State.MaybeDesigner, State.MaybeHelper, State.Exit);
                    return true;
                case State.MaybeDesigner:
                    this.SetState(State.MaybeHelper, State.Exit);
                    return true;
                case State.MaybeHelper:
                    this.SetState(State.Exit);
                    return true;
                case State.Exit:
                    this.SetState(State.MaybePlace);
                    return true;

                case State.Delete:
                    this.deleteRange++;
                    if (this.deleteRange == CommsRadioLightSniper.DELETE_RANGES.Length)
                    {
                        this.deleteRange = 0;
                    }
                    return true;

                case State.Place:
                case State.Paint:
                    this.templates.Next();
                    this.UpdateRadio(this.templates.SelectedTemplate.Name);
                    return true;

                case State.Decorate:
                    this.decorationTemplates.Next();
                    this.UpdateRadio(this.decorationTemplates.SelectedTemplate.Name);
                    break;

                case State.DesignSelect:
                    this.SetState(State.DesignCancel);
                    break;
                case State.DesignCancel:
                    this.SetState(State.DesignSelect);
                    break;

                case State.DesignerActive:
                    this.designer?.ButtonACustomAction();
                    this.UpdateRadio();
                    return true;

                case State.PlaceHelper:
                    this.helpers.Next();
                    this.UpdateRadio(this.helpers.SelectedName);
                    return true;
            }

            return false;
        }

        public bool ButtonBCustomAction()
        {
            switch (this.state)
            {
                case State.MaybePlace:
                    this.SetState(State.Exit);
                    return true;
                case State.MaybeDelete:
                    this.SetState(State.MaybePlace);
                    return true;
                case State.MaybePaint:
                    this.SetState(State.MaybeDelete);
                    return true;
                case State.MaybeDecorate:
                    this.SetState(State.MaybePaint);
                    return true;
                case State.MaybeDesigner:
                    this.SetState(State.MaybeDecorate, State.MaybePaint);
                    return true;
                case State.MaybeHelper:
                    this.SetState(State.MaybeDesigner, State.MaybeDecorate, State.MaybePaint);
                    return true;
                case State.Exit:
                    this.SetState(State.MaybeHelper, State.MaybeDesigner, State.MaybeDecorate, State.MaybePaint);
                    return true;

                case State.Delete:
                    this.deleteRange--;
                    if (this.deleteRange < 0)
                    {
                        this.deleteRange = CommsRadioLightSniper.DELETE_RANGES.Length - 1;
                    }
                    return true;

                case State.Place:
                case State.Paint:
                    this.templates.Previous();
                    this.UpdateRadio(this.templates.SelectedTemplate.Name);
                    return true;

                case State.Decorate:
                    this.decorationTemplates.Previous();
                    this.UpdateRadio(this.decorationTemplates.SelectedTemplate.Name);
                    break;

                case State.DesignSelect:
                    this.SetState(State.DesignCancel);
                    break;
                case State.DesignCancel:
                    this.SetState(State.DesignSelect);
                    break;

                case State.DesignerActive:
                    this.designer?.ButtonBCustomAction();
                    this.UpdateRadio();
                    return true;

                case State.PlaceHelper:
                    this.helpers.Previous();
                    this.UpdateRadio(this.helpers.SelectedName);
                    return true;
            }

            return false;
        }

        internal bool SaveTemplate(string templateName)
        {
            if (this.designer != null && this.state == State.DesignerActive)
            {
                this.templates.Set(new LightTemplate(templateName).WithLight(this.designer.DesigningLight.Properties.Clone()));
                this.templates.Save();
                return true;
            }

            if (this.SelectedLight != null)
            {
                this.templates.Set(new LightTemplate(templateName).WithLight(this.SelectedLight.Properties.Clone()));
                this.templates.Save();
                return true;
            }

            return false;
        }

        internal void ExitDesigner(Designer designer)
        {
            if (designer == this.designer && this.state == State.DesignerActive)
            {
                this.templates.Set(new LightTemplate("<USER DEFINED>").WithLight(this.designer.DesigningLight.Properties.Clone()));
                this.templates.Save();
                this.designer = null;
                this.SetState(State.DesignSelect);
            }
            designer.Release();
        }

        public void SetStartingDisplay()
        {
            this.UpdateRadio();
        }

        private bool GoIntoState(State state)
        {
            this.ExitMode();
            if (this.CanGotoState(state))
            {
                this.SetState(state);
                this.OnUse();
                return true;
            }

            return false;
        }

        private void SetState(params State[] states)
        {
            this.SelectedLight = null;

            foreach (State state in states)
            {
                if (this.CanGotoState(state))
                {
                    this.state = state;
                    break;
                }
            }

            this.UpdateRadio();
            this.laserBeam.SetBeamColor(this.GetLaserBeamColor());
        }

        private bool CanGotoState(State state)
        {
            switch (state)
            {
                case State.MaybeDecorate:
                    return LightSniper.Settings.EnableDecorateMode;
                case State.MaybeDesigner:
                    return LightSniper.Settings.EnableDesignerMode;
                case State.MaybeHelper:
                    return LightSniper.Settings.EnableHelpers;
                default:
                    return true;
            }
        }

        private void UpdateRadio(string text = null)
        {
            this.controller.ShowSprites = true;

            switch (this.state)
            {
                case State.Inactive:
                    this.lcd.TurnOn(false);
                    this.display.SetDisplay("LIGHTSNIPER", "ENABLE\nLIGHTSNIPER?", "ENABLE");
                    this.ButtonBehaviour = ButtonBehaviourType.Regular;
                    this.controller.ShowSprites = false;
                    break;

                case State.MaybePlace:
                    this.lcd.TurnOn(false);
                    this.display.SetDisplay("CREATE", "CREATE NEW LIGHTS", "");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
                case State.Place:
                    if (this.templates.IsCancel)
                    {
                        this.lcd.TurnOn(true);
                        this.display.SetDisplay("CREATE", "", "BACK");
                    }
                    else
                    {
                        this.lcd.TurnOff();
                        this.display.SetDisplay("CREATE", "TEMPLATE:\n" + text + "\n\nGROUP: " + this.controller.CurrentGroup, "CREATE");
                    }
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.MaybePaint:
                    this.lcd.TurnOn(false);
                    this.display.SetDisplay("PAINT", "PAINT LIGHT\nPROPERTIES", "");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
                case State.Paint:
                    if (this.templates.IsCancel)
                    {
                        this.lcd.TurnOn(true);
                        this.display.SetDisplay("PAINT", "", "BACK");
                    }
                    else
                    {
                        this.lcd.TurnOff();
                        this.display.SetDisplay("PAINT", "TEMPLATE:\n" + this.templates.SelectedTemplate.Name + "\n\n" + (text ?? "<SELECT>"), text == null ? "" : "PAINT");
                    }
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.MaybeDecorate:
                    this.lcd.TurnOn(false);
                    this.display.SetDisplay("DECORATE", "Decorate Meshes", "");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
                case State.Decorate:
                    if (this.decorationTemplates.IsCancel)
                    {
                        this.lcd.TurnOn(true);
                        this.display.SetDisplay("DECORATE", "", "BACK");
                    }
                    else
                    {
                        this.lcd.TurnOff();
                        this.display.SetDisplay("DECORATE", "DECORATION:\n" + text + "\n\nGROUP: " + this.controller.CurrentGroup, this.decorateActionText);
                    }
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.MaybeDelete:
                    this.lcd.TurnOn(false);
                    this.display.SetDisplay("DELETE", "DELETE LIGHTS", "");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
                case State.Delete:
                    this.lcd.TurnOff();
                    if (this.deleteRange == 0)
                    {
                        this.lcd.TurnOn(true);
                        this.display.SetDisplay("DELETE", "", "BACK");
                    }
                    else
                    {
                        this.lcd.TurnOff();
                        this.display.SetDisplay("DELETE", CommsRadioLightSniper.DELETE_RANGE_STRINGS[this.deleteRange] + "\n\n" + (text ?? "<SELECT>"), text == null ? "" : "DELETE");
                    }
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.MaybeDesigner:
                    this.lcd.TurnOn(false);
                    this.display.SetDisplay("DESIGNER", "DESIGN LIGHTS", "");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.DesignSelect:
                    this.lcd.TurnOff();
                    this.display.SetDisplay("DESIGNER", this.SelectedLight?.Name ?? "<SELECT>", this.SelectedLight != null ? "SELECT" : "");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
                case State.DesignCancel:
                    this.lcd.TurnOn(true);
                    this.display.SetDisplay("DESIGNER", "", "BACK");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.DesignerActive:
                    this.designer?.UpdateRadio(this.lcd, this.display);
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.MaybeHelper:
                    this.lcd.TurnOn(false);
                    this.display.SetDisplay("HELPERS", "PLACE HELPERS", "");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.PlaceHelper:
                    if (this.helpers.IsCancel)
                    {
                        this.lcd.TurnOn(true);
                        this.display.SetDisplay("HELPERS", "", "BACK");
                    }
                    else if (this.helpers.IsClear)
                    {
                        this.lcd.TurnOn(false);
                        this.display.SetDisplay("HELPERS", this.helpers.SelectedName, "CLEAR");
                    }
                    else
                    {
                        this.lcd.TurnOff();
                        this.display.SetDisplay("HELPERS", "HELPER:\n" + text, "CREATE");
                    }
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;

                case State.Exit:
                    this.lcd.TurnOn(true);
                    this.display.SetDisplay("EXIT", "EXIT LIGHTSNIPER?", "BACK");
                    this.ButtonBehaviour = ButtonBehaviourType.Override;
                    break;
            }
        }

        public Color GetLaserBeamColor()
        {
            switch (this.state)
            {
                case State.MaybePlace:  return Color.green;
                case State.Place:       return Color.green;
                case State.MaybePaint:  return Color.cyan;
                case State.Paint:       return Color.cyan;
                case State.MaybeDelete: return Color.red;
                case State.Delete:      return Color.red;
                default:                return Color.white;
            }
        }
    }
}
