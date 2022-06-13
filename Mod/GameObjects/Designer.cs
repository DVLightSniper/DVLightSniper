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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DV;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Spawners;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

namespace DVLightSniper.Mod.GameObjects
{
    /// <summary>
    /// The menu system in CommsRadioLightSniper is pretty complex already, so this class handles
    /// just the light designer part of the menu system since it's a pretty complex submenu in its
    /// own right.
    /// </summary>
    internal class Designer
    {
        private static readonly int[] COLOUR_STEPS =
        {
            0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF
        };

        private static readonly float[] CORONA_SCALE_STEPS =
        {
            0.10F, 0.25F, 0.33F, 0.50F, 0.75F, 0.87F, 1.00F, 1.10F, 1.25F, 1.50F, 1.75F, 2.0F, 2.5F, 5.0F
        };

        private static readonly float[] CORONA_RANGE_STEPS =
        {
            25.0F, 50.0F, 100.0F, 250.0F, 500.0F, 1000.0F
        };

        private static readonly float[] INTENSITY_STEPS =
        {
            0.1F, 0.2F, 0.3F, 0.4F, 0.5F, 0.6F, 0.7F, 0.8F, 0.9F, 1.0F, 1.25F, 1.5F, 1.75F, 2.0F, 2.5F, 3.0F, 4.0F, 5.0F
        };

        private static readonly float[] RANGE_STEPS =
        {
            1.0F, 2.0F, 5.0F, 10.0F, 15.0F, 20.0F, 25.0F, 30.0F, 50.0F
        };

        private static readonly DutyCycle[] DUTY_CYCLE_PRESETS =
        {
            DutyCycle.ALWAYS, new DutyCycle.DuskTillDawn(), new DutyCycle.Flashing(1.0F), new DutyCycle.Flashing(2.0F), new DutyCycle.Random(false), null
        };

        internal event Action<Designer> OnExit;

        internal enum State : int
        {
            DesignIntensity,
            DesignRange,
            DesignRed,
            DesignGreen,
            DesignBlue,
            DesignCoronaSprite,
            DesignCoronaSize,
            DesignCoronaRange,
            ChangeDutyCycle,
            NeverCullToggle,
            SelectMode
        }

        private State state = State.SelectMode;

        private State mode = 0;

        private LightSpawner designingLight;

        private LightProperties properties;

        private int dutyCycleIndex = 0;

        internal LightSpawner DesigningLight
        {
            get
            {
                return this.designingLight;
            }

            set
            {
                if (this.designingLight != null)
                {
                    this.designingLight.Designing = false;
                    this.designingLight.SpriteIndex = 0;
                    this.designingLight.Save();
                }

                this.designingLight = value;
                this.properties = value?.Properties;

                if (this.designingLight != null)
                {
                    this.designingLight.SpriteIndex = 5;
                    this.designingLight.Designing = true;
                }
            }
        }

        internal Designer(LightSpawner designingLight)
        {
            this.DesigningLight = designingLight;
        }

        internal void Release()
        {
            this.DesigningLight = null;
        }

        internal void OnUse()
        {
            if (this.state == State.SelectMode)
            {
                if (this.mode == State.SelectMode)
                {
                    this.Exit();
                    return;
                }

                if (this.mode == State.NeverCullToggle)
                {
                    this.properties.NeverCull = !this.properties.NeverCull;
                    return;
                }

                this.dutyCycleIndex = 0;
                this.state = this.mode;
                return;
            }

            if (this.Apply())
            {
                return;
            }

            this.state = State.SelectMode;
        }

        internal void Exit()
        {
            Action<Designer> onExit = this.OnExit;
            onExit?.Invoke(this);
        }

        private bool Apply()
        {
            if (this.state == State.DesignRed || this.state == State.DesignGreen)
            {
                this.state++;
                return true;
            }

            if (this.state == State.ChangeDutyCycle)
            {
                this.SelectDutyCycle(0);
            }

            return false;
        }

        internal bool ButtonACustomAction()
        {
            if (this.state == State.SelectMode)
            {
                if (this.mode == State.DesignRed)
                {
                    // Skip over green and blue
                    this.mode = (State.DesignBlue + 1);
                }
                else
                {
                    this.mode = this.mode == State.SelectMode ? 0 : ++this.mode;
                }

                return true;
            }

            this.AdjustParameter(-1);
            return true;
        }

        internal bool ButtonBCustomAction()
        {
            if (this.state == State.SelectMode)
            {
                if (this.mode == (State.DesignBlue + 1))
                {
                    // Skip over green and blue
                    this.mode = State.DesignRed;
                }
                else
                {
                    this.mode = this.mode == 0 ? State.SelectMode : --this.mode;
                }

                return true;
            }

            this.AdjustParameter(1);
            return true;
        }

