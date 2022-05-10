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

using DVLightSniper.Mod.GameObjects.Spawners.Properties;
using DVLightSniper.Mod.Util;

using JetBrains.Annotations;

using TMPro;

using UnityEngine;

namespace DVLightSniper.Mod.Components.Prefabs
{
    /// <summary>
    /// Component added to to spawned sign prefabs to apply the sign properties. The signs
    /// themselves are made up of several objects: the sign itself, which is a skinned mesh with
    /// bones to allow us to move the 4 corners; the sign legs, whose spacing and height must be
    /// adjusted to fit the sign; and the TextMesh object which actually renders the text. The way
    /// that we adjust these parameters varies based on the orientation of the sign, but basically
    /// we just need to compute the different offsets for each part and then apply them.
    /// </summary>
    [PrefabComponent("Sign")]
    internal class SignComponent : MonoBehaviour
    {
        [UsedImplicitly]
        private void Start()
        {
            MeshProperties properties = this.GetComponent<MeshComponent>().Properties;
            MeshOrientation orientation = properties.AssetName.GetMeshOrientation();
            Rect rect = this.ComputeSignRect(properties, orientation);
            this.UpdateBonePositions(properties, orientation, rect);
            this.UpdatePolePositions(properties, orientation, rect);
            this.UpdateTextMesh(properties, orientation, rect);
            this.CreateCollider(properties, orientation, rect);
        }

        protected Rect ComputeSignRect(IProperties properties, MeshOrientation orientation)
        {
            float width = properties.Get("sign.width", 1.0F).Clamp(0.5F, 15.0F);
            float height = properties.Get("sign.height", 1.0F).Clamp(0.5F, 15.0F);
            float xPos = (width * -0.5F);
            float yPos = orientation == MeshOrientation.Floor ? 1.0F : orientation == MeshOrientation.Ceiling ? height + 0.2F : height * -0.5F;
            return new Rect(xPos, yPos, width, height);
        }

        protected void UpdateBonePositions(IProperties properties, MeshOrientation orientation, Rect rect)
        {
            Transform boneTopLeft     = this.transform.Find("TL");
            Transform boneTopRight    = this.transform.Find("TR");
            Transform boneBottomRight = this.transform.Find("BR");
            Transform boneBottomLeft  = this.transform.Find("BL");

            SkinnedMeshRenderer sign_lod0 = this.gameObject.transform.Find("Sign_LOD0").GetComponent<SkinnedMeshRenderer>();
            SkinnedMeshRenderer sign_lod1 = this.gameObject.transform.Find("Sign_LOD1").GetComponent<SkinnedMeshRenderer>();
            Bounds bounds;

            switch (orientation)
            {
                case MeshOrientation.Wall:
                    boneTopLeft.localPosition     = new Vector3(rect.x,              0.05F, rect.y + rect.height);
                    boneTopRight.localPosition    = new Vector3(rect.x + rect.width, 0.05F, rect.y + rect.height);
                    boneBottomRight.localPosition = new Vector3(rect.x + rect.width, 0.05F, rect.y              );
                    boneBottomLeft.localPosition  = new Vector3(rect.x,              0.05F, rect.y              );
                    bounds = new Bounds(Vector3.zero, new Vector3(rect.width * 2.0F, 0.5F, rect.height * 2.0F));
                    break;

                case MeshOrientation.Ceiling:
                    boneTopLeft.localPosition     = new Vector3(rect.x + rect.width, rect.y - rect.height, 0.0F);
                    boneTopRight.localPosition    = new Vector3(rect.x,              rect.y - rect.height, 0.0F);
                    boneBottomRight.localPosition = new Vector3(rect.x,              rect.y,               0.0F);
                    boneBottomLeft.localPosition  = new Vector3(rect.x + rect.width, rect.y,               0.0F);
                    bounds = new Bounds(Vector3.zero, new Vector3(rect.width * 2.0F, rect.y + rect.height * 1.5F, 0.5F));
                    break;

                default:
                    boneTopLeft.localPosition     = new Vector3(rect.x,              rect.y + rect.height, 0.0F);
                    boneTopRight.localPosition    = new Vector3(rect.x + rect.width, rect.y + rect.height, 0.0F);
                    boneBottomRight.localPosition = new Vector3(rect.x + rect.width, rect.y,               0.0F);
                    boneBottomLeft.localPosition  = new Vector3(rect.x,              rect.y,               0.0F);
                    bounds = new Bounds(Vector3.zero, new Vector3(rect.width * 2.0F, rect.y + rect.height * 1.5F, 0.5F));
                    break;
            }

            // Update bounds for the renderers otherwise it will get frustum culled
            sign_lod0.localBounds = sign_lod1.localBounds = bounds;
        }

