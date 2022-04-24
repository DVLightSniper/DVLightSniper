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

using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.Util;

using JetBrains.Annotations;

using UnityEngine;

using UnityModManagerNet;

using ModSettings = UnityModManagerNet.UnityModManager.ModSettings;
using ModEntry = UnityModManagerNet.UnityModManager.ModEntry;

namespace DVLightSniper.Mod
{
    public enum TriState
    {
        Auto,
        Enabled,
        Disabled
    }

    public enum UpdateStrategy
    {
        Lazy,
        Ambitious,
        Aggressive,
        Psychotic,
        NukeItFromOrbit
    }

    public class Settings : ModSettings, IDrawable
    {
        [Draw(Label = "Enable LightSniper Comms Radio Mode")]
        public bool EnableRadioMode = true;

        [Draw(Label = "Enable Player Glow", Type = DrawType.ToggleGroup)]
        public TriState EnablePlayerGlow = TriState.Auto;

        [Draw(Label = "Player Glow Intensity", InvisibleOn = "EnablePlayerGlow|Disabled", Type = DrawType.Slider, Min = 0.05F, Max = 1.0F)]
        public float PlayerGlowIntensity = 0.2F;

        [Draw(Label = "Corona Opacity (%)", Type = DrawType.Slider, Min = 0, Max = 100)]
        public int CoronaOpacity = 100;

        [Draw(Label = "Enable Distance Culling")]
        public bool EnableDistanceCulling = true;

        [Draw(Label = "Distance Culling Radius", VisibleOn = "EnableDistanceCulling|true", Type = DrawType.Slider, Min = 200, Max = 2000)]
        public int DistanceCullingRadius = 400;

        [Draw(Label = "Spawner Update Strategy Tune", Type = DrawType.PopupList)]
        public UpdateStrategy UpdateStrategy = UpdateStrategy.Lazy;

        [Draw(Label = "Update Tick Rate (ticks/s)", Type = DrawType.Slider, Min = 1, Max = 120)]
        public int TickRate = 60;

        [Draw(Label = "Light Render Mode", Type = DrawType.ToggleGroup)]
        public LightRenderMode RenderMode = LightRenderMode.Auto;

        [Draw(Label = "Dawn Time (Hour)", Type = DrawType.Slider, Min = 0, Max = 23)]
        public int DawnTime = 8;

        [Draw(Label = "Dusk Time (Hour)", Type = DrawType.Slider, Min = 1, Max = 24)]
        public int DuskTime = 18;

        [Draw(Label = "Enable Decorate Mode", VisibleOn = "EnableRadioMode|true")]
        public bool EnableDecorateMode = true;

        [Draw(Label = "Enable Designer Mode", VisibleOn = "EnableRadioMode|true")]
        public bool EnableDesignerMode = false;

        [Draw(Label = "Enable Helpers Mode", VisibleOn = "EnableRadioMode|true")]
        public bool EnableHelpers = false;

        [Draw(Label = "Enable Enhanced Mesh Placer (Modifier Keys)", VisibleOn = "EnableRadioMode|true")]
        public bool EnableEnhancedPlacer = false;

        [Draw(Label = "ROTATE Orientation", VisibleOn = "EnableRadioMode|true", Type= DrawType.KeyBinding)]
        public KeyBinding Perpendicular = new KeyBinding();

        [Draw(Label = "SHIFT Orientation", VisibleOn = "EnableRadioMode|true", Type = DrawType.KeyBinding)]
        public KeyBinding Shift = new KeyBinding();

        // Internal flag, tracks whether we have displayed the popup message for helpers
        public bool helperMessageDisplayed = false;

        // Internal flag, tracks whether we are showing the debug markers
        public bool showDebugMarkers = false;

        // Current group being edited
        public string currentGroup = Region.DEFAULT_GROUP_NAME;

        // User-tunable, sets the amount of random variance in brightness and range applied to
        // lights when spawned in order to make added lights feel more organic
        public int randomVariancePct = 20;

        // Flag which allows LightSniper to load on later versions of the game than the one it is
        // intended for. This is a "use at your own risk" flag.
        public bool forceLoadOnVersionMismatch = false;

        internal bool ShowDebugMarkers
        {
            get
            {
                return CommandLineOption.DEBUG_MARKERS || this.showDebugMarkers; 
            }
        }

        internal string CurrentGroup
        {
            get
            {
                return this.currentGroup;
            }

            set
            {
                this.currentGroup = value;
                this.Save(LightSniper.ModEntry);
            }
        }

        internal float RotationOffset
        {
            get
            {
                return this.EnableEnhancedPlacer ? (this.Perpendicular.Pressed() ? 90.0F : 0.0F) + (this.Shift.Pressed() ? 180.0F : 0.0F) : 0.0F;
            }
        }

        internal bool PerpendicularPressed
        {
            get => this.EnableEnhancedPlacer && this.Perpendicular.Pressed();
        }

        internal bool ShiftPressed
        {
            get => this.EnableEnhancedPlacer && this.Shift.Pressed();
        }

        internal int AllowedUpdates
        {
            get
            {
                if (LoadingScreenManager.IsLoading || FastTravelController.IsFastTravelling)
                {
                    return Int32.MaxValue;
                }

                switch (this.UpdateStrategy)
                {
                    case UpdateStrategy.Lazy: return 3;
                    case UpdateStrategy.Ambitious: return 5;
                    case UpdateStrategy.Aggressive: return 10;
                    case UpdateStrategy.Psychotic: return 50;
                    case UpdateStrategy.NukeItFromOrbit: return Int32.MaxValue;
                }

                return 3;
            }
        }

        internal int BackoffTimeMs
        {
            get
            {
                if (LoadingScreenManager.IsLoading || FastTravelController.IsFastTravelling)
                {
                    return 0;
                }

                switch (this.UpdateStrategy)
                {
                    case UpdateStrategy.Lazy: return 32;
                    case UpdateStrategy.Ambitious: return 32;
                    case UpdateStrategy.Aggressive: return 16;
                    case UpdateStrategy.Psychotic: return 16;
                    case UpdateStrategy.NukeItFromOrbit: return 0;
                }

                return 32;
            }
        }

        internal int DistanceCullingRadiusSq
        {
            get
            {
                return this.EnableDistanceCulling ? this.DistanceCullingRadius * this.DistanceCullingRadius : 0;
            }
        }

        internal double DesiredTickRate
        {
            get
            {
                return Math.Min(Math.Max(this.TickRate, 1), 120);
            }
        }

        public void OnLoad()
        {
            if (this.Perpendicular.keyCode == 0)
            {
                this.Perpendicular.keyCode = KeyCode.LeftControl;
            }

            if (this.Shift.keyCode == 0)
            {
                this.Shift.keyCode = KeyCode.LeftShift;
            }
        }

        public override void Save(ModEntry modEntry)
        {
            ModSettings.Save<Settings>(this, modEntry);
        }

        public void OnChange()
        {
            if (this.DuskTime <= this.DawnTime)
            {
                this.DuskTime = this.DawnTime + 1;
            }

            this.DistanceCullingRadius = (this.DistanceCullingRadius / 25) * 25;
            this.TickRate = Math.Max(1, (this.TickRate / 10) * 10);
        }
    }
}
