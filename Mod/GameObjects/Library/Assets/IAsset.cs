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

using Object = UnityEngine.Object;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{
    /// <summary>
    /// Base interface for assets
    /// </summary>
    public interface IAsset<out TAsset> where TAsset : Object
    {
        /// <summary>
        /// Asset bundle name
        /// </summary>
        string AssetBundleName { get; }

        /// <summary>
        /// Asset name
        /// </summary>
        string AssetName { get; }

        /// <summary>
        /// The loaded asset, if available
        /// </summary>
        TAsset Asset { get; }

        /// <summary>
        /// Type of asset
        /// </summary>
        AssetType Type { get; }

        /// <summary>
        /// Flag which indicates the file was changed on disk, used to trigger a reload of the
        /// sprite the next time it's updated
        /// </summary>
        bool Changed { get; }

        /// <summary>
        /// True if the file was loaded successfully
        /// </summary>
        bool Valid { get; }

        /// <summary>
        /// Reloads the texture resource
        /// </summary>
        void Reload();
    }
}
