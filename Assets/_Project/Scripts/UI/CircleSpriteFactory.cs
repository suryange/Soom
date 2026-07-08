using UnityEngine;

/// <summary>
/// Procedural circle sprites for the breath UI (ring outlines + filled dots) so nobody has to
/// hand-author and import texture assets for a shape this simple. Distance-field math per pixel
/// (smoothstep on distance-from-center vs radius) gives free anti-aliasing at any size.
/// </summary>
public static class CircleSpriteFactory
{
    const int Size = 128;

    /// <summary>A solid filled disc, soft-edged.</summary>
    public static Sprite CreateFilledCircle(Color color) => Create(color, 0f);

    /// <summary>A ring outline of the given thickness (0..1, fraction of the radius).</summary>
    public static Sprite CreateRing(Color color, float thickness) => Create(color, Mathf.Clamp01(thickness));

    static Sprite Create(Color color, float ringThickness)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float c = Size * 0.5f, maxR = Size * 0.5f - 1f;
        float aa = 1.5f / maxR; // antialias band width in normalized distance

        var px = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / maxR; // 0 center .. 1 edge

            float a;
            if (ringThickness <= 0f)
            {
                a = 1f - Mathf.SmoothStep(1f - aa, 1f, d); // filled disc
            }
            else
            {
                float inner = 1f - ringThickness;
                float outerEdge = 1f - Mathf.SmoothStep(1f - aa, 1f, d);
                float innerEdge = Mathf.SmoothStep(inner - aa, inner + aa, d);
                a = outerEdge * innerEdge; // ring = outside the inner hole, inside the outer edge
            }
            px[y * Size + x] = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(a));
        }
        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }
}
