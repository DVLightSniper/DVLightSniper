using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// An unpacked pack, in a folder
    /// </summary>
    internal class FolderPack : Pack
    {
        public FolderPack(string path, PackMetaStorage metaStorage)
            : base(path, metaStorage)
        {
        }

        internal IEnumerable<FileInfo> Files
        {
            get => new DirectoryInfo(this.Path).EnumerateFiles("*.*", SearchOption.AllDirectories);
        }

        internal override IEnumerable<string> Find(string path, string extension = null)
        {
            List<string> results = new List<string>();
            foreach (FileInfo file in this.Files)
            {
                string name = file.RelativeToDir(this.Path);
                if (name.StartsWith(path) && (extension == null || name.EndsWith(extension)))
                {
                    results.Add(name);
                }
            }
            return results;
        }

        internal override Stream OpenStream(string path)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(this.Path, path);
                if (!File.Exists(fullPath))
                {
                    return null;
                }
                return new FileStream(fullPath, FileMode.Open);
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
                return null;
            }
        }

        internal override byte[] OpenResource(string path)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(this.Path, path);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
            }
            return null;
        }
    }
}
