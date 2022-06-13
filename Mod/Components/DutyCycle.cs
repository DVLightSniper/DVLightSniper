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
using System.Reflection;
using System.Text.RegularExpressions;

using DVLightSniper.Mod.GameObjects;

namespace DVLightSniper.Mod.Components
{
    /// <summary>
    /// This class and its children manage duty cycles (on and off time) for lights based on certain
    /// criteria or game state.
    /// </summary>
    internal abstract class DutyCycle
    {
        /// <summary>
        /// A light which is always on
        /// </summary>
        internal class Always : DutyCycle
        {
            internal static readonly Always INSTANCE = new Always();

            private Always()
            {
            }

            internal static DutyCycle Parse(int argc, string[] argv)
            {
                return Always.INSTANCE;
            }
        }

        /// <summary>
        /// A light which comes on at the specified dusk time (plus or minus 10 minutes, randomly
        /// selected) and goes off at the specified dawn time (also with 20 minute window so that 
        /// lights don't all go on or off at the same time).
        /// </summary>
        internal class DuskTillDawn : DutyCycle
        {
            private static readonly System.Random RANDOM_SOURCE = new System.Random();

            private readonly int offsetSeconds;

            internal DuskTillDawn()
            {
                this.offsetSeconds = DuskTillDawn.RANDOM_SOURCE.Next(-600, 600);
            }

            protected override bool ComputeState()
            {
                return LightSniper.Settings.IsNightTime(SpawnerController.CurrentSecond + this.offsetSeconds);
            }

            internal static DutyCycle Parse(int argc, string[] argv)
            {
                return new DuskTillDawn();
            }
        }

        /// <summary>
        /// A light which flashes on and off based on its spawn time at the rate specified by
        /// frequency
        /// </summary>
        internal class Flashing : DutyCycle
        {
            internal class FrequencySource
            {
                private static readonly List<FrequencySource> globals = new List<FrequencySource>();

                internal float Frequency { get; }

                private DateTime lastFlashTime;
                private bool flashState;

                internal FrequencySource(float frequency)
                {
                    this.Frequency = frequency;
                }

                internal bool ComputeState()
                {
                    if (this.Frequency <= 0.001F)
                    {
                        return true;
                    }

                    float flashTime = 1.0F / this.Frequency;
                    if ((DateTime.UtcNow - this.lastFlashTime).TotalSeconds > flashTime)
                    {
                        this.flashState = !this.flashState;
                        this.lastFlashTime = DateTime.UtcNow;
                    }
                    return this.flashState;
                }

                public override bool Equals(object obj)
                {
                    return obj is FrequencySource fs && Math.Abs(fs.Frequency - this.Frequency) < 0.001F;
                }

                public override int GetHashCode()
                {
                    return this.Frequency.GetHashCode();
                }

                internal static FrequencySource Global(float frequency)
                {
                    foreach (FrequencySource global in FrequencySource.globals)
                    {
                        if (Math.Abs(global.Frequency - frequency) < 0.001F)
                        {
                            return global;
                        }
                    }

                    FrequencySource newGlobal = new FrequencySource(frequency);
                    FrequencySource.globals.Add(newGlobal);
                    return newGlobal;
                }
            }

            protected readonly FrequencySource source;

            internal Flashing(float frequency)
                : this(new FrequencySource(frequency))
            { }

            protected Flashing(FrequencySource source)
            {
                this.source = source;
            }

            protected override bool ComputeState()
            {
                return this.source.ComputeState();
            }

            public override string ToString()
            {
                return $"{this.Name}({this.source.Frequency:F2})";
            }

            public override bool Equals(object obj)
            {
                return obj is Flashing flashing && Math.Abs(flashing.source.Frequency - this.source.Frequency) < 0.001F;
            }

            public override int GetHashCode()
            {
                return this.Name.GetHashCode() * this.source.GetHashCode() * 37;
            }

