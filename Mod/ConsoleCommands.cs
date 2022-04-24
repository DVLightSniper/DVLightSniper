﻿//------------------------------------------------------------------------------
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CommandTerminal;

using DVLightSniper.Mod.GameObjects;
using DVLightSniper.Mod.Util;

using UnityEngine;

namespace DVLightSniper.Mod
{
    /// <summary>
    /// LightSniper console commands
    /// </summary>
    internal class ConsoleCommands
    {
        private static bool registered = false;

        internal static void Register()
        {
            // Only do this once
            if (ConsoleCommands.registered)
            {
                return;
            }

            try
            {
                ConsoleCommands.RegisterCommand(null,          "LightSniper.InstallDefaultPack", ConsoleCommands.InstallDefaultPack, 0, 0);
                ConsoleCommands.RegisterCommand("BG",          "LightSniper.BeginGroup",         ConsoleCommands.BeginGroup,         1, 1);
                ConsoleCommands.RegisterCommand("EG",          "LightSniper.EndGroup",           ConsoleCommands.EndGroup,           0, 0);
                ConsoleCommands.RegisterCommand("LON",         "LightSniper.EnableGroup",        ConsoleCommands.EnableGroup,        0, 2);
                ConsoleCommands.RegisterCommand("LOFF",        "LightSniper.DisableGroup",       ConsoleCommands.DisableGroup,       0, 2);
                ConsoleCommands.RegisterCommand("RGN",         "LightSniper.SetRegion",          ConsoleCommands.SetRegion,          0, 1);
                ConsoleCommands.RegisterCommand(null,          "LightSniper.SaveTemplate",       ConsoleCommands.SaveTemplate,       1   );
                ConsoleCommands.RegisterCommand(null,          "LightSniper.ReloadTemplates",    ConsoleCommands.ReloadTemplates,    0, 0);

                // hidden from autocomplete, dev only
                ConsoleCommands.RegisterCommand("MARKERS",     "LightSniper.ToggleMarkers",      ConsoleCommands.ToggleMarkers,      0, 0, false);
                ConsoleCommands.RegisterCommand("DBGTOGGLE",   "LightSniper.ToggleDebugOverlay", ConsoleCommands.ToggleDebugOverlay, 0, 0, false);
                ConsoleCommands.RegisterCommand("MARKORPHANS", "LightSniper.MarkOrphans",        ConsoleCommands.MarkOrphans,        0, 0, false);
                ConsoleCommands.RegisterCommand("KILLORPHANS", "LightSniper.KillOrphans",        ConsoleCommands.KillOrphans,        0, 0, false);
                ConsoleCommands.RegisterCommand("FREEORPHANS", "LightSniper.FreeOrphans",        ConsoleCommands.FreeOrphans,        0, 0, false);

                ConsoleCommands.registered = true;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }
        }

        private static void RegisterCommand(string shortName, string fqName, Action<CommandArg[]> proc, int minArgs = 0, int maxArgs = -1, bool registerAutocomplete = true, string help = "", string hint = null)
        {
            if (shortName != null)
            {
                Terminal.Shell.AddCommand(shortName, proc, minArgs, maxArgs, help, hint);
            }
            Terminal.Shell.AddCommand(fqName, proc, minArgs, maxArgs, help, hint);
            if (registerAutocomplete)
            {
                Terminal.Autocomplete.Register(fqName);
            }
        }

        private static void InstallDefaultPack(CommandArg[] args)
        {
            if (LightSniper.SpawnerController != null && LightSniper.SpawnerController.InstallDefaultPack())
            {
                Terminal.Log(TerminalLogType.Message, "Default pack successfully installed");
            }
            else
            {
                Terminal.Log(TerminalLogType.Warning, "Could not install default pack, maybe it's already installed");
            }
        }

        private static void BeginGroup(CommandArg[] args)
        {
            if (ConsoleCommands.GetNameFromArgs(args, false, false, out string groupName))
            {
                LightSniper.SpawnerController?.BeginGroup(groupName);
            }
        }

        private static void EndGroup(CommandArg[] args)
        {
            LightSniper.SpawnerController?.EndGroup();
        }

        private static void EnableGroup(CommandArg[] args)
        {
            ConsoleCommands.EnableGroup(args, true);
        }

        private static void DisableGroup(CommandArg[] args)
        {
            ConsoleCommands.EnableGroup(args, false);
        }

        private static void EnableGroup(CommandArg[] args, bool enabled)
        {
            string yardId = "*";
            int groupArgIndex = 0;
            if (args.Length > 1)
            {
                ConsoleCommands.GetNameFromArgs(args, true, false, out yardId, 0, "Region", "");
                groupArgIndex = 1;
            }

            if (ConsoleCommands.GetNameFromArgs(args, true, true, out string groupName, groupArgIndex))
            {
                LightSniper.SpawnerController?.EnableGroup(yardId, groupName, enabled);
            }
        }

