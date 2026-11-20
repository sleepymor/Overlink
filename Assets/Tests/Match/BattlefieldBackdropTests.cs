using NUnit.Framework;
using UnityEngine;
using Overlink.Match;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// The sky hangs off the main camera at far depth. These tests pin the three things that make it show:
    /// the image is a child of the camera, the camera's culling mask includes its layer, and the image covers
    /// the frustum at its depth. They also pin that the camera's clear flags are left alone.
    /// </summary>
    public class BattlefieldBackdropTests
    {
        GameObject camGo;
        Camera cam;
        GameObject host;
        BattlefieldBackdrop backdrop;
        Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            camGo = new GameObject("BackdropTestCamera");
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.farClipPlane = 200f;
            cam.aspect = 16f / 9f;
            host = new GameObject("BackdropTestHost");
            backdrop = host.AddComponent<BattlefieldBackdrop>();
            sprite = Resources.Load<Sprite>(BattlefieldBackdrop.ImagePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (backdrop != null)
            {
                backdrop.Release();
            }
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(camGo);
        }

        [Test]
        public void Attach_HangsTheSkyOffTheCameraAtFarDepth()
        {
            Assert.IsNotNull(sprite, "precondition: the sky image must load from Resources.");
            Assert.IsTrue(backdrop.Attach(cam, sprite));

            Transform image = backdrop.Image.transform;
            Assert.AreSame(cam.transform, image.parent,
                "The sky must be a child of the main camera, or it will not stay screen-fixed.");
            Assert.AreEqual(cam.farClipPlane * BattlefieldBackdrop.FarFraction, image.localPosition.z, 1e-3f,
                "The sky must sit just inside the far plane, behind the board.");
            Assert.AreEqual(Vector3.zero, new Vector3(image.localPosition.x, image.localPosition.y, 0f),
                "The sky must be centred on the camera's view axis.");
        }

        [Test]
        public void Attach_IncludesTheBackdropLayerInTheCullingMask()
        {
            int layer = LayerMask.NameToLayer(BattlefieldBackdrop.LayerName);
            Assert.GreaterOrEqual(layer, 0, "precondition: the Backdrop layer must exist in TagManager.");

            cam.cullingMask = ~(1 << layer);
            backdrop.Attach(cam, sprite);

            Assert.AreNotEqual(0, cam.cullingMask & (1 << layer),
                "The main camera must draw the Backdrop layer, or the sky never reaches the screen.");
            Assert.AreEqual(layer, backdrop.Image.gameObject.layer);
        }

        [Test]
        public void Attach_LeavesTheCameraClearFlagsAlone()
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            backdrop.Attach(cam, sprite);

            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags,
                "The sky is drawn by the camera itself, so the clear must not be changed to suit it.");
        }

        [Test]
        public void Attach_ScalesTheSkyToCoverTheFrustumAtItsDepth()
        {
            backdrop.Attach(cam, sprite);

            float distance = cam.farClipPlane * BattlefieldBackdrop.FarFraction;
            float frustumHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * cam.aspect;
            Vector3 scale = backdrop.Image.transform.localScale;
            float imageHeight = scale.y * sprite.bounds.size.y;
            float imageWidth = scale.x * sprite.bounds.size.x;

            Assert.GreaterOrEqual(imageHeight, frustumHeight - 1e-2f,
                "The sky must cover the screen top to bottom, the way CSS background-size: cover does.");
            Assert.GreaterOrEqual(imageWidth, frustumWidth - 1e-2f,
                "The sky must cover the screen side to side at this aspect.");
        }

        [Test]
        public void Attach_WithNoCamera_ReturnsFalseAndTouchesNothing()
        {
            int before = cam.cullingMask;
            Assert.IsFalse(backdrop.Attach(null, sprite));
            Assert.IsNull(backdrop.Image);
            Assert.AreEqual(before, cam.cullingMask);
        }

        [Test]
        public void Destroy_RestoresTheCullingMaskAndRemovesTheSky()
        {
            int before = cam.cullingMask;
            backdrop.Attach(cam, sprite);
            GameObject image = backdrop.Image.gameObject;

            backdrop.Release();

            Assert.AreEqual(before, cam.cullingMask, "Tearing down must put the camera's mask back.");
            Assert.IsTrue(image == null, "The sky quad must be removed with the component.");
            Assert.IsNull(backdrop.Image, "Releasing must leave no dangling reference to the sky.");
        }
    }
}