        internal void UpdateRadio(ArrowLCD lcd, CommsRadioDisplay display)
        {
            switch (this.state)
            {
                case State.SelectMode:
                    lcd.TurnOff();
                    if (this.mode == State.SelectMode)
                    {
                        lcd.TurnOn(true);
                    }

                    display.SetDisplay("DESIGNER", this.MenuText, this.mode == State.SelectMode ? "BACK" : "EDIT");
                    break;

                case State.DesignCoronaSprite:
                    display.SetDisplay("DESIGNER", "SELECT CORONA:\n\n" + this.CoronaSprite, "DONE");
                    break;
                case State.DesignCoronaSize:
                    display.SetDisplay("DESIGNER", "CORONA SIZE:\n\n" + this.CoronaScale, "DONE");
                    break;
                case State.DesignCoronaRange:
                    display.SetDisplay("DESIGNER", "CORONA RANGE:\n\n" + this.CoronaRange, "DONE");
                    break;
                case State.DesignRed:
                    display.SetDisplay("DESIGNER", "COLOUR:\n" + Designer.ColourMenu(this.properties.Colour, 0), "NEXT");
                    break;
                case State.DesignGreen:
                    display.SetDisplay("DESIGNER", "COLOUR:\n" + Designer.ColourMenu(this.properties.Colour, 1), "NEXT");
                    break;
                case State.DesignBlue:
                    display.SetDisplay("DESIGNER", "COLOUR:\n" + Designer.ColourMenu(this.properties.Colour, 2), "DONE");
                    break;
                case State.DesignIntensity:
                    display.SetDisplay("DESIGNER", "INTENSITY:\n\n" + this.Intensity, "DONE");
                    break;
                case State.DesignRange:
                    display.SetDisplay("DESIGNER", "RANGE:\n\n" + this.Range, "DONE");
                    break;
                case State.ChangeDutyCycle:
                    display.SetDisplay("DESIGNER", "NEW DUTY CYCLE:\n\n" + this.SelectedDutyCycle?.RadioDisplayText, this.SelectedDutyCycle != null ? "APPLY" : "CANCEL");
                    break;
                case State.NeverCullToggle:
                    display.SetDisplay("DESIGNER", "IGNORE CULLING:\n" + this.NeverCull, "TOGGLE");
                    break;
            }
        }

        private void AdjustParameter(int offset)
        {
            switch (this.state)
            {
                case State.DesignCoronaSprite:
                    this.DesignCoronaSprite(offset);
                    break;
                case State.DesignCoronaSize:
                    this.DesignCoronaSize(offset);
                    break;
                case State.DesignCoronaRange:
                    this.DesignCoronaRange(offset);
                    break;
                case State.DesignRed:
                    this.DesignColour(offset, 0);
                    break;
                case State.DesignGreen:
                    this.DesignColour(offset, 1);
                    break;
                case State.DesignBlue:
                    this.DesignColour(offset, 2);
                    break;
                case State.DesignIntensity:
                    this.DesignIntensity(offset);
                    break;
                case State.DesignRange:
                    this.DesignRange(offset);
                    break;
                case State.ChangeDutyCycle:
                    this.SelectDutyCycle(offset * -1);
                    break;

            }

            this.designingLight.Apply();
        }

        private void DesignCoronaSprite(int offset)
        {
            int coronaIndex = CoronaComponent.AvailableCoronas.IndexOf(this.properties.Corona?.Sprite ?? "");
            int newCoronaIndex = (coronaIndex + offset + CoronaComponent.AvailableCoronas.Count) % CoronaComponent.AvailableCoronas.Count;
            if (this.properties.Corona == null)
            {
                this.properties.Corona = new CoronaProperties(CoronaComponent.AvailableCoronas[newCoronaIndex]);
            }
            else
            {
                this.properties.Corona.Sprite = CoronaComponent.AvailableCoronas[newCoronaIndex];
            }
        }

        private void DesignCoronaSize(int offset)
        {
            if (this.properties.Corona != null)
            {
                this.properties.Corona.Scale = Designer.GetOffsetValueInRange(this.properties.Corona.Scale, offset, Designer.CORONA_SCALE_STEPS);
            }
        }

        private void DesignCoronaRange(int offset)
        {
            if (this.properties.Corona != null)
            {
                this.properties.Corona.Range = Designer.GetOffsetValueInRange(this.properties.Corona.Range, offset, Designer.CORONA_RANGE_STEPS);
            }
        }

        private void DesignColour(int offset, int index)
        {
            Color colour = this.properties.Colour;
            colour[index] = (float)Math.Min(Designer.GetOffsetValueInRange((int)(colour[index] * 255.0), offset, Designer.COLOUR_STEPS) / 255.0, 1.0);
            this.properties.Colour = colour;
        }