        protected void UpdatePolePositions(IProperties properties, MeshOrientation orientation, Rect rect)
        {
            Transform pole1 = this.transform.Find("Pole001");
            Transform pole2 = this.transform.Find("Pole002");

            float yOffset = 0.0F;
            float zScale = rect.height - 0.2F;

            switch (orientation)
            {
                case MeshOrientation.Wall:
                    yOffset = 0.05F;

                    Transform strut1 = this.transform.Find("Strut001");
                    Transform strut2 = this.transform.Find("Strut002");

                    strut1.localPosition = new Vector3(0.0F, yOffset, rect.height * 0.5F - 0.2F);
                    strut2.localPosition = new Vector3(0.0F, yOffset, rect.height * -0.5F + 0.2F);
                    strut1.localScale = strut2.localScale = new Vector3(1.0F, 1.0F, rect.width - 0.25F);

                    break;

                case MeshOrientation.Ceiling:
                    zScale = rect.y - 0.1F;
                    break;

                default:
                    zScale = (rect.y - 0.1F + rect.height) / 1.5F;
                    break;
            }

            pole1.localPosition = new Vector3(rect.x + 0.1F,              yOffset, 0.0F);
            pole2.localPosition = new Vector3(rect.x + rect.width - 0.1F, yOffset, 0.0F);
            pole1.localScale = pole2.localScale = new Vector3(1.0F, 1.0F, zScale);
        }

        protected void UpdateTextMesh(IProperties properties, MeshOrientation orientation, Rect rect)
        {
            TextMeshPro text = this.gameObject.transform.Find("Text").GetComponent<TextMeshPro>();

            text.text = properties.Get("sign.text", "");
            text.fontSize = properties.Get("sign.font", 1.5F).Clamp(0.5F, 10.0F);

            switch (orientation)
            {
                case MeshOrientation.Ceiling:
                    text.rectTransform.localPosition = new Vector3(0.0F, rect.y, -0.01F);
                    text.rectTransform.sizeDelta = new Vector2(rect.width - 0.2F, rect.height);
                    break;

                default:
                    text.rectTransform.sizeDelta = new Vector2(rect.width - 0.2F, rect.height);
                    break;
            }
        }

        protected void CreateCollider(MeshProperties properties, MeshOrientation orientation, Rect rect)
        {
            BoxCollider collider = this.gameObject.AddComponent<BoxCollider>();

            switch (orientation)
            {
                case MeshOrientation.Wall:
                    collider.size = new Vector3(rect.width, 0.1F, rect.height);
                    collider.center = new Vector3(0.0F, 0.05F, 0.0F);
                    break;

                case MeshOrientation.Ceiling:
                    collider.size = new Vector3(rect.width, rect.height, 0.1F);
                    collider.center = new Vector3(0.0F, rect.y - (rect.height * 0.5F), 0.0F);
                    break;

                default:
                    collider.size = new Vector3(rect.width, rect.height, 0.1F);
                    collider.center = new Vector3(0.0F, 1.0F + rect.height * 0.5F, 0.0F);
                    break;
            }
        }
    }
}
