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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CommandTerminal;

using DV;

using DVLightSniper.Mod.GameObjects;
using DVLightSniper.Mod.GameObjects.Library;
using DVLightSniper.Mod.Util;

using UnityEngine;

using UnityModManagerNet;

using ModEntry = UnityModManagerNet.UnityModManager.ModEntry;
using ModSettings = UnityModManagerNet.UnityModManager.ModSettings;

using Object = UnityEngine.Object;
using Debug = System.Diagnostics.Debug;
using Logger = DVLightSniper.Mod.Util.Logger;

namespace DVLightSniper.Mod
{
    /// <summary>
    /// Main mod class, initialises different mod elements such as the SpawnerController and comms
    /// radio mode as well as acting as a hub for settings and logging
    /// </summary>
    public class LightSniper
    {
        public const int VERSION_MAJOR = 0;
        public const int VERSION_MINOR = 1;
        public const int VERSION_REVISION = 0;

        public const int DV_BUILD = 92;

        /// <summary>
        /// Connection info bundle for hooking a CommsRadioLightSniper instance to a
        /// CommsRadioController, stored as a bundle to decouple the comms radio instance and
        /// LightSniper comms radio mode from each other
        /// </summary>
        internal sealed class CommsRadioConnection
        {
            /// <summary>
            /// Radio controller instance
            /// </summary>
            internal readonly CommsRadioController radio;

            /// <summary>
            /// Reference to the allModes collection in the radio, extracted via the mixin so that
            /// we can add our custom mode when needed
            /// </summary>
            internal readonly List<ICommsRadioMode> allModes;

            /// <summary>
            /// Reference to the CommsRadioDisplay, yoinked from the JunctionRemoteLogic mode so we
            /// can use it 
            /// </summary>
            internal readonly CommsRadioDisplay display;

            /// <summary>
            /// Reference to the ArrowLCD, yoinked from the JunctionRemoteLogic mode so we can use
            /// it 
            /// </summary>
            internal readonly ArrowLCD lcd;

            /// <summary>
            /// Reference to the signalOrigin Transform from the JunctionRemoteLogic mode so we can
            /// use it as default until OverrideSignalOrigin is called
            /// </summary>
            internal readonly Transform signalOrigin;

            /// <summary>
            /// Reference to the LaserBeamLineRenderer so we can change its colour when needed
            /// </summary>
            internal readonly LaserBeamLineRenderer laserBeam;

            /// <summary>
            /// Holographic display for showing error messages
            /// </summary>
            internal readonly GameObject holoDisplay;

            internal AudioClip hoverOverSwitch;
            internal AudioClip switchSound;
            internal AudioClip cancelSound;

            internal CommsRadioConnection(CommsRadioController radio, List<ICommsRadioMode> allModes, JunctionRemoteLogic switchControl, CommsRadioCargoLoader cargoLoaderControl, LaserBeamLineRenderer laserBeam, GameObject holoDisplay)
            {
                this.radio = radio;
                this.allModes = allModes;
                this.display = switchControl.display;
                this.lcd = switchControl.lcd;
                this.signalOrigin = switchControl.signalOrigin;
                this.laserBeam = laserBeam;
                this.holoDisplay = holoDisplay;

                this.hoverOverSwitch = switchControl.hoverOverSwitch;
                this.switchSound = switchControl.switchSound;
                this.cancelSound = cargoLoaderControl.cancelSound;
            }

            internal void Connect(CommsRadioLightSniper mode)
            {
                mode.transform.parent = this.radio.transform;
                mode.transform.localPosition = Vector3.zero;

                mode.display = this.display;
                mode.lcd = this.lcd;
                mode.signalOrigin = this.signalOrigin;
                mode.laserBeam = this.laserBeam;
                mode.holoDisplay = this.holoDisplay;
                mode.hoverOverSwitch = this.hoverOverSwitch;
                mode.switchSound = this.switchSound;
                mode.cancelSound = this.cancelSound;

                this.allModes.Add(mode);
            }

            internal void Release(CommsRadioLightSniper mode)
            {
                if (this.allModes.Remove(mode))
                {
                    try
                    {
                        // In case the LightSniper mode is currently selected, we call SetNextMode
                        // to select a different mode, which ensures the internal state of the radio
                        // controller remains consistent, otherwise weird stuff can happen
                        MethodInfo mdSetNextMode = this.radio.GetType().GetMethod("SetNextMode", BindingFlags.Instance | BindingFlags.NonPublic);
                        mdSetNextMode?.Invoke(this.radio, new object[0]);
                    }
                    catch (Exception e)
                    {
                        LightSniper.Logger.Error(e);
                    }
                }
            }
        }