        private static void SetRegion(CommandArg[] args)
        {
            if (ConsoleCommands.GetNameFromArgs(args, true, false, out string regionName, 0, "Region", ""))
            {
                string yardId = LightSniper.SpawnerController?.SetRegion(regionName);
                if (yardId != null)
                {
                    Terminal.Log(TerminalLogType.Message, "Selected region is {0}", yardId);
                }
                else
                {
                    Terminal.Log(TerminalLogType.Error, "Could not set region, the specified region {0} was not found", regionName);
                }
            }
        }

        private static void SaveTemplate(CommandArg[] args)
        {
            if (ConsoleCommands.TemplateNameFromArgs(args, out string templateName))
            {
                GameObject radioController = GameObject.Find("LightSniperRadioController");
                CommsRadioLightSniper lsRadio = radioController?.GetComponent<CommsRadioLightSniper>();
                if (lsRadio != null)
                {
                    if (lsRadio.SaveTemplate(templateName))
                    {
                        Terminal.Log(TerminalLogType.Message, "Saved template <{0}>", templateName);
                    }
                    else
                    {
                        Terminal.Log(TerminalLogType.Error, "Could not save template, no light is currently selected");
                    }
                }
            }
        }

        private static void ReloadTemplates(CommandArg[] obj)
        {
            GameObject radioControllerHolder = GameObject.Find("LightSniperRadioController");
            if (radioControllerHolder != null)
            {
                CommsRadioLightSniper radioController = radioControllerHolder.GetComponent<CommsRadioLightSniper>();
                radioController.ReloadTemplates();
                Terminal.Log(TerminalLogType.Message, "Templates reloaded from disk");
            }
        }

        private static bool GetNameFromArgs(CommandArg[] args, bool allowNone, bool allowColon, out string name, int argIndex = 0, string type = "Group", string defaultValue = "*")
        {
            if (args.Length < argIndex + 1)
            {
                if (allowNone)
                {
                    name = defaultValue;
                    return true;
                }

                name = "";
                Terminal.Log(TerminalLogType.Error, "{0} name must be specified", type);
                return false;
            }

            name = args[argIndex].String.ToLowerInvariant();
            if (allowNone && name == defaultValue)
            {
                return true;
            }

            string regex = allowColon ? @"^[a-z0-9\(\)\[\]\.\,\@\+\-\=\{\}\#\$\!_]+(:[a-z0-9\(\)\[\]\.\,\@\+\-\=\{\}\#\$\!_]+)?$" : @"^[a-z0-9\(\)\[\]\.\,\@\+\-\=\{\}\#\$\!_]+$";
            if (!new Regex(regex).IsMatch(name))
            {
                Terminal.Log(TerminalLogType.Error, "{0} name '{1}' is not a valid {0} name", type, name);
                return false;
            }
            return true;
        }

        private static bool TemplateNameFromArgs(CommandArg[] args, out string templateName)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var arg in args)
            {
                sb.Append(' ').Append(arg.String);
            }
            templateName = sb.ToString().Trim().ToLowerInvariant();
            if (!new Regex(@"^[a-z0-9 \(\)\[\]\.\,\@\+\-\=\{\}\#\$\!_]+$").IsMatch(templateName))
            {
                Terminal.Log(TerminalLogType.Error, "Template name '{0}' not a valid template name", templateName);
                return false;
            }
            return true;
        }

        private static void ToggleMarkers(CommandArg[] obj)
        {
            LightSniper.Settings.showDebugMarkers = !LightSniper.Settings.showDebugMarkers;
            LightSniper.Settings.Save(LightSniper.ModEntry);
        }

        private static void ToggleDebugOverlay(CommandArg[] obj)
        {
            DebugOverlay.Visible = !DebugOverlay.Visible;
        }

        private static void MarkOrphans(CommandArg[] obj)
        {
            SpawnerController.MarkOrphans = !SpawnerController.MarkOrphans;
            Terminal.Log(TerminalLogType.Message, "Mark orphans is now " + SpawnerController.MarkOrphans);
        }

        private static void KillOrphans(CommandArg[] obj)
        {
            SpawnerController.KillOrphans = true;
            Terminal.Log(TerminalLogType.Message, "Orphans will be killed for 10 seconds");
        }

        private static void FreeOrphans(CommandArg[] obj)
        {
            SpawnerController.FreeOrphans = true;
            Terminal.Log(TerminalLogType.Message, "Inhibited orphans will be released for 10 seconds");
        }
    }
}