            internal static DutyCycle Parse(int argc, string[] argv)
            {
                return argc > 0 && float.TryParse(argv[0], out float frequency) ? new Flashing(frequency) : null;
            }
        }

        /// <summary>
        /// A light which flashes on and off at the specified frequency and is synchronised with all
        /// other lights of the same frequency. Setting "alternate" inverts the phase so it is off
        /// when matching lights are on and vice versa.
        /// </summary>
        internal class FlashingGlobal : Flashing
        {
            internal bool Alternate { get; }

            internal FlashingGlobal(float frequency, bool alternate = false)
                : base(FrequencySource.Global(frequency))
            {
                this.Alternate = alternate;
            }

            protected override bool ComputeState()
            {
                bool state = base.ComputeState();
                return this.Alternate ? !state : state;
            }

            public override string ToString()
            {
                return $"{this.Name}({this.source.Frequency:F2},{this.Alternate})";
            }

            internal new static DutyCycle Parse(int argc, string[] argv)
            {
                if (argc > 0 && float.TryParse(argv[0], out float frequency))
                {
                    bool alternate = argc > 1 && bool.TryParse(argv[1], out bool altv) && altv;
                    return new FlashingGlobal(frequency, alternate);
                }
                
                return null;
            }
        }

        /// <summary>
        /// A sequenced light which takes a sequence of times (specified in seconds). For example
        /// specifying (0.2, 1.8) leads to a light which comes on for 0.2 seconds and then stays off
        /// for 1.8 seconds, thus having a total cycle time of 2 seconds and flashing very briefly
        /// like a strobe. Many sequence elements can be specified to create complex light patterns.
        /// For example (0.2, 0.5, 0.2, 0.5, 0.2, 2.0) creates a light which blinks 3 times every
        /// 3.6 seconds (the total sequence time is the sum of each element).
        /// </summary>
        internal class Sequenced : DutyCycle
        {
            internal TimeSpan TotalTime { get; }

            private readonly long[] frames;

            private DateTime mark = DateTime.MinValue;

            internal Sequenced(float[] timings)
            {
                this.frames = Sequenced.TimingsToFrames(timings, out long totalTime);
                this.TotalTime = new TimeSpan(totalTime * TimeSpan.TicksPerMillisecond);
            }

            protected override bool ComputeState()
            {
                if (this.mark == DateTime.MinValue || (DateTime.UtcNow - this.mark).TotalSeconds > (this.TotalTime.TotalSeconds * 10))
                {
                    this.mark = DateTime.UtcNow;
                    return true;
                }

                TimeSpan timeSlice = (DateTime.UtcNow - this.mark);
                while (timeSlice > this.TotalTime)
                {
                    this.mark += this.TotalTime;
                    timeSlice = (DateTime.UtcNow - this.mark);
                }
                long cursor = (long)timeSlice.TotalMilliseconds;
                bool state = true;
                for (int frame = 0; frame < this.frames.Length - 1; frame++)
                {
                    if (cursor >= this.frames[frame])
                    {
                        state = !state;
                    }
                }
                return state;
            }

            private static long[] TimingsToFrames(float[] timings, out long totalTime)
            {
                long[] frames = new long[timings.Length];
                long frameTime = 0L;
                for (int frame = 0; frame < timings.Length; frame++)
                {
                    frameTime += (long)(timings[frame] * 1000.0);
                    frames[frame] = frameTime;
                }
                totalTime = frameTime;
                return frames;
            }

            internal static DutyCycle Parse(int argc, string[] argv)
            {
                List<float> timings = new List<float>();
                foreach (string arg in argv)
                {
                    if (float.TryParse(arg, out float timing))
                    {
                        timings.Add(timing);
                    }
                }
                return timings.Count > 1 ? new Sequenced(timings.ToArray()) : null;
            }
        }

        /// <summary>
        /// A random duty cycle with on and off times between 25ms and 500ms, creates a "broken
        /// light" effect like a old flourescent light which can't strike properly.
        /// </summary>
        internal class Random : DutyCycle
        {
            private static readonly System.Random RANDOM_SOURCE = new System.Random();

