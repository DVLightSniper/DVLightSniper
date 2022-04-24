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

using DV;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects.Library;

using HarmonyLib;

using JetBrains.Annotations;

using TMPro;

using UnityEngine;

using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.Mixins
{
    /// <summary>
    /// Mixin for the comms radio which lets us obtain references we need to feed to the LightSniper
    /// comms radio mode (such as the radio itself, and other references we grab from
    /// JunctionRemoteLogic) and attach the holodisplay TMPro prefab which we will use to display
    /// errors.
    /// </summary>
    [HarmonyPatch(typeof(CommsRadioController))]
    static class CommsRadioControllerMixin
    {
        [UsedImplicitly]
        [HarmonyPatch("Awake")]
        private static void Postfix(CommsRadioController __instance, List<ICommsRadioMode> ___allModes, JunctionRemoteLogic ___switchControl, CommsRadioCargoLoader ___cargoLoaderControl, LaserBeamLineRenderer ___laserBeam)
        {
            GameObject holoDisplay = Object.Instantiate(AssetLoader.Builtin.LoadBuiltin<GameObject>("HoloDisplay"));
            holoDisplay.transform.parent = __instance.gameObject.transform;
            holoDisplay.transform.localPosition = new Vector3(-0.045F, 0.02F, 0.135F);
            holoDisplay.transform.rotation = Quaternion.Euler(90.0F, 180.0F, 0.0F);

            holoDisplay.AddComponent<TimedMessageComponent>();

            TextMeshPro holoDisplayText = holoDisplay.GetComponent<TextMeshPro>();
            TextMeshPro displayText = ___switchControl.display.content.GetComponent<TextMeshPro>();
            holoDisplayText.font = displayText.font;

            LightSniper.RadioConnection(__instance, ___allModes, ___switchControl, ___cargoLoaderControl, ___laserBeam, holoDisplay);
        }
    }
}
