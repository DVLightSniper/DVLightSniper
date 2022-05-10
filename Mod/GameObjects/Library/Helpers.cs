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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using DV;

using DVLightSniper.Mod.Util;

using JetBrains.Annotations;

using TMPro;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Library
{
    /// <summary>
    /// Helpers are ephemeral objects such as grids and struts which can be used in-game to help
    /// lay out lights in an organised manner. Grids can be used to place lights at regular
    /// intervals for example, and struts can be used to place lights on objects which don't have
    /// colliders attached (such as some decorative objects like lamp posts).
    /// </summary>
    internal class Helpers
    {
        /// <summary>
        /// Snap offsets for struts
        /// </summary>
        internal class SnapPoints : MonoBehaviour
        {
            internal List<Vector3> Offsets { get; } = new List<Vector3>();

            internal float Distance { get; set; } = 0.15F;

            internal SnapPoints AddOffset(float x, float y, float z)
            {
                this.Offsets.Add(new Vector3(x, y, z));
                return this;
            }

            internal SnapPoints AddOffset(Vector3 offset)
            {
                this.Offsets.Add(offset);
                return this;
            }

            internal SnapPoints AddOffsets(IEnumerable<Vector3> offsets)
            {
                this.Offsets.AddRange(offsets);
                return this;
            }
        }

        internal class HelperAttachment : MonoBehaviour
        {
            internal Helper helper;
            internal object data;
        }

        /// <summary>
        /// Base helper class, spawns an asset and keeps it updated until it's "placed" by the user
        /// </summary>
        internal class Helper
        {
            private static int nextId;

            internal string DisplayName { get; }

            internal string AssetName { get; }

            internal Helper(string displayName, string assetName)
            {
                this.DisplayName = displayName;
                this.AssetName = assetName;
            }

            internal virtual GameObject Create()
            {
                if (this.AssetName == null)
                {
                    return null;
                }

                GameObject template = AssetLoader.Builtin.LoadBuiltin<GameObject>(this.AssetName);
                if (template == null)
                {
                    LightSniper.Logger.Warn("Failed to load helper object " + this.AssetName);
                    return null;
                }

                GameObject helper = new GameObject(this.GetType().Name + Helper.nextId++);
                helper.AddComponent<HelperAttachment>().helper = this;
                this.Create(helper, template);
                helper.transform.parent = SingletonBehaviour<WorldMover>.Instance.originShiftParent;
                return helper;
            }

            protected virtual void Create(GameObject helper, GameObject template)
            {
                GameObject subHelper = Object.Instantiate(template);
                subHelper.transform.parent = helper.transform;
                subHelper.transform.position = Vector3.zero;
            }

            internal virtual void Update(GameObject helper, RaycastHit hitInfo)
            {
                helper.transform.position = hitInfo.point + (hitInfo.normal * 0.02F);
                helper.transform.rotation = Quaternion.LookRotation(LightSniper.PlayerDirectionFine, Vector3.up);
            }

            internal virtual void Place(GameObject helper)
            {
            }

            internal virtual void Cancel(GameObject helper)
            {
            }

            internal virtual void End(GameObject helper)
            {
            }

            internal virtual void Destroy(GameObject helper)
            {
                Object.Destroy(helper);
            }

            public override string ToString()
            {
                return $"{this.GetType().Name}({this.DisplayName})";
            }
        }

        /// <summary>
        /// A helper which only acts like a menu item and has no other behaviour
        /// </summary>
        internal class MenuItem : Helper
        {
            private readonly Action func;

            internal MenuItem(string displayName, Action func)
                : base(displayName, null)
            {
                this.func = func;
            }

            internal override void Place(GameObject helper)
            {
                this.func();
            }
        }

        internal class ClearMenuItem : MenuItem
        {
            internal ClearMenuItem(string displayName, Action func)
                : base(displayName, func) { }
        }

        internal class CancelMenuItem : MenuItem
        {
            internal CancelMenuItem(string displayName, Action func)
                : base(displayName, func) { }
        }

        /// <summary>
        /// Helper which places a grid at the marked position, the grid is oriented according to the
        /// player's current direction
        /// </summary>
        internal class GridHelper : Helper
        {
            protected readonly int xStart, yStart, xEnd, yEnd;

            protected readonly float spacing, scale;

            internal GridHelper(string displayName, string assetName, int xStart, int yStart, int xEnd, int yEnd, float spacing = 10.0F, float scale = 1.0F)
                : base(displayName, assetName)
            {
                this.xStart = xStart;
                this.xEnd = xEnd;
                this.yStart = yStart;
                this.yEnd = yEnd;

                this.spacing = spacing;
                this.scale = scale;
            }

            protected override void Create(GameObject helper, GameObject template)
            {
                for (int x = this.xStart; x < this.xEnd; x++)
                {
                    for (int y = this.yStart; y < this.yEnd; y++)
                    {
                        this.CreateSubHelper(helper, template, x, y);
                    }
                }
            }

            protected virtual void CreateSubHelper(GameObject helper, GameObject template, int x, int y)
            {
                GameObject subHelper = Object.Instantiate(template);
                subHelper.transform.parent = helper.transform;
                subHelper.transform.position = new Vector3(x * this.spacing * this.scale, 0, y * this.spacing * this.scale);
                subHelper.transform.localScale = new Vector3(this.scale, this.scale, this.scale);
            }
        }

        /// <summary>
        /// Helper which places a grid of pillars at the marked position
        /// </summary>
        internal class PillarGridHelper : GridHelper
        {
            private readonly GameObject lineTemplate;

            internal PillarGridHelper(string displayName, string assetName, int xStart, int yStart, int xEnd, int yEnd, float spacing = 10.0F, float scale = 1.0F)
                : base(displayName, assetName, xStart, yStart, xEnd, yEnd, spacing, scale)
            {
                this.lineTemplate = AssetLoader.Builtin.LoadBuiltin<GameObject>("PillarLines10");
            }

            protected override void CreateSubHelper(GameObject helper, GameObject template, int x, int y)
            {
                base.CreateSubHelper(helper, template, x, y);

                if (this.lineTemplate != null && x < this.xEnd - 1 && y < this.yEnd - 1)
                {
                    float spacingScale = this.spacing / 10.0F;

                    GameObject lineHelper = Object.Instantiate(this.lineTemplate);
                    lineHelper.transform.parent = helper.transform;
                    lineHelper.transform.position = new Vector3(x * this.spacing * this.scale, 0.5F, y * this.spacing * this.scale);
                    lineHelper.transform.localScale = new Vector3(this.scale * spacingScale, this.scale * spacingScale, this.scale * spacingScale);
                }
            }
        }

        /// <summary>
        /// Helper which places a grid at the marked position, the grid is snapped to the nearest
        /// cardinal direction to the player's direction
        /// </summary>
        internal class SnappedGridHelper : GridHelper
        {
            internal SnappedGridHelper(string displayName, string assetName, int xStart, int yStart, int xEnd, int yEnd, float spacing = 10.0F, float scale = 1.0F)
                : base(displayName, assetName, xStart, yStart, xEnd, yEnd, spacing, scale)
            {
            }

            internal override void Update(GameObject helper, RaycastHit hitInfo)
            {
                float x = (float)Math.Floor(hitInfo.point.x);
                float y = (float)(Math.Ceiling(hitInfo.point.y * 10.0) * 0.1);
                float z = (float)Math.Floor(hitInfo.point.z);

                helper.transform.position = new Vector3(x, y, z);
                helper.transform.rotation = Quaternion.LookRotation(LightSniper.PlayerCardinalDirection, Vector3.up);
            }
        }

        /// <summary>
        /// Helper which places a chain of posts at variable intervals, limited to chainDistance for
        /// creating quick spacings along non-linear areas
        /// </summary>
        internal class ChainHelper : Helper
        {
            private static int nextLinkId;

            private readonly GameObject lineTemplate, labelTemplate;

            private readonly float chainDistance;

            private readonly Color colour;

            // Last chain post (of this type), which is where we will chain from
            private GameObject lastChainPost;

            // The chain link which joins the current post to the previous one
            private GameObject chainLink;

            // Distance label holder and TextMeshPro component
            private GameObject distanceLabel;
            private TextMeshPro distanceLabelText;

            internal ChainHelper(string displayName, string assetName, float chainDistance, Color colour)
                : base(displayName, assetName)
            {
                this.chainDistance = chainDistance;
                this.colour = colour;

                this.lineTemplate = AssetLoader.Builtin.LoadBuiltin<GameObject>("Tape10");
                this.labelTemplate = AssetLoader.Builtin.LoadBuiltin<GameObject>("PillarLabel");
            }

            internal override void Update(GameObject helper, RaycastHit hitInfo)
            {
                // If placing the first post, just place it normally
                if (this.lastChainPost == null)
                {
#pragma warning disable IDE0041 // Use 'is null' check - GameObject overrides equality operator
                    if (object.ReferenceEquals(this.lastChainPost, null))
#pragma warning restore IDE0041 // Use 'is null' check
                    {
                        base.Update(helper, hitInfo);
                        return;
                    }
                }

                // If we're placing a new post in the chain and don't have a link yet, create the
                // link and distance label
                if (this.chainLink == null)
                {
                    this.chainLink = Object.Instantiate(this.lineTemplate);
                    this.chainLink.name = this.lastChainPost.name + "_Link" + ChainHelper.nextLinkId++;
                }

                if (this.distanceLabel == null)
                {
                    this.distanceLabel = Object.Instantiate(this.labelTemplate);
                    this.distanceLabel.transform.SetParent(helper.transform, true);

                    this.distanceLabelText = this.distanceLabel.GetComponent<TextMeshPro>();
                    this.distanceLabelText.text = "";
                    this.distanceLabelText.color = this.colour;
                }

                Vector3 tapeVector = (hitInfo.point - this.lastChainPost.transform.position);
                float distance = Math.Min(this.chainDistance, tapeVector.magnitude);
                Vector3 tapeNormal = tapeVector.normalized;

                this.chainLink.transform.position = this.lastChainPost.transform.position + (Vector3.up * 0.5F);
                this.chainLink.transform.rotation = Quaternion.LookRotation(tapeNormal, Vector3.up);
                this.chainLink.transform.localScale = new Vector3(3.0F, 3.0F, distance / 10.0F);

                helper.transform.position = this.lastChainPost.transform.position + (tapeNormal * distance);
                helper.transform.rotation = Quaternion.LookRotation(LightSniper.PlayerDirection, Vector3.up);

                this.distanceLabelText.text = $"{distance:F1}m";
                this.distanceLabelText.color = this.colour;
                this.distanceLabelText.faceColor = this.colour;
            }

            internal override void Place(GameObject helper)
            {
                if (this.lastChainPost != null)
                {
                    helper.GetComponent<HelperAttachment>().data = this.lastChainPost;
                }

                this.lastChainPost = helper;
                if (this.chainLink != null)
                {
                    this.chainLink.transform.parent = helper.transform;
                    this.chainLink = null;

                    Object.Destroy(this.distanceLabel);
                    this.distanceLabel = null;
                    this.distanceLabelText = null;
                }
            }

            internal override void Cancel(GameObject helper)
            {
                if (this.chainLink != null)
                {
                    Object.Destroy(this.chainLink);
                    this.chainLink = null;
                }
                base.Cancel(helper);
            }

            internal override void End(GameObject helper)
            {
                this.Cancel(helper);
                this.lastChainPost = null;
            }

            internal override void Destroy(GameObject helper)
            {
                if (helper == this.lastChainPost)
                {
                    this.lastChainPost = helper.GetComponent<HelperAttachment>().data as GameObject;

                    if (this.lastChainPost == null)
                    {
                        Object.Destroy(this.chainLink);
                        this.chainLink = null;

                        Object.Destroy(this.distanceLabel);
                        this.distanceLabel = null;
                        this.distanceLabelText = null;
                    }
                }

                base.Destroy(helper);
            }
        }

        /// <summary>
        /// Helper which can place collidable "struts" into the world to create scaffolding for
        /// various purposes
        /// </summary>
        internal class StrutHelper : Helper
        {
            internal static readonly int IGNORE_RAYCAST = LayerMask.NameToLayer(Layers.Ignore_Raycast);

            protected Vector3 forward, upwards;

            protected readonly IList<Vector3> snapOffsets = new List<Vector3>();

            internal StrutHelper(string displayName, string assetName)
                : this(displayName, assetName, Vector3.zero, Vector3.zero)
            {
            }

            internal StrutHelper AddSnapOffset(float x, float y, float z)
            {
                this.snapOffsets.Add(new Vector3(x, y, z));
                return this;
            }

            internal StrutHelper AddSnapOffset(Vector3 offset)
            {
                this.snapOffsets.Add(offset);
                return this;
            }

            internal StrutHelper(string displayName, string assetName, Vector3 forward, Vector3 upwards)
                : base(displayName, assetName)
            {
                this.forward = forward;
                this.upwards = upwards;
            }

            protected override void Create(GameObject helper, GameObject template)
            {
                base.Create(helper, template);

                foreach (Transform child in helper.transform)
                {
                    // Disable raycast collision for strut while placing so that the placement raycast doesn't hit it
                    child.gameObject.layer = StrutHelper.IGNORE_RAYCAST;

                    if (this.snapOffsets.Count > 0)
                    {
                        child.gameObject.AddComponent<SnapPoints>().AddOffsets(this.snapOffsets);
                    }
                }

                foreach (MeshRenderer renderer in helper.GetComponentsInChildren<MeshRenderer>())
                {
                    Color materialColor = renderer.material.color;
                    materialColor.a = 0.25F;
                    renderer.material.color = materialColor;
                }
            }

            internal override void Place(GameObject helper)
            {
                base.Place(helper);

                // Re-enable raycast collision
                foreach (Transform child in helper.transform)
                {
                    child.gameObject.layer = 0;
                }

                foreach (MeshRenderer renderer in helper.GetComponentsInChildren<MeshRenderer>())
                {
                    Color materialColor = renderer.material.color;
                    materialColor.a = 1.0F;
                    renderer.material.color = materialColor;
                }
            }

            internal override void Update(GameObject helper, RaycastHit hitInfo)
            {
                helper.transform.position = hitInfo.point;

                bool snapped = false;
                SnapPoints snaps = hitInfo.transform.gameObject.GetComponent<SnapPoints>();
                if (snaps != null)
                {
                    Vector3 relativePosition = hitInfo.transform.parent.InverseTransformPoint(hitInfo.point);
                    foreach (Vector3 snapOffset in snaps.Offsets)
                    {
                        Vector3 snapDelta = relativePosition - snapOffset;
                        if (snapDelta.magnitude < snaps.Distance)
                        {
                            helper.transform.position = hitInfo.transform.parent.TransformPoint(snapOffset);
                            snapped = true;
                            break;
                        }
                    }
                }

                if (this.forward == Vector3.zero && this.upwards == Vector3.zero)
                {
                    // Snap to existing object so adjust rotation to match
                    if (snapped)
                    {
                        MeshOrientation orientation = hitInfo.GetMeshOrientation();
                        if (orientation == MeshOrientation.Wall)
                        {
                            helper.transform.rotation = Quaternion.LookRotation(Vector3.up, hitInfo.normal);
                        }
                        else if (orientation == MeshOrientation.Floor)
                        {
                            helper.transform.rotation = Quaternion.AngleAxis(hitInfo.transform.parent.rotation.eulerAngles.y, hitInfo.normal);
                        }
                        else
                        {
                            helper.transform.rotation = Quaternion.Euler(0, hitInfo.transform.parent.rotation.eulerAngles.y, 180.0F);
                        }
                    }
                    else
                    {
                        helper.transform.rotation = hitInfo.GetMeshRotation();
                    }
                }
                else
                {
                    helper.transform.rotation = Quaternion.LookRotation(
                        this.forward == Vector3.zero ? hitInfo.normal : this.forward,
                        this.upwards == Vector3.zero ? hitInfo.normal : this.upwards);
                }
            }
        }

        /// <summary>
        /// Available helpers
        /// </summary>
        private readonly List<Helper> helpers = new List<Helper>();

        /// <summary>
        /// Placed gameobjects
        /// </summary>
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        internal Transform SignalOrigin
        {
            get; set;
        }

        private int selectedIndex, lastUsedIndex;

        internal string SelectedName
        {
            get { return this.helpers[this.SelectedIndex].DisplayName; }
        }

        internal Helper SelectedHelper
        {
            get { return this.helpers[this.SelectedIndex]; }
        }

        internal bool IsValid { get { return !this.IsCancel && !this.IsClear; } }
        internal bool IsCancel { get { return this.SelectedHelper is CancelMenuItem; } }
        internal bool IsClear { get { return this.SelectedHelper is ClearMenuItem; } }

        public int SelectedIndex
        {
            get
            {
                return this.selectedIndex;
            }
            set
            {
                this.selectedIndex = Math.Min(Math.Max(value, 0), this.helpers.Count - 1);
                this.UpdateGameObject();
            }
        }

        private GameObject gameObject;

        private GameObject GameObject
        {
            get
            {
                return this.gameObject;
            }

            set
            {
                if (this.gameObject != null)
                {
                    this.gameObject.GetComponent<HelperAttachment>().helper.Cancel(this.gameObject);
                    Object.Destroy(this.gameObject);
                }

                this.gameObject = value;
            }
        }

        internal Helpers()
        {
            this.helpers.Add(new GridHelper       ("100m x 100m Grid",          "Grid10x10", -5, 0, 5, 10));
            this.helpers.Add(new GridHelper       ("20m x 100m Grid",           "Grid10x10", -1, 0, 1, 10));
            this.helpers.Add(new GridHelper       ("20m x 1km Grid",            "Grid10x10", -1, 0, 1, 100));
            this.helpers.Add(new GridHelper       ("100m x 1km Grid",           "Grid10x10", -5, 0, 5, 100));
            
            this.helpers.Add(new SnappedGridHelper("100m x 100m Grid\nSnapped", "Grid10x10", -5, 0, 5, 10));
            this.helpers.Add(new SnappedGridHelper("20m x 100m Grid\nSnapped",  "Grid10x10", -1, 0, 1, 10));
            this.helpers.Add(new SnappedGridHelper("20m x 1km Grid\nSnapped",   "Grid10x10", -1, 0, 1, 100));
            this.helpers.Add(new SnappedGridHelper("100m x 1km Grid\nSnapped",  "Grid10x10", -5, 0, 5, 100));

            this.helpers.Add(new GridHelper       ("1km² Massive Grid",         "Grid10x10", -5,  0,  5,  10, 10.0F, 10.0F));
            this.helpers.Add(new PillarGridHelper ("Pillar Grid 10m",           "Pillar",    -50, 0, 50, 100, 10.0F,  1.0F));
            this.helpers.Add(new PillarGridHelper ("Pillar Grid 30m",           "Pillar",    -50, 0, 50, 100, 30.0F,  1.0F));

            this.helpers.Add(new ChainHelper      ("Chain 10m",                 "Pillar",       10.0F,   Color.cyan));
            this.helpers.Add(new ChainHelper      ("Chain 20m",                 "PillarRed",    20.0F,   Color.red));
            this.helpers.Add(new ChainHelper      ("Chain 40m",                 "PillarGreen",  40.0F,   Color.green));
            this.helpers.Add(new ChainHelper      ("Chain 100m",                "PillarBlue",   100.0F,  Color.blue));
            this.helpers.Add(new ChainHelper      ("Chain 1km",                 "PillarYellow", 1000.0F, Color.yellow));

            this.helpers.Add(new StrutHelper      ("1m Strut\nSurface Normal",  "Strut1mYellow"                                 ).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 1.0F, 0.0F));
            this.helpers.Add(new StrutHelper      ("1m Strut\nX Axis",          "Strut1mRed",   Vector3.forward, Vector3.right  ).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 1.0F, 0.0F));
            this.helpers.Add(new StrutHelper      ("1m Strut\nY Axis",          "Strut1mGreen", Vector3.forward, Vector3.up     ).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 1.0F, 0.0F));
            this.helpers.Add(new StrutHelper      ("1m Strut\nZ Axis",          "Strut1mBlue",  Vector3.right,   Vector3.forward).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 1.0F, 0.0F));

            this.helpers.Add(new StrutHelper      ("5m Strut\nSurface Normal",  "Strut5mYellow"                                 ).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 2.5F, 0.0F).AddSnapOffset(0.0F, 5.0F, 0.0F));
            this.helpers.Add(new StrutHelper      ("5m Strut\nX Axis",          "Strut5mRed",   Vector3.forward, Vector3.right  ).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 2.5F, 0.0F).AddSnapOffset(0.0F, 5.0F, 0.0F));
            this.helpers.Add(new StrutHelper      ("5m Strut\nY Axis",          "Strut5mGreen", Vector3.forward, Vector3.up     ).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 2.5F, 0.0F).AddSnapOffset(0.0F, 5.0F, 0.0F));
            this.helpers.Add(new StrutHelper      ("5m Strut\nZ Axis",          "Strut5mBlue",  Vector3.right,   Vector3.forward).AddSnapOffset(Vector3.zero).AddSnapOffset(0.0F, 2.5F, 0.0F).AddSnapOffset(0.0F, 5.0F, 0.0F));

            this.helpers.Add(new ClearMenuItem    ("CLEAR GRIDS",  this.Clear<GridHelper>));
            this.helpers.Add(new ClearMenuItem    ("CLEAR CHAINS", this.Clear<ChainHelper>));
            this.helpers.Add(new ClearMenuItem    ("CLEAR STRUTS", this.Clear<StrutHelper>));
            this.helpers.Add(new ClearMenuItem    ("CLEAR ALL",    this.Clear<Helper>));
            this.helpers.Add(new CancelMenuItem   ("CANCEL",       this.End));
        }

        internal void Next()
        {
            this.selectedIndex++;
            if (this.selectedIndex >= this.helpers.Count)
            {
                this.selectedIndex = 0;
            }
            this.UpdateGameObject();
        }

        internal void Previous()
        {
            this.selectedIndex--;
            if (this.selectedIndex < 0)
            {
                this.selectedIndex = this.helpers.Count - 1;
            }
            this.UpdateGameObject();
        }

        private void UpdateGameObject()
        {
            this.GameObject = this.IsValid ? this.SelectedHelper.Create() : null;
            this.Update();
        }

        internal void Begin()
        {
            this.SelectedIndex = this.lastUsedIndex;

            if (!LightSniper.Settings.helperMessageDisplayed)
            {
                Transform boomBoxDisclaimer = SingletonBehaviour<CanvasSpawner>.Instance.CanvasGO.transform.Find("BoomboxDisclaimerPrompt");
                MenuScreen menuScreen = boomBoxDisclaimer?.GetComponent<MenuScreen>();
                TutorialPrompt prompt = menuScreen?.GetComponentInChildren<TutorialPrompt>(true);
                if (menuScreen != null && prompt != null)
                {
                    prompt.SetText("Important Note!\n\nHelpers are ephemeral objects which you can place to help you align, position and access lights. They will only exist until you close the game!\n\nYou can clear grid and strut helpers at any time using the options in the HELPERS menu.");
                    SingletonBehaviour<CanvasSpawner>.Instance.Open(menuScreen);
                }

                LightSniper.Settings.helperMessageDisplayed = true;
                LightSniper.Settings.Save(LightSniper.ModEntry);
            }
        }

        internal void Update()
        {
            if (this.GameObject == null)
            {
                return;
            }

            if (!SpawnerController.CastRay(this.SignalOrigin, out RaycastHit hitInfo))
            {
                this.GameObject.SetActive(false);
                return;
            }

            this.GameObject.SetActive(true);
            this.SelectedHelper.Update(this.GameObject, hitInfo);

        }

        internal void End()
        {
            Helper activeHelper = this.gameObject?.GetComponent<HelperAttachment>()?.helper;
            foreach (Helper helper in this.helpers)
            {
                helper.End(helper == activeHelper ? this.gameObject : null);
            }
            this.GameObject = null;
        }

        internal void Place()
        {
            if (!(this.SelectedHelper is MenuItem))
            {
                this.lastUsedIndex = this.selectedIndex;
            }

            this.SelectedHelper.Place(this.GameObject);
            if (this.gameObject != null)
            {
                this.gameObjects.Add(this.gameObject);
                this.gameObject = null;
            }
            this.UpdateGameObject();
        }

        internal void Undo()
        {
            if (this.gameObjects.Count > 0)
            {
                int last = this.gameObjects.Count - 1;
                GameObject tail = this.gameObjects[last];
                this.gameObjects.RemoveAt(last);
                tail.GetComponent<HelperAttachment>().helper.Destroy(tail);
            }
        }

        internal void Clear<T>() where T : Helper
        {
            bool filter(GameObject obj) => obj.name.Contains(typeof(T).Name);
            foreach (GameObject helper in this.gameObjects.FindAll(filter))
            {
                helper.GetComponent<HelperAttachment>().helper.Destroy(helper);
            }
            this.gameObjects.RemoveAll(filter);
        }
    }
}