            private static int MIN_TIME = 25;
            private static int MAX_TIME = 500;

            private readonly bool always;

            private DateTime nextTransition = DateTime.UtcNow;

            private bool state = false;

            public Random(bool always)
            {
                this.always = always;
            }

            protected override bool ComputeState()
            {
                if (!this.always && !SpawnerController.IsNightTime)
                {
                    return false;
                }

                if (DateTime.UtcNow > this.nextTransition)
                {
                    this.state = !this.state;
                    this.nextTransition = DateTime.UtcNow.AddMilliseconds(Random.RANDOM_SOURCE.Next(Random.MIN_TIME, Random.MAX_TIME));
                }

                return this.state;
            }

            public override string ToString()
            {
                return $"{this.Name}({(this.always ? "always" : "")})";
            }

            internal static DutyCycle Parse(int argc, string[] argv)
            {
                return new Random(argc > 0 && argv[0].Equals("always", StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// ALWAYS is a singleton since it's used everywhere that no other duty cycle is needed and
        /// basically does nothing except return true all the time
        /// </summary>
        internal static readonly DutyCycle ALWAYS = Always.INSTANCE;

        /// <summary>
        /// Duty cycles are serialised as "ClassName(list,of,arguments)" so we parse this out of
        /// the config with regex
        /// </summary>
        private static readonly Regex DUTY_CYCLE_REGEX = new Regex(@"^(?<name>[A-Za-z]+)\((?<args>[^\)]*)\)$");

        /// <summary>
        /// Name of this duty cycle
        /// </summary>
        internal string Name
        {
            get { return this.GetType().Name; }
        }

        /// <summary>
        /// How to display the duty cycle on the comms radio when in designer mode. Displays the
        /// name on one line and the args on the line below without the parentheses
        /// </summary>
        internal string RadioDisplayText
        {
            get
            {
                return this.ToString().Replace("(", "\n").Replace(")", "").Replace(",", ", ");
            }
        }

        /// <summary>
        /// Whether this duty cycle is currently on (after call to Update)
        /// </summary>
        internal bool On { get; private set; }

        public override string ToString()
        {
            return $"{this.Name}()";
        }

        /// <summary>
        /// Update this duty cycle's "on" state
        /// </summary>
        internal virtual void Update()
        {
            this.On = this.ComputeState();
        }

        /// <summary>
        /// Overridden by subclasses, does the actual computation of the state
        /// </summary>
        /// <returns></returns>
        protected virtual bool ComputeState()
        {
            return true;
        }

        internal virtual DutyCycle Clone()
        {
            return DutyCycle.Parse(this.ToString());
        }

        public override bool Equals(object obj)
        {
            return obj?.GetType() == this.GetType();
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        /// <summary>
        /// Parse a duty cycle from a configuration string
        /// </summary>
        /// <param name="dutyCycle"></param>
        /// <returns></returns>
        internal static DutyCycle Parse(string dutyCycle)
        {
            if (dutyCycle == null)
            {
                return DutyCycle.ALWAYS;
            }

            Match match = DutyCycle.DUTY_CYCLE_REGEX.Match(dutyCycle);
            if (match.Success)
            {
                string name = match.Groups["name"].Value;
                string[] argv = match.Groups["args"].Value.Split(',');
                int argc = argv.Length;
                for (int i = 0; i < argc; i++)
                {
                    argv[i] = argv[i].Trim();
                }

                foreach (Type cycleType in typeof(DutyCycle).GetNestedTypes(BindingFlags.NonPublic))
                {
                    if (name.Equals(cycleType.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        MethodInfo mParse = cycleType.GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static);
                        if (mParse?.Invoke(null, new object[]{ argc, argv }) is DutyCycle parsed)
                        {
                            return parsed;
                        }
                    }
                }
            }

            return DutyCycle.ALWAYS;
        }
    }
}
