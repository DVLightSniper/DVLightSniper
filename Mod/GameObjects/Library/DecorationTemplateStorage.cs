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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.Storage;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Library
{
    /// <summary>
    /// Collection of decoration templates
    /// </summary>
    [DataContract]
    internal class DecorationTemplateStorage : TemplateStorage<DecorationTemplate>
    {
        internal static readonly DecorationTemplate TRAFFIC_LIGHT_GREEN = new DecorationTemplate("TRAFFIC_LIGHT:GREEN", "Traffic Light Green")
                                                                                .WithTarget("TrafficLight01_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight01_GreenLight")
                                                                                .WithTarget("TrafficLight03_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight03_GreenLight");

        internal static readonly DecorationTemplate TRAFFIC_LIGHT_AMBER = new DecorationTemplate("TRAFFIC_LIGHT:AMBER", "Traffic Light Amber")
                                                                                .WithTarget("TrafficLight01_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight01_AmberLight")
                                                                                .WithTarget("TrafficLight03_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight03_AmberLight");

        internal static readonly DecorationTemplate TRAFFIC_LIGHT_RED = new DecorationTemplate("TRAFFIC_LIGHT:RED", "Traffic Light Red")
                                                                                .WithTarget("TrafficLight01_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight01_RedLight")
                                                                                .WithTarget("TrafficLight03_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight03_RedLight");

        internal static readonly DecorationTemplate TRAFFIC_LIGHT_AMBER_FLASHING = new DecorationTemplate("TRAFFIC_LIGHT:AMBER_FLASHING", "Traffic Light Amber (Flashing)")
                                                                                .WithTarget("TrafficLight01_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight01_AmberLight").WithDutyCycle(new DutyCycle.FlashingGlobal(2.0F))
                                                                                .WithTarget("TrafficLight03_LOD", "meshes_decoration_lights.assetbundle", "TrafficLight03_AmberLight").WithDutyCycle(new DutyCycle.FlashingGlobal(2.0F));

        internal static readonly (string name, int count)[] WINDOW_MESHES =
        {
            ("Burner_Large",          5),
            ("Burner_Large2",         5),
            ("Factory_FlatLarge",     5),
            ("Factory_FlatL",         5),
            ("Offices_Tall",          5),
            ("Utility_Medium",        5),
            ("Warehouse_Truck",       5),
            ("Warehouse_TruckTall",   5),
            ("Warehouse_MediumFlat",  5),
            ("Warehouse_MediumFlat2", 5),
            ("Warehouse_OldMedium",   5),
            ("Supermarket",           5),
            ("Factory_Food",          5),
            ("Offices_Medium2",       5),
            ("Church",                5),
            ("Utility_Medium2",       5),
            ("Offices_Medium",        5),
            ("Factory_Old",           5),
            ("house_01",              0),
            ("house_02",              0),
            ("house_03",              0),
            ("house_04",              0),
            ("house_05",              0),
            ("house_06",              0),
            ("house_07",              0),
            ("house_08",              0),
            ("house_09",              0),
            ("house_10",              0),
            ("house_11",              0),
            ("house_12",              0),
            ("house_13",              0),
            ("house_14",              0),
            ("house_15",              0),
            ("house_16",              0),
            ("house_17",              0),
            ("@FakeOffice_1",         5),
            ("@FakeOffice_2",         5),
            ("@FakeOffice_3",         5),
            ("@FakeOffice_4",         1),
            ("@FakeOffice_5",         1),
            ("@FakeOffice_6",         3),
            ("@FakeOffice_7",         1),
            ("Industry_Steel",        5),
            ("Industry_Iron",         5),
            ("Industry_Forest",       5),
            ("Industry_Coal",         5),
            ("Util_Conveyor",         1),
            ("@Utility_ForestHouse",  1),
            ("MilitaryBarracks01",    1)
        };

        internal static readonly (string name, string prefab, string renderer, float randomAlpha, int count)[] WINDOW_MATERIALS =
        {
            ("@rus_build_2et_01",  "rus_build_15_Windows",             "Regroup13", 0.5F, 5), // includes rus_build_2et_01a, rus_build_2et_01b and rus_build_2et_01c
            ("@rus_build_4et_01a", "rus_build_06_07_12_13_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_4et_01",  "rus_build_06_07_12_13_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_5et_01",  "rus_build_01_02_03_04_05_Windows", "Regroup13", 0.7F, 5), // includes rus_build_5et_01a
            ("@rus_build_5et_02",  "rus_build_01_02_03_04_05_Windows", "Regroup13", 0.7F, 5), // includes rus_build_5et_02a
            ("@rus_build_5et_03c", "rus_build_06_07_12_13_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_5et_03d", "rus_build_11_14_16_17_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_5et_03e", "rus_build_06_07_12_13_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_5et_03f", "rus_build_11_14_16_17_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_5et_03",  "rus_build_01_02_03_04_05_Windows", "Regroup13", 0.7F, 5), // includes rus_build_5et_03a and rus_build_5et_03b
            ("@rus_build_5et_04",  "rus_build_06_07_12_13_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_5et_05",  "rus_build_11_14_16_17_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_5et_06",  "rus_build_11_14_16_17_Windows",    "Regroup13", 0.7F, 5),
            ("@rus_build_9et_01",  "rus_build_08_09_10_Windows",       "Regroup13", 0.7F, 5), // includes rus_build_9et_01a and rus_build_9et_01b
            ("@rus_build_9et_03",  "rus_build_08_09_10_Windows",       "Regroup13", 0.7F, 5), // includes rus_build_9et_03a and rus_build_9et_03b
        };

        protected override void AddDefaults()
        {
            bool saveRequired = false;

            if (this.Version < 1)
            {
                for (int index = 1; index <= 5; index++)
                {
                    DecorationTemplate template = new DecorationTemplate($"WINDOWS_{index}:LIT", $"Building Windows {index}");
                    foreach ((string name, int count) in DecorationTemplateStorage.WINDOW_MESHES)
                    {
                        string match = name.StartsWith("@") ? name : name + "_LOD";
                        string asset = name.StartsWith("@") ? name.Substring(1) : name;
                        if (count == 0 && index == 1)
                        {
                            template.WithTarget(match, "meshes_decoration_lights.assetbundle", $"{asset}_Windows").WithDutyCycle(new DutyCycle.DuskTillDawn());
                        }
                        else if (index <= count)
                        {
                            template.WithTarget(match, "meshes_decoration_lights.assetbundle", $"{asset}_Windows{index}").WithDutyCycle(new DutyCycle.DuskTillDawn());
                        }
                    }

                    foreach ((string name, string prefab, string renderer, float randomAlpha, int count) in DecorationTemplateStorage.WINDOW_MATERIALS)
                    {
                        if (index <= count)
                        {
                            string match = name.StartsWith("@") ? name : name + "_LOD";
                            template.WithTarget(match, "meshes_decoration_lights.assetbundle", prefab).WithMaterialAssignment(renderer, index - 1).WithRandomAlpha(randomAlpha).WithDutyCycle(new DutyCycle.DuskTillDawn());
                        }

                    }
                    this.Add(template);
                }

                this.Add(DecorationTemplateStorage.TRAFFIC_LIGHT_GREEN);
                this.Add(DecorationTemplateStorage.TRAFFIC_LIGHT_AMBER);
                this.Add(DecorationTemplateStorage.TRAFFIC_LIGHT_RED);
                this.Add(DecorationTemplateStorage.TRAFFIC_LIGHT_AMBER_FLASHING);

                this.Version = 1;
                saveRequired = true;
            }

            if (saveRequired)
            {
                this.Save();
            }
        }

        internal DecorationTemplate.TemplateMatch MatchRecursive(GameObject gameObject, DecorationTemplate exclude = null)
        {
            return (from template in this.templates where template != exclude select template.MatchRecursive(gameObject)).FirstOrDefault(match => match != null);
        }
    }
}