        private static CommsRadioConnection radioConnection;

        internal static string Path
        {
            get;
            private set;
        }

        internal static bool Enabled
        {
            get; private set;
        }

        internal static LightSniper Instance
        {
            get; private set;
        }

        internal static SpawnerController SpawnerController
        {
            get
            {
                return GameObject.Find("SpawnerController")?.GetComponent<SpawnerController>();
            }
        }

        internal static Settings Settings
        {
            get; private set;
        }

        internal static ModEntry ModEntry
        {
            get; private set;
        }

        internal static Logger Logger { get; } = new Logger();

        public static bool EnablePlayerGlow
        {
            get
            {
                TriState enablePlayerGlow = LightSniper.Settings.EnablePlayerGlow;
                if (enablePlayerGlow == TriState.Enabled)
                {
                    return true;
                }
                if (enablePlayerGlow == TriState.Auto)
                {
                    return !(UnityModManager.FindMod("DVExtraLights")?.Active ?? false);
                }
                return false;
            }
        }

        internal static float PlayerRotationFine
        {
            get
            {
                return (float)Math.Floor((PlayerManager.PlayerTransform.rotation.eulerAngles.y * 4.0F) + 0.5F) / 4.0F;
            }
        }

        internal static Vector3 PlayerDirectionFine
        {
            get
            {
                return Quaternion.Euler(0, LightSniper.PlayerRotationFine, 0) * Vector3.forward;
            }
        }

        internal static float PlayerRotation
        {
            get
            {
                return (float)Math.Floor(PlayerManager.PlayerTransform.rotation.eulerAngles.y + 0.5);
            }
        }

        internal static Vector3 PlayerDirection
        {
            get
            {
                return Quaternion.Euler(0, LightSniper.PlayerRotation, 0) * Vector3.forward;
            }
        }

        internal static float PlayerCardinalRotation
        {
            get
            {
                return (int)(((PlayerManager.PlayerTransform.eulerAngles.y + 22.5) / 45) % 8) * 45.0F;
            }
        }

        internal static Vector3 PlayerCardinalDirection
        {
            get
            {
                return Quaternion.Euler(0, LightSniper.PlayerCardinalRotation, 0) * Vector3.forward;
            }
        }

        internal static Vector3 ModifiedPlayerDirection
        {
            get
            {
                return Quaternion.Euler(0, LightSniper.PlayerRotation + LightSniper.Settings.RotationOffset, 0) * Vector3.forward;
            }
        }

        /// <summary>
        /// Flag which indicates we were manually disabled and thus need to recreate game objects if
        /// re-enabled
        /// </summary>
        private bool reinitialise;

        /// <summary>
        /// Main gameobject which manages and updates spawners
        /// </summary>
        private GameObject spawnerController;

        /// <summary>
        /// Player glow component, like ExtraLights
        /// </summary>
        private GameObject playerGlow;

        /// <summary>
        /// Debug overlay
        /// </summary>
        private GameObject debugOverlay;

        private CommsRadioLightSniper radioMode;

        internal LightSniper(ModEntry modEntry)
        {
            LightSniper.Instance = this;
            LightSniper.ModEntry = modEntry;
            LightSniper.Logger.Accept(modEntry);

            if (modEntry.Enabled)
            {
                this.Enable(modEntry);
            }
        }

        internal void Enable(ModEntry modEntry)
        {
            if (LightSniper.Enabled)
            {
                return;
            }

            LightSniper.Path = modEntry.Path;
            LightSniper.Enabled = true;

            if (LightSniper.Settings == null)
            {
                try
                {
                    LightSniper.Settings = ModSettings.Load<Settings>(modEntry);
                }
                catch (Exception)
                {
                    LightSniper.Settings = new Settings();
                }
                LightSniper.Settings.OnLoad();
            }

            if (Config.BUILD_VERSION > LightSniper.DV_BUILD)
            {
                LightSniper.Logger.Error("This version of LightSniper is for Derail Valley Build {0} but Build {1} was found", LightSniper.DV_BUILD, Config.BUILD_VERSION);
                if (!LightSniper.Settings.forceLoadOnVersionMismatch)
                {
                    throw new InvalidOperationException("This version of LightSniper is not compatible with Derail Valley Build " + Config.BUILD_VERSION);
                }
            }

            modEntry.OnGUI = this.OnGui;
            modEntry.OnHideGUI = this.OnHideGui;
            modEntry.OnSaveGUI = this.OnSettingsSaved;

            if (this.reinitialise)
            {
                this.reinitialise = false;
                this.CreateGameObjects();
            }

            PlayerManager.PlayerChanged += this.OnPlayerChanged;

            Directory.CreateDirectory(System.IO.Path.Combine(LightSniper.Path, "Resources"));
            AssetLoader.Of("Meshes", (key) => key.StartsWith("meshes_") && key.EndsWith(".assetbundle")).ExtractResources();
            AssetLoader.Of("Coronas", (key) => key.StartsWith("corona_") && key.EndsWith(".png")).ExtractResources();

            this.UpdateRadioConnection();
        }

