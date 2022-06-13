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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CommandTerminal;

using DVLightSniper.Mod.GameObjects;
using DVLightSniper.Mod.GameObjects.Spawners.Packs;
using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.Util;

using UnityEngine;

using Debug = System.Diagnostics.Debug;

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
                ConsoleCommands.RegisterCommand(null,          "LightSniper.InstallPack",        ConsoleCommands.InstallPack,        1,  1, "Extract the specified built-in pack");
                ConsoleCommands.RegisterCommand(null,          "LightSniper.EnablePack",         ConsoleCommands.EnablePack,         1,  1, "Enable the specified pack");
                ConsoleCommands.RegisterCommand(null,          "LightSniper.DisablePack",        ConsoleCommands.DisablePack,        1,  1, "Disable the specified pack");
                ConsoleCommands.RegisterCommand("BG",          "LightSniper.BeginGroup",         ConsoleCommands.BeginGroup,         1,  1, "Begin editing a group of lights/meshes/decorations");
                ConsoleCommands.RegisterCommand("EG",          "LightSniper.EndGroup",           ConsoleCommands.EndGroup,           0,  0, "End editing the current group (resets group to \"user\")");
                ConsoleCommands.RegisterCommand("LG",          "LightSniper.ListGroups",         ConsoleCommands.ListGroups,         0,  2, "List all groups in the current region or the specified region");
                ConsoleCommands.RegisterCommand("LON",         "LightSniper.EnableGroup",        ConsoleCommands.EnableGroup,        0,  2, "Enable all groups, the specified group, or a specific group in a region");
                ConsoleCommands.RegisterCommand("LOFF",        "LightSniper.DisableGroup",       ConsoleCommands.DisableGroup,       0,  2, "Disable all groups, the specified group, or a specific group in a region");
                ConsoleCommands.RegisterCommand("PRIO",        "LightSniper.GroupPriority",      ConsoleCommands.GroupPriority,      0,  3, "Set the priority of the specified group");
                ConsoleCommands.RegisterCommand("RGN",         "LightSniper.SetRegion",          ConsoleCommands.SetRegion,          0,  1, "Set the override region to use for overlapping regions");
                ConsoleCommands.RegisterCommand(null,          "LightSniper.SaveTemplate",       ConsoleCommands.SaveTemplate,       1, -1, "Save the current <USER DEFINED> template as a named template");
                ConsoleCommands.RegisterCommand(null,          "LightSniper.ReloadTemplates",    ConsoleCommands.ReloadTemplates,    0,  0, "Reload templates from JSON files (takes effect when radio is next active)");
                ConsoleCommands.RegisterCommand(null,          "LightSniper.CleanUpOnExit",      ConsoleCommands.CleanUpOnExit,      0,  1, "Removes empty region folders when exiting the game, useful before zipping a pack");
                ConsoleCommands.RegisterCommand("PROPGET",     "LightSniper.GetProperty",        ConsoleCommands.GetProperty,        0,  1, "Get the specified global property");
                ConsoleCommands.RegisterCommand("PROPSET",     "LightSniper.SetProperty",        ConsoleCommands.SetProperty,        1, -1, "Set the specified global property to the specified value");

                // hidden from autocomplete, dev only
                ConsoleCommands.RegisterCommand("MARKERS",     "LightSniper.ToggleMarkers",      ConsoleCommands.ToggleMarkers,      0,  0);
                ConsoleCommands.RegisterCommand("DBGTOGGLE",   "LightSniper.ToggleDebugOverlay", ConsoleCommands.ToggleDebugOverlay, 0,  0);
                ConsoleCommands.RegisterCommand("DBGLEVEL",    "LightSniper.SetDebugLevel",      ConsoleCommands.SetDebugLevel,      1, -1);
                ConsoleCommands.RegisterCommand("MARKORPHANS", "LightSniper.MarkOrphans",        ConsoleCommands.MarkOrphans,        0,  0);
                ConsoleCommands.RegisterCommand("KILLORPHANS", "LightSniper.KillOrphans",        ConsoleCommands.KillOrphans,        0,  0);
                ConsoleCommands.RegisterCommand("FREEORPHANS", "LightSniper.FreeOrphans",        ConsoleCommands.FreeOrphans,        0,  0);

                ConsoleCommands.registered = true;
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }
        }

        private static void RegisterCommand(string shortName, string fqName, Action<CommandArg[]> proc, int minArgs = 0, int maxArgs = -1, string help = null, string hint = null)
        {
            if (shortName != null)
            {
                Terminal.Shell.AddCommand(shortName, proc, minArgs, maxArgs, null, hint, true);
            }
            Terminal.Shell.AddCommand(fqName, proc, minArgs, maxArgs, help ?? "", hint, help == null);
            if (help != null)
            {
                Terminal.Autocomplete.Register(fqName);
            }
        }

        private static void InstallPack(CommandArg[] args)
        {
            if (ConsoleCommands.GetNameFromArgs(args, false, false, out string packId, 0, "Pack") && PackLoader.Unpack(packId))
            {
                Terminal.Log(TerminalLogType.Message, "Pack successfully installed");
                LightSniper.SpawnerController?.Reload();
            }
            else
            {
                Terminal.Log(TerminalLogType.Warning, "Could not install pack {0}, maybe it's already installed", packId);
            }
        }

        private static void EnablePack(CommandArg[] args)
        {
            ConsoleCommands.EnablePack(args, true);
        }

        private static void DisablePack(CommandArg[] args)
        {
            ConsoleCommands.EnablePack(args, false);
        }

        private static void EnablePack(CommandArg[] args, bool enabled)
        {
            if (!ConsoleCommands.GetNameFromArgs(args, false, false, out string packId, 0, "Pack"))
            {
                return;
            }

            Pack pack = PackLoader.Get(packId);
            if (pack == null)
            {
                Terminal.Log(TerminalLogType.Warning, "Pack {0} not found", packId);
                return;
            }

            if (pack.Enabled == enabled)
            {
                Terminal.Log(TerminalLogType.Message, "Pack {0} is already {1}", packId, enabled ? "Enabled" : "Disabled");
                return;
            }

            pack.Enabled = enabled;
            Terminal.Log(TerminalLogType.Message, "{0} pack {1}", enabled ? "Enabled" : "Disabled", packId);
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

        private static void ListGroups(CommandArg[] args)
        {
            string yardId = "";
            int page = 1;
            if (args.Length > 0)
            {
                if (args.Length == 1 && int.TryParse(args[0].String, out int pg))
                {
                    page = pg;
                }
                else if (!ConsoleCommands.GetNameFromArgs(args, true, false, out yardId, 0, "Region"))
                {
                    return;
                }
            }

            if (args.Length > 1 && !int.TryParse(args[1].String, out page))
            {
                Terminal.Log(TerminalLogType.Error, "Invalid page number {0}", args[1].String);
                return;
            }

            LightSniper.SpawnerController?.ListGroups(yardId, page);
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
                if (!ConsoleCommands.GetNameFromArgs(args, true, false, out yardId, 0, "Region"))
                {
                    return;
                }
                groupArgIndex = 1;
            }

            if (ConsoleCommands.GetNameFromArgs(args, true, true, out string groupName, groupArgIndex))
            {
                LightSniper.SpawnerController?.EnableGroup(yardId, groupName, enabled);
            }
        }

        private static void GroupPriority(CommandArg[] args)
        {
            string yardId = "*";
            int groupArgIndex = 0;
            if (args.Length > 2)
            {
                if (!ConsoleCommands.GetNameFromArgs(args, true, false, out yardId, 0, "Region"))
                {
                    return;
                }
                groupArgIndex = 1;
            }

            if (ConsoleCommands.GetNameFromArgs(args, true, true, out string groupName, groupArgIndex))
            {
                if (args.Length < groupArgIndex + 2)
                {
                    Terminal.Log(TerminalLogType.Error, "New priority must be specified");
                    return;
                }

                if (Enum.TryParse(args[groupArgIndex + 1].String, true, out Priority priority))
                {
                    LightSniper.SpawnerController?.SetGroupPriority(yardId, groupName, priority);
                }
                else
                {
                    Terminal.Log(TerminalLogType.Error, "Unrecognised priority '{0}'", args[groupArgIndex + 1].String);
                }
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

        private static void ReloadTemplates(CommandArg[] args)
        {
            CommsRadioLightSniper.TemplateReloadRequested = true;
            GameObject radioControllerHolder = GameObject.Find("LightSniperRadioController");
            if (radioControllerHolder == null)
            {
                Terminal.Log(TerminalLogType.Message, "Templates will be reloaded when radio is activated");
            }
        }

        private static void CleanUpOnExit(CommandArg[] args)
        {
            if (args.Length == 1)
            {
                LightSniper.Settings.cleanUpOnExit = args[0].Bool;
            }
            else
            {
                LightSniper.Settings.cleanUpOnExit = !LightSniper.Settings.cleanUpOnExit;
            }

            Terminal.Log(TerminalLogType.Message, "cleanUpOnExit is now {0}", LightSniper.Settings.cleanUpOnExit);
            LightSniper.Settings.Save(LightSniper.ModEntry);
        }

        private static void GetProperty(CommandArg[] args)
        {
            if (args.Length == 0)
            {
                foreach (KeyValuePair<string, string> kv in GlobalProperties.Instance.GetAll())
                {
                    Terminal.Log(TerminalLogType.Message, "{0}=<color=#{2}>{1}</color>", kv.Key, kv.Value ?? "null", kv.Value == null ? "FF0000" : "00FF00");
                }
                return;
            }

            string key = args[0].String;
            Match match = new Regex(@"^(?<group>[a-z_\-]+(?<key>\.[a-z0-9_\-]+)?)$", RegexOptions.IgnoreCase).Match(key);
            if (!match.Success)
            {
                Terminal.Log(TerminalLogType.Error, "'{0}' is not a valid property key.", key);
                return;
            }

            if (!match.Groups["key"].Success)
            {
                foreach (KeyValuePair<string, string> kv in GlobalProperties.Instance.GetAll(match.Groups["group"].Value))
                {
                    Terminal.Log(TerminalLogType.Message, "{0}=<color=#{2}>{1}</color>", kv.Key, kv.Value ?? "null", kv.Value == null ? "FF0000" : "00FF00");
                }
                return;
            }

            string value = GlobalProperties.Instance.Get(key, null);
            Terminal.Log(TerminalLogType.Message, "{0}=<color=#{2}>{1}</color>", key, value ?? "null", value == null ? "FF0000" : "00FF00");
        }

        private static void SetProperty(CommandArg[] args)
        {
            string key = args[0].String;
            if (!new Regex(@"^([a-z_\-]+\.[a-z0-9_\-]+)$", RegexOptions.IgnoreCase).IsMatch(key))
            {
                Terminal.Log(TerminalLogType.Error, "'{0}' is not a valid property key.", key);
                return;
            }

            string value = args.Length > 1 ? ConcatenateArgs(args, 1) : null;
            GlobalProperties.Instance.Set(key, value);
            Terminal.Log(TerminalLogType.Message, "{0}=<color=#{2}>{1}</color>", key, value ?? "null", value == null ? "FF0000" : "00FF00");
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
            if (!new Regex(regex, RegexOptions.IgnoreCase).IsMatch(name))
            {
                Terminal.Log(TerminalLogType.Error, "{0} name '{1}' is not a valid {0} name", type, name);
                return false;
            }
            return true;
        }

        private static bool TemplateNameFromArgs(CommandArg[] args, out string templateName)
        {
            templateName = ConsoleCommands.ConcatenateArgs(args).ToLowerInvariant();
            if (!new Regex(@"^[a-z0-9 \(\)\[\]\.\,\@\+\-\=\{\}\#\$\!_]+$").IsMatch(templateName))
            {
                Terminal.Log(TerminalLogType.Error, "Template name '{0}' not a valid template name", templateName);
                return false;
            }
            return true;
        }

        private static string ConcatenateArgs(CommandArg[] args, int startIndex = 0)
        {
            StringBuilder sb = new StringBuilder();
            for (int index = startIndex; index < args.Length; index++)
            {
                CommandArg arg = args[index];
                sb.Append(' ').Append(arg.String);
            }
            return sb.ToString().Trim();
        }

        private static void ToggleMarkers(CommandArg[] args)
        {
            LightSniper.Settings.showDebugMarkers = !LightSniper.Settings.showDebugMarkers;
            LightSniper.Settings.Save(LightSniper.ModEntry);
        }

        private static void ToggleDebugOverlay(CommandArg[] args)
        {
            DebugOverlay.Visible = !DebugOverlay.Visible;
        }

        private static void SetDebugLevel(CommandArg[] args)
        {
            int debugLevel = 0;

            foreach (CommandArg arg in args)
            {
                if (int.TryParse(arg.String, out int value))
                {
                    debugLevel |= value;
                }
                else if (Enum.TryParse(arg.String, true, out SpawnerController.TimingLevel level))
                {
                    debugLevel |= (int)level;
                }
                else
                {
                    Terminal.Log(TerminalLogType.Warning, "Unrecognised level argument {0}", arg.String);
                }
            }

            LightSniper.Settings.debugLevel = debugLevel;
            LightSniper.Settings.Save(LightSniper.ModEntry);
        }

        private static void MarkOrphans(CommandArg[] args)
        {
            SpawnerController.MarkOrphans = !SpawnerController.MarkOrphans;
            Terminal.Log(TerminalLogType.Message, "Mark orphans is now " + SpawnerController.MarkOrphans);
        }

        private static void KillOrphans(CommandArg[] args)
        {
            SpawnerController.KillOrphans = true;
            Terminal.Log(TerminalLogType.Message, "Orphans will be killed for 10 seconds");
        }

        private static void FreeOrphans(CommandArg[] args)
        {
            SpawnerController.FreeOrphans = true;
            Terminal.Log(TerminalLogType.Message, "Inhibited orphans will be released for 10 seconds");
        }
    }
}
