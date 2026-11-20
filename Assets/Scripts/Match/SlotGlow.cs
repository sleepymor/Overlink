using System.Collections.Generic;
using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// Event-driven glow for the board. Nothing glows at rest: each call starts one short-lived overlay quad on a
    /// face, the presenter ticks the list from its LateUpdate, and the quad and its material are destroyed when the
    /// pulse ends. A Flash is a bright pulse that fades; a Scan is a band that crosses the face once. Both use the
    /// Overlink/SlotGlow shader, which is additive and black at rest.
    /// </summary>
    public sealed class SlotGlow
    {
        const string ShaderName = "Overlink/SlotGlow";
        const string ReduceFlashKey = "Overlink.Settings.ReduceFlash";
        const float FrontOffset = -0.045f;
        const float RiseFraction = 0.2f;

        sealed class Pulse
        {
            public GameObject Go;
            public Material Material;
            public float Start;
            public float Duration;
            public float Peak;
            public bool Scan;
        }

        readonly List<Pulse> pulses = new List<Pulse>();
        Shader shader;

        public void Flash(Transform face, Vector2 size, Color color, float duration, float peak)
        {
            Start(face, size, color, duration, peak, false);
        }

        public void Scan(Transform face, Vector2 size, Color color, float duration)
        {
            Start(face, size, color, duration, 1f, true);
        }

        public void Tick()
        {
            if (pulses.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                Pulse pulse = pulses[i];
                float t = (now - pulse.Start) / pulse.Duration;
                if (pulse.Go == null || t >= 1f)
                {
                    Release(pulse);
                    pulses.RemoveAt(i);
                    continue;
                }

                if (pulse.Scan)
                {
                    pulse.Material.SetFloat("_Scan", t);
                    pulse.Material.SetFloat("_Intensity", pulse.Peak);
                }
                else
                {
                    float envelope = t < RiseFraction ? t / RiseFraction : 1f - (t - RiseFraction) / (1f - RiseFraction);
                    pulse.Material.SetFloat("_Intensity", pulse.Peak * envelope);
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < pulses.Count; i++)
            {
                Release(pulses[i]);
            }
            pulses.Clear();
        }

        void Start(Transform face, Vector2 size, Color color, float duration, float peak, bool scan)
        {
            if (!Application.isPlaying || face == null)
            {
                return;
            }

            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }
            if (shader == null)
            {
                return;
            }

            float strength = PlayerPrefs.GetInt(ReduceFlashKey, 0) != 0 ? 0.35f : 1f;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Slot glow";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(face, false);
            go.transform.localPosition = new Vector3(0f, 0f, FrontOffset);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var material = new Material(shader);
            material.SetColor("_GlowColor", color);
            material.SetFloat("_Intensity", 0f);
            material.SetFloat("_Rim", scan ? 0f : 1f);
            material.SetFloat("_Scan", scan ? 0f : -1f);
            go.GetComponent<Renderer>().sharedMaterial = material;

            pulses.Add(new Pulse
            {
                Go = go,
                Material = material,
                Start = Time.unscaledTime,
                Duration = Mathf.Max(0.01f, duration),
                Peak = peak * strength,
                Scan = scan,
            });
        }

        static void Release(Pulse pulse)
        {
            if (pulse.Go != null)
            {
                Object.Destroy(pulse.Go);
            }
            if (pulse.Material != null)
            {
                Object.Destroy(pulse.Material);
            }
        }
    }
}
