using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>Editable, non-interactive elliptical light behind the Signal Bay pack.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SignalBayGlow : MaskableGraphic
    {
        const int Segments = 48;
        const int Rings = 8;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 centre = rect.center;
            Vector2 radius = rect.size * .5f;
            for (int ring = 0; ring <= Rings; ring++)
            {
                float t = (float)ring / Rings;
                Color tint = color;
                tint.a *= (1f - t) * (1f - t);
                for (int segment = 0; segment < Segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / Segments;
                    var vertex = UIVertex.simpleVert;
                    vertex.position = centre + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y) * t;
                    vertex.color = tint;
                    mesh.AddVert(vertex);
                    if (ring == 0) continue;
                    int current = ring * Segments + segment;
                    int next = ring * Segments + (segment + 1) % Segments;
                    mesh.AddTriangle(current - Segments, current, next);
                    mesh.AddTriangle(current - Segments, next, next - Segments);
                }
            }
        }
    }
}
