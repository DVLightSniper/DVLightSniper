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

namespace DVLightSniper.Mod.GameObjects.Spawners.Properties
{
    /// <summary>
    /// Interface for property stores
    /// </summary>
    internal interface IProperties
    {
        /// <summary>
        /// Raised when any property in the store changes, the key is the argument
        /// </summary>
        event Action<string> Changed;

        /// <summary>
        /// Get a string value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        string Get(string key, string defaultValue = "");

        /// <summary>
        /// Get an integer value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        int Get(string key, int defaultValue = 0);

        /// <summary>
        /// Get a float value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        float Get(string key, float defaultValue = 0.0F);

        /// <summary>
        /// Get a boolean value from the store
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        bool Get(string key, bool defaultValue = false);

        /// <summary>
        /// Set a string value in the store only if it does not already exist
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetDefault(string key, string value);

        /// <summary>
        /// Set a string value in the store and raise the Changed event if the value is different
        /// to the previous value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void Set(string key, string value);

        /// <summary>
        /// Set an integer value in the store and raise the Changed event if the value is different
        /// to the previous value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void Set(string key, int value);

        /// <summary>
        /// Set a boolean value in the store and raise the Changed event if the value is different
        /// to the previous value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void Set(string key, bool value);
    }
}
