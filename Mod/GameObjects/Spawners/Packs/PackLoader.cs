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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Resources = DVLightSniper.Properties.Resources;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    internal class PackLoader
    {
        private static readonly Dictionary<string, string> INTERNAL_PACKS = new Dictionary<string, string>()
        {
            { "default", "Default_Pack_1.0.zip" },
            { "level_crossings", "Level_Crossings_Pack_1.0.zip" }
        };

        internal static string Dir { get; }

        private static List<Pack> packs;

        private static Metadata metadata;

        internal static IEnumerable<Pack> Packs
        {
            get
            {
                return PackLoader.packs ?? (PackLoader.packs = PackLoader.Discover());
            }
        }

        static PackLoader()
        {
            PackLoader.Dir = Path.Combine(LightSniper.Path, "Packs");
            Directory.CreateDirectory(PackLoader.Dir);
        }

        internal static bool Unpack(string packId)
        {
            packId = packId.ToLowerInvariant();
            if (!PackLoader.INTERNAL_PACKS.ContainsKey(packId))
            {
                return false;
            }

            string packFileName = PackLoader.INTERNAL_PACKS[packId];
            string outFileName = Path.Combine(PackLoader.Dir, packFileName);
            if (File.Exists(outFileName))
            {
                return false;
            }

            try
            {
                object data = Resources.ResourceManager.GetObject(packFileName, Resources.Culture);
                if (data != null)
                {
                    File.WriteAllBytes(outFileName, (byte[])data);
                    PackLoader.packs.Add(new ZippedPack(outFileName, PackLoader.metadata));
                    return true;
                }
            }
            catch (Exception e)
            {
                LightSniper.Logger.Error(e);
            }

            return false;
        }

        internal static Pack Get(string packId)
        {
            return PackLoader.Packs.FirstOrDefault(pack => string.Equals(pack.Id, packId, StringComparison.InvariantCultureIgnoreCase));
        }

        private static List<Pack> Discover()
        {
            PackLoader.metadata = Metadata.Load(Path.Combine(LightSniper.ResourcesPath, "packs.metadata.json"));
            Dictionary<string, Pack> discovered = new Dictionary<string, Pack>();
            DirectoryInfo dir = new DirectoryInfo(PackLoader.Dir);
            if (!dir.Exists)
            {
                return new List<Pack>();
            }

            void AddCandidate(Pack pack)
            {
                if (pack.Valid)
                {
                    LightSniper.Logger.Info("{0} is a valid pack with ID {1} and Version {2}", pack.Name, pack.Id, pack.Version);
                    if (!discovered.ContainsKey(pack.Id))
                    {
                        discovered[pack.Id] = pack;
                        return;
                    }

                    Pack existing = discovered[pack.Id];
                    LightSniper.Logger.Info("{0} has conflicting ID {1} with previously discovered {2}", pack.Name, pack.Id, existing.Name);

                    if (!Version.TryParse(existing.Version, out Version existingVersion))
                    {
                        existingVersion = new Version(0, 0, 0, 0);
                    }

                    if (!Version.TryParse(pack.Version, out Version packVersion))
                    {
                        packVersion = new Version(0, 0, 0, 0);
                    }

                    if (existingVersion == packVersion)
                    {
                        LightSniper.Logger.Info("{0} is the same version as {1} (or versions are invalid), keeping {1}", pack.Name, existing.Name);
                        pack.Dispose();
                    }
                    else if (existingVersion > packVersion)
                    {
                        LightSniper.Logger.Info("{0} is an older version than {1}, keeping {1}", pack.Name, existing.Name);
                        pack.Dispose();
                    }
                    else if (existingVersion < packVersion)
                    {
                        LightSniper.Logger.Info("{0} is an newer version than {1}, replacing with {0}", pack.Name, existing.Name);
                        discovered[pack.Id] = pack;
                        existing.Dispose();
                    }
                }
                else
                {
                    pack.Dispose();
                }
            }

            LightSniper.Logger.Info("Searching for packs in {0}", dir.FullName);
            foreach (FileInfo zipFile in dir.EnumerateFiles("*.zip"))
            {
                LightSniper.Logger.Info("Found candidate zip file {0}", zipFile.FullName);
                AddCandidate(new ZippedPack(zipFile.FullName, PackLoader.metadata));
            }
            foreach (DirectoryInfo directory in dir.EnumerateDirectories())
            {
                LightSniper.Logger.Info("Found candidate folder {0}", directory.FullName);
                AddCandidate(new FolderPack(directory.FullName, PackLoader.metadata));
            }

            return discovered.Values.ToList();
        }

        internal static IEnumerable<string> Find(string path, string extension = null)
        {
            List<string> results = new List<string>();
            foreach (Pack pack in PackLoader.Packs)
            {
                results.AddRange(pack.Find(path, extension));
            }
            return results.Distinct();
        }

        internal static bool Contains(string path)
        {
            return PackLoader.Packs.Any(pack => pack.Contains(path));
        }

        internal static DateTime GetLastWriteTime(string path)
        {
            return PackLoader.Packs.Select(pack => pack.GetLastWriteTime(path)).FirstOrDefault(date => date != DateTime.MinValue);
        }

        internal static PackStream OpenStream(string path)
        {
            return PackLoader.Packs.Select(pack => pack.OpenStream(path)).FirstOrDefault(stream => stream != null);
        }

        internal static PackResource OpenResource(string path)
        {
            return PackLoader.Packs.Select(pack => pack.OpenResource(path)).FirstOrDefault(resource => resource != null);
        }

        internal static PackJson OpenJson(string path)
        {
            return PackLoader.Packs.Select(pack => pack.OpenJson(path)).FirstOrDefault(json => json != null);
        }

        internal static PackMetaStorage OpenMeta(string path)
        {
            return PackLoader.Packs.Select(pack => pack.OpenMeta(path)).FirstOrDefault(json => json != null);
        }
    }
}
