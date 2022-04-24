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

using DV;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects;

using JetBrains.Annotations;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.Util
{
    /// <summary>
    /// Draws debugging information in the top left corner of the screen and can also render entity
    /// markers. This class contains a handful of built-in debug messages (player position) but all
    /// other messages are added by other objects by calling AddSection and writing messages into
    /// the section handle.
    /// </summary>
    internal class DebugOverlay : MonoBehaviour
    {
        /// <summary>
        /// A debug section, can contain a single text message which is updated on-demand, or
        /// multiple messages in a log-like format which expire after a preset delay
        /// </summary>
        internal class Section
        {
            /// <summary>
            /// A temporary message with built-in lifespan
            /// </summary>
            internal struct Message
            {
                private readonly DateTime createdTime;

                private readonly double lifeSpan;

                internal bool IsExpired
                {
                    get
                    {
                        return (DateTime.Now - this.createdTime).TotalSeconds > this.lifeSpan;
                    }
                }

                internal string Text { get; }

                internal Color Colour { get; }

                public Message(string text, Color colour, double lifeSpan = 1.0)
                {
                    this.lifeSpan = lifeSpan;
                    this.Text = text;
                    this.Colour = colour;
                    this.createdTime = DateTime.Now;
                }
            }

            /// <summary>
            /// Label for this section
            /// </summary>
            internal string Label { get; }

            /// <summary>
            /// Sets the text for this section, clears any existing temporary messages
            /// </summary>
            internal string Text
            {
                get
                {
                    return this.text;
                }

                set
                {
                    this.text = value;
                    this.messages = null;
                    this.Enabled = true;
                }
            }

            /// <summary>
            /// Default colour for this section (label and text)
            /// </summary>
            internal Color Colour { get; }

            /// <summary>
            /// Whether this section is enabled (visible)
            /// </summary>
            internal bool Enabled { get; set; }

            /// <summary>
            /// Whether messages added to this section via AddMessage should also be emitted to the
            /// log and at what level
            /// </summary>
            internal Logger.Level LogLevel { get; set; }

            private string text;
            private List<Message> messages;

            /// <summary>
            /// Do not call this ctor directly, use DebugOverlay.AddSection
            /// </summary>
            /// <param name="label"></param>
            /// <param name="colour"></param>
            internal Section(string label, Color colour)
            {
                this.Label = label;
                this.text = "";
                this.Colour = colour;
            }

            /// <summary>
            /// Add a message to this section with the default colour and lifespan
            /// </summary>
            /// <param name="text"></param>
            internal void AddMessage(string text)
            {
                this.AddMessage(text, this.Colour, 1.0);
            }

            /// <summary>
            /// Add a message to this section with the default colour and the specified lifespan
            /// </summary>
            /// <param name="text"></param>
            /// <param name="lifeSpan"></param>
            internal void AddMessage(string text, double lifeSpan)
            {
                this.AddMessage(text, this.Colour, lifeSpan);
            }

            /// <summary>
            /// Add a message to this section with the specified colour and default lifespan
            /// </summary>
            /// <param name="text"></param>
            /// <param name="colour"></param>
            internal void AddMessage(string text, Color colour)
            {
                this.AddMessage(text, colour, 1.0);
            }

            /// <summary>
            /// Add a message to this section with the specified colour and lifespan
            /// </summary>
            /// <param name="text"></param>
            /// <param name="colour"></param>
            /// <param name="lifeSpan"></param>
            internal void AddMessage(string text, Color colour, double lifeSpan)
            {
                LightSniper.Logger.Log(this.LogLevel, "[{0}] {1}", this.Label, text);

                if (!DebugOverlay.Enabled)
                {
                    return; // Don't add messages if overlay not enabled, they will just accumulate
                }

                (this.messages ?? (this.messages = new List<Message>())).Add(new Message(text, colour, lifeSpan));
                this.Enabled = true;
            }

            /// <summary>
            /// Clear and disable this section
            /// </summary>
            internal void Clear()
            {
                this.text = "";
                this.messages?.Clear();
                this.Enabled = false;
            }

            /// <summary>
            /// Remove this section from the debug overlay
            /// </summary>
            internal void Remove()
            {
                DebugOverlay.RemoveSection(this);
            }

            /// <summary>
            /// Get an interator over the messages (also cleans out expired messages)
            /// </summary>
            public IEnumerable<Message> Messages
            {
                get
                {
                    if (this.messages != null)
                    {
                        this.messages.RemoveAll(message => message.IsExpired);
                        return this.messages;
                    }

                    return new[] { new Message(this.Text, this.Colour) };
                }
            }

            /// <summary>
            /// Get the entire text body of the section (also cleans out expired messages)
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                if (this.messages != null)
                {
                    string messageText = "";
                    this.messages.RemoveAll(message => message.IsExpired);
                    foreach (Message message in this.messages)
                    {
                        messageText += message.Text + "\n";
                    }
                    return messageText;
                }

                return this.Text;
            }
        }

        /// <summary>
        /// A marker to draw on the HUD
        /// </summary>
        internal class Marker
        {
            /// <summary>
            /// Whether this marker is visible (enabled) or not
            /// </summary>
            internal bool Enabled { get; set; } = true;

            /// <summary>
            /// Whether this marker is important (no distance fade)
            /// </summary>
            internal bool Important { get; set; }

            /// <summary>
            /// World position (offset position)
            /// </summary>
            internal Vector3 Position { get; set; }

            /// <summary>
            /// Colour for the marker text
            /// </summary>
            internal Color Colour { get; set; } = Color.green;

            /// <summary>
            /// Marker text
            /// </summary>
            internal string Label { get; set; }

            /// <summary>
            /// Do not call this ctor directly, use DebugOverlay.AddMarker
            /// </summary>
            /// <param name="label"></param>
            /// <param name="colour"></param>
            internal Marker(string label, Color colour)
            {
                this.Label = label;
                this.Colour = colour;
            }

            /// <summary>
            /// Remove this marker from the debug overlay
            /// </summary>
            internal void Remove()
            {
                DebugOverlay.RemoveMarker(this);
            }
        }

        /// <summary>
        /// Whether the debug overlay is enabled
        /// </summary>
        internal static bool Enabled { get; private set; }

        /// <summary>
        /// Whether the debug overlay should be rendered
        /// </summary>
        internal static bool Visible { get; set; }

        /// <summary>
        /// Whether the debug overlay is both enabled and visible
        /// </summary>
        internal static bool Active { get { return DebugOverlay.Enabled && DebugOverlay.Visible; } }

        /// <summary>
        /// Debug sections added via AddSection
        /// </summary>
        private static readonly IList<Section> sections = new List<Section>();

        /// <summary>
        /// Markers added via AddMarker
        /// </summary>
        private static readonly IList<Marker> markers = new List<Marker>();

        // Built-in sections
        private static readonly Section playerPosition = DebugOverlay.AddSection("Local Position");
        private static readonly Section worldPosition = DebugOverlay.AddSection("World Position");
        private static readonly Section playerRotation = DebugOverlay.AddSection("Rotation");
        private static readonly Section rayTrace = CommandLineOption.DEBUG_RAYTRACE ? DebugOverlay.AddSection("Ray", Color.yellow) : null;

        private const int FONT_SIZE = 12;
        private static Font font;
        private static GUIStyle sectionStyle, markerStyle;

        private static float glyphWidth = 8.0F;
        private static float glyphHeight = 10.0F;

        /// <summary>
        /// ctor
        /// </summary>
        internal DebugOverlay()
        {
            DebugOverlay.Enabled = CommandLineOption.DEBUG;
            DebugOverlay.Visible = true;
        }

        /// <summary>
        /// Add a new section with the specified label and default colour (green)
        /// </summary>
        /// <param name="label"></param>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        internal static Section AddSection(string label, Logger.Level logLevel = Logger.Level.None)
        {
            return DebugOverlay.AddSection(label, Color.green, logLevel);
        }

        /// <summary>
        /// Add a new section with the specified label and colour
        /// </summary>
        /// <param name="label"></param>
        /// <param name="colour"></param>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        internal static Section AddSection(string label, Color colour, Logger.Level logLevel = Logger.Level.None)
        {
            Section section = new Section(label, colour) { LogLevel = logLevel };
            DebugOverlay.sections.Add(section);
            return section;
        }

        /// <summary>
        /// Remove the specified section
        /// </summary>
        /// <param name="section"></param>
        internal static void RemoveSection(Section section)
        {
            DebugOverlay.sections.Remove(section);
        }

        /// <summary>
        /// Add a new marker with the specified label and default colour (green)
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        internal static Marker AddMarker(string label)
        {
            return DebugOverlay.AddMarker(label, Color.green);
        }

        /// <summary>
        /// Add a new marker with the specified label and colour
        /// </summary>
        /// <param name="label"></param>
        /// <param name="colour"></param>
        /// <returns></returns>
        internal static Marker AddMarker(string label, Color colour)
        {
            Marker marker = new Marker(label, colour);
            DebugOverlay.markers.Add(marker);
            return marker;
        }

        /// <summary>
        /// Remove the specified marker
        /// </summary>
        /// <param name="marker"></param>
        internal static void RemoveMarker(Marker marker)
        {
            DebugOverlay.markers.Remove(marker);
        }

        [UsedImplicitly]
        private void Start()
        {
            if (DebugOverlay.font == null)
            {
                DebugOverlay.font = Font.CreateDynamicFontFromOSFont("Consolas", DebugOverlay.FONT_SIZE);
                DebugOverlay.font.RequestCharactersInTexture("H");

                DebugOverlay.sectionStyle = new GUIStyle
                {
                    fontStyle = FontStyle.Normal,
                    fontSize = DebugOverlay.FONT_SIZE
                };

                DebugOverlay.markerStyle = new GUIStyle
                {
                    fontStyle = FontStyle.Normal,
                    fontSize = 9
                };

                DebugOverlay.font.GetCharacterInfo('H', out CharacterInfo ci, DebugOverlay.sectionStyle.fontSize, DebugOverlay.sectionStyle.fontStyle);
                DebugOverlay.glyphWidth = ci.glyphWidth;
                DebugOverlay.glyphHeight = ci.glyphHeight;
            }
        }

        [UsedImplicitly]
        private void OnGUI()
        {
            DebugOverlay.sectionStyle.font = DebugOverlay.font;
            DebugOverlay.markerStyle.font = DebugOverlay.font;

            if (DebugOverlay.Visible)
            {
                this.UpdateLabels();
                this.DrawSections();

                if (LightSniper.Settings.ShowDebugMarkers)
                {
                    this.DrawMarkers();
                }
            }
        }

        private void DrawSections()
        {
            float labelWidth = 0.0F;
            foreach (Section line in DebugOverlay.sections)
            {
                labelWidth = Math.Max(labelWidth, line.Label.Length * DebugOverlay.glyphWidth);
            }

            float ySpacing = DebugOverlay.glyphHeight + 3.0F;

            float yPos = 24.0F;
            foreach (Section section in DebugOverlay.sections)
            {
                if (!section.Enabled)
                {
                    continue;
                }

                DebugOverlay.sectionStyle.normal.textColor = Color.green;
                GUI.Label(new Rect(24.0F, yPos, 100, 40), section.Label, DebugOverlay.sectionStyle);
                bool isEmpty = true;

                foreach (Section.Message message in section.Messages)
                {
                    foreach (string line in message.Text.Split('\n'))
                    {
                        if (line != "")
                        {
                            DebugOverlay.sectionStyle.normal.textColor = message.Colour;
                            GUI.Label(new Rect(24.0F + labelWidth + 12.0F, yPos, 640, ySpacing), line, DebugOverlay.sectionStyle);
                            yPos += ySpacing;
                            isEmpty = false;
                        }
                    }
                }

                if (isEmpty)
                {
                    yPos += ySpacing;
                }
            }
        }

        private void DrawMarkers()
        {
            foreach (Marker marker in DebugOverlay.markers)
            {
                if (!marker.Enabled)
                {
                    continue;
                }

                Vector3 screenPos = SpawnerController.PlayerCamera.WorldToScreenPoint(marker.Position);
                if (screenPos.z > 0 && (screenPos.z < 500.0F || marker.Important))
                {
                    screenPos.y = Screen.height - screenPos.y;
                    Color markerColour = marker.Colour;
                    markerColour.a = marker.Important ? 1.0F : (500.0F - screenPos.z) / 500.0F;
                    DebugOverlay.sectionStyle.normal.textColor = markerColour;
                    GUI.Label(new Rect(screenPos.x - 4, screenPos.y - 4, 16, 16), "+", DebugOverlay.sectionStyle);
                    if (screenPos.z < 100.0F || marker.Important)
                    {
                        float labelAlpha = marker.Important ? 1.0F : (100.0F - screenPos.z) / 100.0F;
                        DebugOverlay.markerStyle.normal.textColor = marker.Important ? markerColour : new Color(1.0F, 1.0F, 1.0F, labelAlpha);
                        GUI.Label(new Rect(screenPos.x + 16, screenPos.y, 200, 16), marker.Label, DebugOverlay.markerStyle);
                    }
                }
            }
        }

        private void UpdateLabels()
        {
            Vector3 playerPos = PlayerManager.PlayerTransform.position;
            Vector3 worldPos = PlayerManager.GetWorldAbsolutePlayerPosition();

            int xTile = Math.Min(Math.Max((int)Math.Floor(worldPos.x) >> 10, 0), 15);
            int zTile = Math.Min(Math.Max((int)Math.Floor(worldPos.z) >> 10, 0), 15);

            DebugOverlay.playerPosition.Text = $"X: {playerPos.x:F3} Y: {playerPos.y:F3} Z: {playerPos.z:F3}";
            DebugOverlay.worldPosition.Text = $"X: {worldPos.x:F3} Y: {worldPos.y:F3} Z: {worldPos.z:F3} Tile: ({xTile}, {zTile}) Hash: {worldPos.AsHex(true)}";

            float playerRotation = PlayerManager.PlayerTransform.eulerAngles.y;
            float playerRotationSnapped = (float)(Math.Floor((playerRotation + 7.5) / 15.0) * 15.0);
            int cardinalDir = (int)(((playerRotation + 45) / 90) % 4);
            string facing = cardinalDir > 2 ? "west" : cardinalDir > 1 ? "south" : cardinalDir > 0 ? "east" : "north";
            DebugOverlay.playerRotation.Text = $"Raw: {playerRotation:F1} Snapped: {playerRotationSnapped:F1} Facing: {facing}";

            if (DebugOverlay.rayTrace != null)
            {
                this.DebuyRaytrace();
            }
        }

        private void DebuyRaytrace()
        {
            DebugOverlay.rayTrace.Clear();

            Transform camera = PlayerManager.PlayerCamera.transform;
            Vector3 rayStart = camera.position + (camera.forward * 200.0F);
            Ray ray = new Ray(rayStart, -camera.forward);

            List<RaycastHit> sortedHits = new List<RaycastHit>(Physics.RaycastAll(ray, 200.0F, CoronaComponent.LAYER_MASK));
            sortedHits.Sort(((a, b) => (a.point - rayStart).sqrMagnitude > (b.point - rayStart).sqrMagnitude ? 1 : -1));
            foreach (RaycastHit hitInfo in sortedHits)
            {
                if (hitInfo.transform == null)
                {
                    continue;
                }

                float distanceTo = Vector3.Distance(hitInfo.point, rayStart);

                GameObject obj = hitInfo.transform.gameObject;
                if (obj != null)
                {
                    DebugOverlay.rayTrace.AddMessage($"Distance {distanceTo:F2} Collider: {hitInfo.collider} Tag: {hitInfo.collider.tag} Transform: {hitInfo.transform.GetObjectPath()} Layer: {LayerMask.LayerToName(hitInfo.collider.gameObject.layer)} ({Convert.ToString(hitInfo.collider.gameObject.layer, 2)})");
                }
                else
                {
                    DebugOverlay.rayTrace.AddMessage($"Distance {distanceTo:F2} Transform: {hitInfo.transform.GetObjectPath()}");
                }
            }
        }
    }
}
