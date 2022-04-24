using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using DVLightSniper.Mod.Storage;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// Since packs are read-only by design, any information we want to store about them needs to be
    /// external, hence we use this storage to keep such information
    /// </summary>
    [DataContract]
    internal class PackMetaStorage : JsonStorage
    {
        [DataMember(Name = "meta", Order = 0)]
        private Dictionary<string, PackMeta> meta = new Dictionary<string, PackMeta>();

        internal static PackMetaStorage Load(params string[] path)
        {
            return JsonStorage.Load<PackMetaStorage>(path) ?? new PackMetaStorage() { FileName = Path.Combine(path) };
        }

        internal PackMeta Get(Pack pack)
        {
            if (!this.meta.ContainsKey(pack.Id))
            {
                PackMeta packMeta = this.meta[pack.Id] = new PackMeta();
                packMeta.Changed += this.OnChanged;
                return packMeta;
            }
            return this.meta[pack.Id];
        }

        protected override void OnLoaded()
        {
            foreach (PackMeta packMeta in this.meta.Values)
            {
                packMeta.Changed += this.OnChanged;
            }
        }

        private void OnChanged()
        {
            this.Save();
        }
    }
}
