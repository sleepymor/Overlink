using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// The sky and city behind the tabletop, as one flat image. The image is a child of the main camera, pushed to
    /// the far plane, so it stays screen-fixed while the board pans and zooms in front of it. It lives on its own
    /// culling layer, which the main camera includes, so the board is drawn over it by ordinary depth testing and
    /// nothing depends on camera stacking or clear flags.
    /// </summary>
    public sealed class BattlefieldBackdrop : MonoBehaviour
    {
        public const string LayerName = "Backdrop";
        public const string ImagePath = "Backdrop/Battlefield_Sky";

        // The Figma angled frame lays a 28% overlay of near-black (4,6,11) over this image. A 0.72 multiply is
        // the same darkening to within about 0.01 per channel, which is below what the eye can separate.
        static readonly Color Dim = new Color(0.72f, 0.72f, 0.72f, 1f);

        // Just inside the far plane, so the image sits behind everything the board draws.
        public const float FarFraction = 0.98f;

        Camera cam;
        int camMask;
        SpriteRenderer image;
        Material unlit;
        int screenWidth = -1;
        int screenHeight = -1;

        public SpriteRenderer Image => image;

        void Start()
        {
            Attach(Camera.main, Resources.Load<Sprite>(ImagePath));
        }

        /// <summary>Hangs the sky off the given camera. Returns false, leaving the camera untouched, if it cannot.</summary>
        public bool Attach(Camera target, Sprite sprite)
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0 || sprite == null || target == null)
            {
                Debug.LogWarning("[Backdrop] Missing layer '" + LayerName + "', image '" + ImagePath +
                                 "', or main camera; the board keeps its solid background.", this);
                enabled = false;
                return false;
            }

            cam = target;
            camMask = cam.cullingMask;
            cam.cullingMask |= 1 << layer;

            var imageGo = new GameObject("Battlefield Backdrop");
            imageGo.layer = layer;
            imageGo.transform.SetParent(cam.transform, false);
            imageGo.transform.localRotation = Quaternion.identity;
            imageGo.transform.localPosition = new Vector3(0f, 0f, cam.farClipPlane * FarFraction);
            image = imageGo.AddComponent<SpriteRenderer>();
            image.sprite = sprite;
            image.color = Dim;

            // The default sprite material is a 2D-renderer lit shader, which a 3D URP camera does not draw.
            // Sprites/Default is the unlit shader the rest of the project already uses for 3D-camera sprites.
            Shader unlitShader = Shader.Find("Sprites/Default");
            if (unlitShader != null)
            {
                unlit = new Material(unlitShader);
                image.sharedMaterial = unlit;
            }
            Fit(sprite);
            return true;
        }

        void LateUpdate()
        {
            if (image == null || cam == null)
            {
                return;
            }

            if (Screen.width != screenWidth || Screen.height != screenHeight)
            {
                Fit(image.sprite);
            }
        }

        /// <summary>Scales the image to cover the screen at its depth, as CSS background-size: cover does.</summary>
        void Fit(Sprite sprite)
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            float distance = cam.farClipPlane * FarFraction;
            float worldHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * cam.aspect;
            Vector2 native = sprite.bounds.size;
            float scale = Mathf.Max(worldWidth / native.x, worldHeight / native.y);
            image.transform.localScale = new Vector3(scale, scale, 1f);
        }

        void OnDestroy()
        {
            Release();
        }

        /// <summary>Puts the camera's mask back and removes the sky. Safe to call more than once.</summary>
        public void Release()
        {
            if (cam != null)
            {
                cam.cullingMask = camMask;
                cam = null;
            }
            if (image != null)
            {
                GameObject quad = image.gameObject;
                image = null;
                quad.transform.SetParent(null, false);
                Discard(quad);
            }
            if (unlit != null)
            {
                Discard(unlit);
                unlit = null;
            }
        }

        static void Discard(Object o)
        {
            if (Application.isPlaying)
            {
                Destroy(o);
            }
            else
            {
                DestroyImmediate(o);
            }
        }
    }
}
