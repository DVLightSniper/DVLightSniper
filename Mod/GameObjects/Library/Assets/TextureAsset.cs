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

using DVLightSniper.Mod.Storage;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{
    internal class TextureAsset : FileAsset<Texture2D>
    {
        public const int DEFAULT_TEXTURE_SIZE = 256;

        public TextureAsset(AssetLoader assetLoader, string assetBundleName, string assetName, AssetLoader.LookupFlags lookupFlags = AssetLoader.LookupFlags.Default)
            : base(assetLoader, AssetType.TEXTURE, assetBundleName, assetName, lookupFlags)
        {
        }

        protected override Texture2D CreateAsset(MetaStorage meta)
        {
            int textureSize = meta?["textureSize"].ValueOrDefault(TextureAsset.DEFAULT_TEXTURE_SIZE) ?? TextureAsset.DEFAULT_TEXTURE_SIZE;
            return new Texture2D(textureSize, textureSize);
        }

        protected override bool LoadAsset(byte[] data)
        {
            this.Asset.LoadImage(data);
            return true;
        }
    }
}
