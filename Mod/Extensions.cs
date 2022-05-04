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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DVLightSniper.Mod.Components;
using DVLightSniper.Mod.GameObjects;
using DVLightSniper.Mod.Util;

using UnityEngine;

namespace DVLightSniper.Mod
{
    /// <summary>
    /// Convenience extensions
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Gets an existing component, or creates a new one if not present
        /// </summary>
        /// <typeparam name="T">Type of component to get or create</typeparam>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static T GetOrCreate<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject?.GetComponent<T>();
            return component ?? gameObject?.AddComponent<T>();
        }

        public static bool IsTerrain(this GameObject gameObject)
        {
            return gameObject.GetComponent<Terrain>() != null;
        }

        /// <summary>
        /// Get whether the object is a suitable mesh/light anchor, used when finding a unique
        /// parent for a snipe
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="allowedMeshGroup"></param>
        /// <returns></returns>
        public static bool IsSuitableAnchor(this GameObject gameObject, string allowedMeshGroup)
        {
            return gameObject != null
                    && gameObject.tag != "Player"
                    && gameObject.GetComponent<Terrain>() == null
                    && gameObject.IsAllowedParentMesh(allowedMeshGroup)
                    && gameObject.GetComponentInParent<LightComponent>() == null
                    && gameObject.GetComponentInParent<SceneSplitManager>() != null
                    && gameObject.GetComponentInParent<Camera>() == null
                    && gameObject.GetComponents<__SingletonBehaviourBase>().Length == 0
                    && gameObject.GetComponentsInParent<__SingletonBehaviourBase>().Length == 0
                    && gameObject.transform.lossyScale == Vector3.one;
        }

        public static bool IsAllowedParentMesh(this GameObject gameObject, string allowedMeshGroup)
        {
            MeshComponent meshComponent = gameObject.GetComponentInParent<MeshComponent>();
            return meshComponent == null || (allowedMeshGroup != null && meshComponent.Spawner.Group.Name == allowedMeshGroup);
        }

        public static float DistanceSq2d(this Vector3 position, Vector3 other)
        {
            float xDelta = position.x - other.x;
            float zDelta = position.z - other.z;
            return xDelta * xDelta + zDelta * zDelta;
        }

        /// <summary>
        /// Return the position of the supplied (offset) transform in "world" (map) coordinates by
        /// subtracting the current world move from the position.
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static Vector3 AsWorldCoordinate(this Transform transform)
        {
            return transform.position - WorldMover.currentMove;
        }

        /// <summary>
        /// Return the position of the supplied (world) transform, 
        /// "real" 
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static Vector3 AsOffsetCoordinate(this Transform transform)
        {
            return transform.position + WorldMover.currentMove;
        }

        public static Vector3 AsWorldCoordinate(this Vector3 vector3)
        {
            return vector3 - WorldMover.currentMove;
        }

        public static Vector3 AsOffsetCoordinate(this Vector3 vector3)
        {
            return vector3 + WorldMover.currentMove;
        }

        public static Vector2 AsMapLocation(this Vector3 vector3)
        {
            return new Vector2(vector3.x, vector3.z);
        }

        public static Vector2 AsMapLocation(this Transform transform)
        {
            Vector3 worldLocation = transform.position - WorldMover.currentMove;
            return new Vector2(worldLocation.x, worldLocation.z);
        }

        public static string AsHex(this Vector3 vector, bool includeSigns = false)
        {
            float xAbs = Math.Abs(vector.x);
            float yAbs = Math.Abs(vector.y);
            float zAbs = Math.Abs(vector.z);

            string signs = includeSigns ? ((vector.x < 0 ? 1 : 0) | (vector.y < 0 ? 2 : 0) | (vector.z < 0 ? 4 : 0)).ToString() : "";

            int xIntegral = (int)xAbs;
            int xDecimals = (int)((xAbs - Math.Truncate(xAbs)) * 100.0F);
            int yIntegral = (int)yAbs;
            int yDecimals = (int)((yAbs - Math.Truncate(yAbs)) * 100.0F);
            int zIntegral = (int)zAbs;
            int zDecimals = (int)((zAbs - Math.Truncate(zAbs)) * 100.0F);

            return $"{xIntegral:X4}{xDecimals:X2}{yIntegral:X2}{yDecimals:X2}{zIntegral:X4}{zDecimals:X2}{signs}";
        }

        public static Vector3 HexToVector(this string hex)
        {
            if (hex == null)
            {
                return Vector3.zero;
            }

            Regex hashRegex = new Regex("(?<xIntegral>[0-9A-F]{4})(?<xDecimals>[0-9A-F]{2})(?<yIntegral>[0-9A-F]{2})(?<yDecimals>[0-9A-F]{2})(?<zIntegral>[0-9A-F]{4})(?<zDecimals>[0-9A-F]{2})(?<sign>[0-7])");
            Match match = hashRegex.Match(hex);
            if (match.Success)
            {
                int xIntegral = int.Parse(match.Groups["xIntegral"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo);
                int xDecimals = int.Parse(match.Groups["xDecimals"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo);
                int yIntegral = int.Parse(match.Groups["yIntegral"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo);
                int yDecimals = int.Parse(match.Groups["yDecimals"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo);
                int zIntegral = int.Parse(match.Groups["zIntegral"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo);
                int zDecimals = int.Parse(match.Groups["zDecimals"].Value, NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo);
                int signs = int.Parse(match.Groups["sign"].Value);

                float xValue = (xIntegral + (xDecimals * 0.01F)) * ((signs & 1) == 1 ? -1.0F : 1.0F);
                float yValue = (yIntegral + (yDecimals * 0.01F)) * ((signs & 2) == 2 ? -1.0F : 1.0F);
                float zValue = (zIntegral + (zDecimals * 0.01F)) * ((signs & 4) == 4 ? -1.0F : 1.0F);
                return new Vector3(xValue, yValue, zValue);
            }
        
            return Vector3.zero;
        }

        public static MeshOrientation GetMeshOrientation(this RaycastHit hitInfo)
        {
            float upFactor = Vector3.Dot(hitInfo.normal, Vector3.up);

            float xFactor = Math.Abs(Vector3.Dot(hitInfo.normal, Vector3.right));
            float yFactor = Math.Abs(upFactor);
            float zFactor = Math.Abs(Vector3.Dot(hitInfo.normal, Vector3.forward));

            return yFactor < Math.Max(xFactor, zFactor) ? MeshOrientation.Wall : (upFactor > 0 ? MeshOrientation.Floor : MeshOrientation.Ceiling);
        }

        public static Quaternion GetMeshRotation(this RaycastHit hitInfo)
        {
            return hitInfo.GetMeshRotation(LightSniper.PlayerDirection);
        }

        public static Quaternion GetMeshRotation(this RaycastHit hitInfo, Vector3 rotation)
        {
            MeshOrientation orientation = hitInfo.GetMeshOrientation();
            if (orientation == MeshOrientation.Wall)
            {
                return Quaternion.LookRotation(Vector3.up, hitInfo.normal);
            }
            return Quaternion.LookRotation(rotation, orientation == MeshOrientation.Floor ? Vector3.up : Vector3.down);
        }

        public static string RelativeToBaseDir(this string dir)
        {
            return dir?.RelativeToDir(LightSniper.Path);
        }

        public static string RelativeToDir(this string dir, string baseDir)
        {
            if (dir == null)
            {
                return null;
            }
            dir = dir.StartsWith(baseDir) ? dir.Substring(baseDir.Length) : dir;
            while (dir.StartsWith("\\"))
            {
                dir = dir.Substring(1);
            }
            return dir;
        }

        public static string RelativeToBaseDir(this FileInfo fileInfo)
        {
            return fileInfo?.RelativeToDir(LightSniper.Path);
        }

        public static string RelativeToDir(this FileInfo fileInfo, string baseDir)
        {
            return fileInfo?.FullName.RelativeToDir(baseDir);
        }

        public static string ConformSlashes(this string path)
        {
            return path?.Replace('/', '\\');
        }
    }
}
