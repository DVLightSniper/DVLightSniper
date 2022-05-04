using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// Priority for groups
    /// </summary>
    internal enum Priority
    {
        /// <summary>
        /// High priority, sorted before normal
        /// </summary>
        High,

        /// <summary>
        /// Normal priority
        /// </summary>
        Normal,

        /// <summary>
        /// Low priority, sorted after normal
        /// </summary>
        Low
    }
}