        private void DesignIntensity(int offset)
        {
            this.properties.Intensity = Designer.GetOffsetValueInRange(this.properties.Intensity, offset, Designer.INTENSITY_STEPS);
        }

        private void DesignRange(int offset)
        {
            this.properties.Range = Designer.GetOffsetValueInRange(this.properties.Range, offset, Designer.RANGE_STEPS);
        }

        private void SelectDutyCycle(int offset)
        {
            if (offset == 0)
            {
                if (this.SelectedDutyCycle != null)
                {
                    this.properties.DutyCycle = this.SelectedDutyCycle.Clone();
                    this.designingLight.Apply();
                }

                return;
            }

            this.dutyCycleIndex = (this.dutyCycleIndex + offset + Designer.DUTY_CYCLE_PRESETS.Length) % Designer.DUTY_CYCLE_PRESETS.Length;
        }

        private static T GetOffsetValueInRange<T>(T value, int offset, T[] steps)
            where T : IComparable
        {
            int currentIndex = 0;
            for (int index = 0; index < steps.Length; index++)
            {
                if (value.CompareTo(steps[index]) >= 0)
                {
                    currentIndex = index;
                }
            }

            return steps[Math.Min(Math.Max(currentIndex + offset, 0), steps.Length - 1)];
        }

        private string CoronaSprite
        {
            get
            {
                string sprite = this.properties.Corona?.Sprite;
                return string.IsNullOrEmpty(sprite) ? "<NONE>" : Path.GetFileNameWithoutExtension(sprite);
            }
        }

        private string CoronaScale
        {
            get
            {
                CoronaProperties corona = this.properties.Corona;
                return corona?.Scale.ToString("F2") ?? "1.0";
            }
        }

        private string CoronaRange
        {
            get
            {
                CoronaProperties corona = this.properties.Corona;
                return corona?.Range.ToString("F1") ?? "100.0";
            }
        }

        private string Intensity
        {
            get
            {
                return this.properties.Intensity.ToString("F2");
            }
        }

        private string Range
        {
            get
            {
                return this.properties.Range.ToString("F1");
            }
        }

        private DutyCycle CurrentDutyCycle
        {
            get
            {
                return this.properties.DutyCycle;
            }
        }

        private DutyCycle SelectedDutyCycle
        {
            get
            {
                return Designer.DUTY_CYCLE_PRESETS[this.dutyCycleIndex];
            }
        }

        private string Colour
        {
            get
            {
                int r = (int)Math.Round(this.properties.Colour.r * 255.0);
                int g = (int)Math.Round(this.properties.Colour.g * 255.0);
                int b = (int)Math.Round(this.properties.Colour.b * 255.0);
                return $"{r:X2}{g:X2}{b:X2}";
            }
        }

        private bool NeverCull
        {
            get
            {
                return this.properties.NeverCull;
            }
        }

        private static string ColourMenu(Color colour, int cursor)
        {
            int r = (int)Math.Round(colour.r * 255.0);
            int g = (int)Math.Round(colour.g * 255.0);
            int b = (int)Math.Round(colour.b * 255.0);

            string red   = $"RED    {(cursor == 0 ? '<' : ' ')} {r:X2} {(cursor == 0 ? '>' : ' ')}";
            string green = $"GREEN {(cursor == 1 ? '<' : ' ')} {g:X2} {(cursor == 1 ? '>' : ' ')}";
            string blue  = $"BLUE   {(cursor == 2 ? '<' : ' ')} {b:X2} {(cursor == 2 ? '>' : ' ')}";

            return $"{red}\n{green}\n{blue}";
        }

        private string MenuText
        {
            get
            {
                switch (this.mode)
                {
                    case State.DesignCoronaSprite: return "CORONA SPRITE\n\n" + this.CoronaSprite;
                    case State.DesignCoronaSize:   return "CORONA SIZE\n\n" + this.CoronaScale;
                    case State.DesignCoronaRange:  return "CORONA RANGE\n\n" + this.CoronaRange;
                    case State.DesignRed:          return this.state == State.SelectMode ? "COLOUR\n\n" + this.Colour : "RED";
                    case State.DesignGreen:        return this.state == State.SelectMode ? "COLOUR [G]" : "GREEN";
                    case State.DesignBlue:         return this.state == State.SelectMode ? "COLOUR [B]" : "BLUE";
                    case State.DesignIntensity:    return "INTENSITY\n\n" + this.Intensity;
                    case State.DesignRange:        return "RANGE\n\n" + this.Range;
                    case State.ChangeDutyCycle:    return "CHANGE DUTY CYCLE\n\n" + this.CurrentDutyCycle?.RadioDisplayText;
                    case State.NeverCullToggle:    return "IGNORE CULLING:\n" + this.NeverCull;
                }

                return "";
            }
        }
    }
}