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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Console = System.Diagnostics.Debug;

using ModEntry = UnityModManagerNet.UnityModManager.ModEntry;
using ModLogger = UnityModManagerNet.UnityModManager.ModEntry.ModLogger;

namespace DVLightSniper.Mod.Util
{
    internal class Logger
    {
        internal enum Level
        {
            None,
            Trace,
            Debug,
            Info,
            Warn,
            Error
        }

        private ModLogger modLogger;

        internal void Accept(ModEntry modEntry)
        {
            this.modLogger = modEntry?.Logger;
        }

        internal void Log(Level level, string message)
        {
            switch (level)
            {
                case Level.Trace: this.Trace(message); return;
                case Level.Debug: this.Debug(message); return;
                case Level.Info:  this.Info (message); return;
                case Level.Warn:  this.Warn (message); return;
                case Level.Error: this.Error(message); return;
            }
        }

        internal void Log(Level level, object value)
        {
            switch (level)
            {
                case Level.Trace: this.Trace(value); return;
                case Level.Debug: this.Debug(value); return;
                case Level.Info:  this.Info (value); return;
                case Level.Warn:  this.Warn (value); return;
                case Level.Error: this.Error(value); return;
            }
        }

        [StringFormatMethod("format")]
        internal void Log(Level level, string format, params object[] args)
        {
            switch (level)
            {
                case Level.Trace: this.Trace(format, args); return;
                case Level.Debug: this.Debug(format, args); return;
                case Level.Info:  this.Info (format, args); return;
                case Level.Warn:  this.Warn (format, args); return;
                case Level.Error: this.Error(format, args); return;
            }
        }

        internal void Trace(string message)
        {
            if (CommandLineOption.DEBUG_TRACE)
            {
                Console.WriteLine(message);
            }
        }

        internal void Trace(object value)
        {
            if (CommandLineOption.DEBUG_TRACE)
            {
                Console.WriteLine(value);
            }
        }

        [StringFormatMethod("format")]
        internal void Trace(string format, params object[] args)
        {
            if (CommandLineOption.DEBUG_TRACE)
            {
                Console.WriteLine(format, args);
            }
        }

        internal void Debug(string message)
        {
            Console.WriteLine(message);
        }

        internal void Debug(object value)
        {
            Console.WriteLine(value);
        }

        [StringFormatMethod("format")]
        internal void Debug(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        internal void Info(string message)
        {
            Console.WriteLine(message);
            this.modLogger?.Log(message);
        }

        internal void Info(object value)
        {
            Console.WriteLine(value);
            this.modLogger?.Log(value?.ToString());
        }

        [StringFormatMethod("format")]
        internal void Info(string format, params object[] args)
        {
            Console.WriteLine(format, args);
            this.modLogger?.Log(string.Format(format, args));
        }

        internal void Warn(string message)
        {
            Console.WriteLine(message);
            this.modLogger?.Warning(message);
        }

        internal void Warn(object value)
        {
            Console.WriteLine(value);
            this.modLogger?.Warning(value?.ToString());
        }

        [StringFormatMethod("format")]
        internal void Warn(string format, params object[] args)
        {
            Console.WriteLine(format, args);
            this.modLogger?.Warning(string.Format(format, args));
        }

        internal void Error(string message)
        {
            Console.WriteLine(message);
            this.modLogger?.Error(message);
        }

        internal void Error(object value)
        {
            Console.WriteLine(value);
            this.modLogger?.Error(value?.ToString());
        }

        [StringFormatMethod("format")]
        internal void Error(string format, params object[] args)
        {
            Console.WriteLine(format, args);
            this.modLogger?.Error(string.Format(format, args));
        }
    }
}