        internal void Disable(ModEntry modEntry)
        {
            if (!LightSniper.Enabled)
            {
                return;
            }

            LightSniper.Enabled = false;

            PlayerManager.PlayerChanged -= this.OnPlayerChanged;

            modEntry.OnGUI = null;
            modEntry.OnHideGUI = null;
            modEntry.OnSaveGUI = null;

            this.DestroyGameObjects();
            this.DisableRadio();
            this.reinitialise = true;
        }

        private void CreateGameObjects()
        {
            if (CommandLineOption.DEBUG)
            {
                this.debugOverlay = new GameObject("DVLightSniperDebugOverlay", typeof(DebugOverlay));
                this.debugOverlay.transform.parent = PlayerManager.PlayerTransform;
                this.debugOverlay.transform.localPosition = Vector3.zero;
            }

            this.spawnerController = new GameObject("SpawnerController", typeof(SpawnerController));
            this.spawnerController.transform.parent = PlayerManager.PlayerTransform;
            this.spawnerController.transform.localPosition = Vector3.zero;

            // Player glow like ExtraLights in case user is not running ExtraLights
            this.playerGlow = new GameObject("DVLightSniperPlayerGlow");
            this.playerGlow.transform.parent = PlayerManager.PlayerCamera.transform;
            this.playerGlow.transform.localPosition = Vector3.up * 0.3F;

            Light playerGlowLight = this.playerGlow.AddComponent<Light>();
            playerGlowLight.type = LightType.Point;
            playerGlowLight.enabled = LightSniper.EnablePlayerGlow;
            playerGlowLight.intensity = LightSniper.Settings.PlayerGlowIntensity;
        }

        private void DestroyGameObjects()
        {
            if (this.debugOverlay != null)
            {
                Object.Destroy(this.debugOverlay);
                this.debugOverlay = null;
            }

            if (this.spawnerController != null)
            {
                Object.Destroy(this.spawnerController);
                this.spawnerController = null;
            }

            if (this.playerGlow != null)
            {
                Object.Destroy(this.playerGlow);
                this.playerGlow = null;
            }
        }

        private void UpdateRadioConnection()
        {
            this.DisableRadio();

            if (LightSniper.Enabled && LightSniper.radioConnection != null && LightSniper.Settings.EnableRadioMode)
            {
                this.radioMode = new GameObject("LightSniperRadioController").AddComponent<CommsRadioLightSniper>();
                this.radioMode.Radio = LightSniper.radioConnection;
            }
        }

        private void DisableRadio()
        {
            if (this.radioMode != null)
            {
                this.radioMode.Radio = null;
                this.radioMode = null;
            }
        }

        public static void RadioConnection(CommsRadioController radio, List<ICommsRadioMode> allModes, JunctionRemoteLogic switchControl, CommsRadioCargoLoader cargoLoaderControl, LaserBeamLineRenderer laserBeam, GameObject holoDisplay)
        {
            LightSniper.radioConnection = new CommsRadioConnection(radio, allModes, switchControl, cargoLoaderControl, laserBeam, holoDisplay);
            if (LightSniper.Enabled)
            {
                LightSniper.Instance.UpdateRadioConnection();
            }
        }

        private void OnGui(ModEntry modEntry)
        {
            LightSniper.Settings.Draw(modEntry);
        }

        private void OnHideGui(ModEntry obj)
        {
            Light playerGlowLight = this.playerGlow?.GetComponent<Light>();
            if (playerGlowLight != null)
            {
                playerGlowLight.enabled = LightSniper.EnablePlayerGlow;
                playerGlowLight.intensity = LightSniper.Settings.PlayerGlowIntensity;
            }

            if (this.radioMode == null == LightSniper.Settings.EnableRadioMode)
            {
                this.UpdateRadioConnection();
            }
        }

        private void OnSettingsSaved(ModEntry modEntry)
        {
            LightSniper.Settings.Save(modEntry);
            this.OnHideGui(modEntry);
        }

        private void OnPlayerChanged()
        {
            this.DestroyGameObjects();
            this.CreateGameObjects();
            ConsoleCommands.Register();
        }
    }
}
