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
    /// A property which tracks a property setting and updates when it is changed by the user
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class TrackingProperty<T> : IDisposable
    {
        /// <summary>
        /// Raised when the computed value of the property changes
        /// </summary>
        internal event Action<T> Changed;

        /// <summary>
        /// The property store containing the property
        /// </summary>
        protected internal readonly IProperties properties;

        /// <summary>
        /// The property key in the store
        /// </summary>
        protected internal readonly string key;

        /// <summary>
        /// The default property value to 
        /// </summary>
        protected internal readonly string defaultPropertyValue;

        protected internal readonly T defaultValue;

        internal string PropertyValue { get; private set; }

        internal T Value { get; private set; }

        protected internal TrackingProperty(string key, string defaultPropertyValue, T defaultValue)
            : this(GlobalProperties.Instance, key, defaultPropertyValue, defaultValue)
        {
        }

        protected internal TrackingProperty(IProperties properties, string key, string defaultPropertyValue, T defaultValue)
        {
            this.properties = properties;
            this.key = key;
            this.PropertyValue = this.defaultPropertyValue = defaultPropertyValue;
            this.Value = this.defaultValue = defaultValue;

            this.properties.SetDefault(key, defaultPropertyValue);
            this.properties.Changed += this.Update;
        }

        public void Dispose()
        {
            this.properties.Changed -= this.Update;
        }

        private void Update(string key)
        {
            if (key == this.key)
            {
                this.Update();
            }
        }

        protected void Update(bool initialise = false)
        {
            string propertyValue = this.properties.Get(this.key, this.defaultPropertyValue);
            if (propertyValue == this.PropertyValue && !initialise)
            {
                return;
            }

            this.PropertyValue = propertyValue;
            T value = this.ComputeValue(this.PropertyValue);
            if (value == null)
            {
                if (this.defaultPropertyValue == this.PropertyValue)
                {
                    return;
                }
                this.Value = this.defaultValue;
            }
            else
            {
                this.Value = value;
            }

            try
            {
                this.Changed?.Invoke(this.Value);
            }
            catch (Exception e)
            {
                LightSniper.Logger.Debug(e);
            }
        }

        protected abstract T ComputeValue(string propertyValue);
    }
}
