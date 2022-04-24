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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.Util;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Library
{
    [DataContract]
    internal class LightTemplateStorage : TemplateStorage<LightTemplate>
    {
        internal static readonly LightTemplate DEFAULT = new LightTemplate("DEFAULT")
                                                                        .WithLight(1.0F, 20.0F, Color.white)
                                                                            .WithSnipeOffset(0.1F);

        internal static readonly LightTemplate SODIUM = new LightTemplate("SODIUM")
                                                                        .WithLight(1.0F, 20.0F, new Color(1.0F, 0.7F, 0.3F, 1.0F))
                                                                            .WithSnipeOffset(0.1F)
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_orange01.png", 0.75F);

        internal static readonly LightTemplate HALOGEN = new LightTemplate("HALOGEN")
                                                                        .WithLight(1.0F, 20.0F, new Color(0.8666F, 0.9333F, 1.0F))
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_flare02.png");

        internal static readonly LightTemplate TUNGSTEN = new LightTemplate("TUNGSTEN")
                                                                        .WithLight(1.0F, 20.0F, new Color(1.0F, 0.9F, 0.7F, 1.0F))
                                                                            .WithSnipeOffset(0.1F)
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn());

        internal static readonly LightTemplate LAMP_POST = new LightTemplate("LAMP POST")
                                                                        .WithLight(1.0F, 20.0F, new Color(0.8666F, 0.9333F, 1.0F))
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_flare02.png")
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "LampPostSmall_Floor")
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "LampPostSmall_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "LampPostSmall_Ceiling");

        internal static readonly LightTemplate LAMP_POST_DOUBLE = new LightTemplate("LAMP POST DOUBLE")
                                                                        .WithLight(1.0F, 20.0F, new Color(0.8666F, 0.9333F, 1.0F))
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_flare02.png")
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "LampPostDouble_Floor")
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "LampPostDouble_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "LampPostSmall_Ceiling");

        internal static readonly LightTemplate LAMP_POST_SODIUM = new LightTemplate("LAMP POST SODIUM")
                                                                        .WithLight(1.0F, 20.0F, new Color(1.0F, 0.7F, 0.3F, 1.0F))
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_orange01.png", 0.75F)
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "LampPostSmall_Floor")
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "LampPostSmall_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "LampPostSmall_Ceiling");

        internal static readonly LightTemplate LAMP_POST_DOUBLE_SODIUM = new LightTemplate("LAMP POST DOUBLE SODIUM")
                                                                        .WithLight(1.0F, 20.0F, new Color(1.0F, 0.7F, 0.3F, 1.0F))
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_orange01.png", 0.75F)
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "LampPostDouble_Floor")
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "LampPostDouble_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "LampPostSmall_Ceiling");

        internal static readonly LightTemplate YARD_LAMP = new LightTemplate("YARD LAMP")
                                                                        .WithLight(1.0F, 30.0F, Color.white)
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_white01.png")
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "YardLamp_Floor")
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "YardLamp_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "YardLamp_Ceiling");

        internal static readonly LightTemplate INDUSTRIAL_LAMP = new LightTemplate("INDUSTRIAL LAMP")
                                                                        .WithLight(0.5F, 10.0F, Color.white)
                                                                            .WithDutyCycle(DutyCycle.ALWAYS)
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "IndustrialLamp_Floor")
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "IndustrialLamp_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "IndustrialLamp_Ceiling");

        internal static readonly LightTemplate FLOOD_LIGHT = new LightTemplate("FLOOD LIGHT - 24H")
                                                                        .WithLight(0.5F, 10.0F, Color.white)
                                                                            .WithDutyCycle(DutyCycle.ALWAYS)
                                                                            .WithCorona("corona_flare02.png", 0.87F)
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "WorkLamp_Floor")
                                                                            .WithAlignment(MeshTemplate.AlignmentType.AlignToNormal)
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "FloodLight_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "FloodLight_Ceiling");

        internal static readonly LightTemplate FLOOD_LIGHT_DTD = new LightTemplate("FLOOD LIGHT - NIGHT")
                                                                        .WithLight(0.5F, 10.0F, Color.white)
                                                                            .WithDutyCycle(new DutyCycle.DuskTillDawn())
                                                                            .WithCorona("corona_flare02.png", 0.87F)
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "WorkLamp_Floor")
                                                                            .WithAlignment(MeshTemplate.AlignmentType.AlignToNormal)
                                                                        .WithMesh(MeshOrientation.Wall, "meshes_lampposts.assetbundle", "FloodLight_Wall")
                                                                        .WithMesh(MeshOrientation.Ceiling, "meshes_lampposts.assetbundle", "FloodLight_Ceiling");

        internal static readonly LightTemplate ANNOUNCER_BLUE_GLOW = new LightTemplate("announcer blue glow")
                                                                        .WithSnipeOffset(2.0F)
                                                                        .WithLight(0.3F, 5.0F, new Color(0.0F, 0.3F, 1.0F));


        internal static readonly LightTemplate LEVEL_CROSSING = new LightTemplate("LEVEL CROSSING")
                                                                        .WithMesh(MeshOrientation.Floor, "meshes_lampposts.assetbundle", "LevelCrossing");

        protected override void AddDefaults()
        {
            bool saveRequired = false;

            // Add templates for version 1 if saved version is lower
            if (this.Version < 1)
            {
                this.Add(LightTemplateStorage.DEFAULT);
                this.Add(LightTemplateStorage.SODIUM);
                this.Add(LightTemplateStorage.HALOGEN);
                this.Add(LightTemplateStorage.TUNGSTEN);
                this.Add(LightTemplateStorage.LAMP_POST);
                this.Add(LightTemplateStorage.LAMP_POST_DOUBLE);
                this.Add(LightTemplateStorage.LAMP_POST_SODIUM);
                this.Add(LightTemplateStorage.LAMP_POST_DOUBLE_SODIUM);
                this.Add(LightTemplateStorage.YARD_LAMP);
                this.Add(LightTemplateStorage.INDUSTRIAL_LAMP);
                this.Add(LightTemplateStorage.FLOOD_LIGHT);
                this.Add(LightTemplateStorage.FLOOD_LIGHT_DTD);
                // this.Add(MainTemplates.LEVEL_CROSSING);
                this.Version = 1;
                saveRequired = true;
            }

            if (saveRequired)
            {
                this.Save();
            }
        }
    }
}
