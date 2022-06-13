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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Crosstales.NAudio.Wave;

using DVLightSniper.Mod.GameObjects.Spawners.Packs;

using UnityEngine;

namespace DVLightSniper.Mod.GameObjects.Library.Assets
{
    internal class SoundAsset : FileAsset<AudioClip>
    {
        public SoundAsset(AssetLoader assetLoader, string assetBundleName, string assetName, AssetLoader.LookupFlags lookupFlags = AssetLoader.LookupFlags.Default)
            : base(assetLoader, AssetType.SOUND, assetBundleName, assetName, lookupFlags)
        {
        }

        protected override bool LoadFromFile(string path)
        {
            if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                using (WaveFileReader stream = new WaveFileReader(path))
                {
                    return this.LoadFromStream(stream);
                }
            }

            if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                using (Mp3FileReader stream = new Mp3FileReader(path))
                {
                    return this.LoadFromStream(stream);
                }
            }

            return false;
        }

        protected override bool LoadFromResource(byte[] data)
        {
            return this.LoadFromStream(new WaveFileReader(new MemoryStream(data)));
        }

        protected override bool LoadFromPack(PackStream stream)
        {
            return this.LoadFromStream(new WaveFileReader(stream.Stream));
        }

        private bool LoadFromStream(WaveFileReader stream)
        {
            return this.LoadFromStream(stream, (int)stream.SampleCount);
        }

        private bool LoadFromStream(Mp3FileReader stream)
        {
            FieldInfo fdTotalSamples = typeof(Mp3FileReader).GetField("totalSamples", BindingFlags.Instance | BindingFlags.NonPublic);
            long totalSamples = fdTotalSamples != null ? (long)fdTotalSamples.GetValue(stream) : 10485760L;
            return this.LoadFromStream(stream, (int)totalSamples);
        }

        private bool LoadFromStream(WaveStream stream, int sampleCount)
        {
            int channels = stream.WaveFormat.Channels;
            int bufferSize = sampleCount * channels;
            float[] buffer = new float[bufferSize];
            int readSamples = stream.ToSampleProvider().Read(buffer, 0, bufferSize);

            AudioClip audioClip = AudioClip.Create(this.AssetName, readSamples / channels, channels, stream.WaveFormat.SampleRate, false);
            audioClip.SetData(buffer, 0);

            this.Asset = audioClip;
            return true;
        }
    }
}
