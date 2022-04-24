using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DVLightSniper.Mod.GameObjects.Spawners.Packs
{
    /// <summary>
    /// Key/value store used to keep external metadata for packs
    /// </summary>
    [DataContract]
    internal class PackMeta
    {
        internal event Action Changed;

        [DataMember(Name = "flags", Order = 0, EmitDefaultValue = false)]
        private Dictionary<string, bool> flags = new Dictionary<string, bool>();

        [DataMember(Name = "strings", Order = 1, EmitDefaultValue = false)]
        private Dictionary<string, string> strings = new Dictionary<string, string>();

        [DataMember(Name = "numbers", Order = 2, EmitDefaultValue = false)]
        private Dictionary<string, int> numbers = new Dictionary<string, int>();

        internal bool GetFlag(string key, bool defaultValue = false)
        {
            return this.flags.ContainsKey(key) ? this.flags[key] : defaultValue;
        }

        internal void SetFlag(string key, bool value)
        {
            this.flags[key] = value;
            this.Changed?.Invoke();
        }

        internal string GetString(string key, string defaultValue = "")
        {
            return this.strings.ContainsKey(key) ? this.strings[key] : defaultValue;
        }

        internal void SetString(string key, string value)
        {
            this.strings[key] = value;
            this.Changed?.Invoke();
        }

        internal int GetNumber(string key, int defaultValue = 0)
        {
            return this.numbers.ContainsKey(key) ? this.numbers[key] : defaultValue;
        }

        internal void SetNumber(string key, int value)
        {
            this.numbers[key] = value;
            this.Changed?.Invoke();
        }
    }
}
