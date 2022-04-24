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
using System.Reflection;

using DV;

using DVLightSniper.Mod;

using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace DVLightSniper
{
    public static class Main
    {
        private static LightSniper lightSniper;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Main.lightSniper = new LightSniper(modEntry);
            modEntry.OnToggle += Main.SetEnabled;

            try
            {
                var harmony = new Harmony("com.mumfrey.dvlightsniper");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }

            return true;
        }

        private static bool SetEnabled(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (enabled)
            {
                Main.lightSniper.Enable(modEntry);
            }
            else
            {
                Main.lightSniper.Disable(modEntry);
            }

            return true;
        }
    }
}