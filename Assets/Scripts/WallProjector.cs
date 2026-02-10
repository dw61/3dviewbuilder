using UnityEngine;

// Draw target(gray)+diff(colors) into ONE texture per view.
// Walls: front/back renderers supported (back uses its own material + flipU).
// Ground: single renderer (no back).
public class WallProjector : MonoBehaviour
{
    [Header("Front wall (front/back)")]
    public Renderer frontWallRenderer;
    public Renderer frontWallBackRenderer;

    [Header("Right wall (front/back)")]
    public Renderer rightWallRenderer;
    public Renderer rightWallBackRenderer;

    [Header("Ground overlay (single)")]
    public Renderer groundRenderer; // your new GroundOverlay Quad

    [Header("Texture settings")]
    public int pixelsPerCell = 64;

    bool[,] targetFront;
    bool[,] targetRight;
    bool[,] targetTop;

    Texture2D frontTex, rightTex, topTex;

    Material frontMat, frontBackMat;
    Material rightMat, rightBackMat;
    Material groundMat;

    // Call once
    public void SetTargets(bool[,] front, bool[,] right, bool[,] top)
    {
        targetFront = front;
        targetRight = right;
        targetTop = top;
        Redraw(null, null, null);
    }

    // Call whenever current projection changes
    public void Redraw(bool[,] curFront, bool[,] curRight, bool[,] curTop)
    {
        if (targetFront == null || targetRight == null || targetTop == null) return;

        curFront ??= new bool[targetFront.GetLength(0), targetFront.GetLength(1)];
        curRight ??= new bool[targetRight.GetLength(0), targetRight.GetLength(1)];
        curTop   ??= new bool[targetTop.GetLength(0),   targetTop.GetLength(1)];

        SafeDestroy(frontTex);
        SafeDestroy(rightTex);
        SafeDestroy(topTex);

        frontTex = MakeCombinedTex(targetFront, curFront);
        rightTex = MakeCombinedTex(targetRight, curRight);
        topTex   = MakeCombinedTex(targetTop,   curTop);

        ApplyFrontBack(frontWallRenderer, frontWallBackRenderer, ref frontMat, ref frontBackMat, frontTex, flipUOnBack: true);
        ApplyFrontBack(rightWallRenderer, rightWallBackRenderer, ref rightMat,  ref rightBackMat,  rightTex, flipUOnBack: true);
        ApplySingle(groundRenderer, ref groundMat, topTex);
    }

    Texture2D MakeCombinedTex(bool[,] target, bool[,] cur)
    {
        int n = target.GetLength(0);
        int w = n * pixelsPerCell;
        int h = n * pixelsPerCell;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color bg = new Color(0.96f, 0.96f, 0.96f, 1f);
        Color targetFill = new Color(0.55f, 0.55f, 0.55f, 1f);
        Color gridLine = new Color(0f, 0f, 0f, 0.18f);

        Color correct = new Color(0.20f, 0.80f, 0.20f, 1f); // green
        Color extra   = new Color(0.90f, 0.20f, 0.20f, 1f); // red
        Color missing = new Color(0.20f, 0.45f, 0.95f, 1f); // blue

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int gx = x / pixelsPerCell;
            int gy = y / pixelsPerCell;
            bool border = (x % pixelsPerCell == 0) || (y % pixelsPerCell == 0);

            bool t = target[gx, gy];
            bool c = cur[gx, gy];

            Color col = t ? targetFill : bg;

            if (c && t) col = correct;
            else if (c && !t) col = extra;
            else if (!c && t) col = missing;

            if (border) col = Color.Lerp(col, gridLine, 0.8f);
            tex.SetPixel(x, y, col);
        }

        tex.Apply();
        return tex;
    }

    void ApplySingle(Renderer r, ref Material mat, Texture2D tex)
    {
        if (r == null) return;

        Shader shader =
            Shader.Find("Unlit/Texture") ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("URP/Unlit") ??
            Shader.Find("Unlit/Color");

        if (mat == null || mat.shader != shader) mat = new Material(shader);

        SetTexAndWhite(mat, tex);
        ResetUV(mat);

        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void ApplyFrontBack(Renderer front, Renderer back,
                        ref Material matFront, ref Material matBack,
                        Texture2D tex, bool flipUOnBack)
    {
        if (front == null) return;

        Shader shader =
            Shader.Find("Unlit/Texture") ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("URP/Unlit") ??
            Shader.Find("Unlit/Color");

        if (matFront == null || matFront.shader != shader) matFront = new Material(shader);
        if (matBack  == null || matBack.shader  != shader) matBack  = new Material(shader);

        // front
        SetTexAndWhite(matFront, tex);
        ResetUV(matFront);
        front.sharedMaterial = matFront;

        // back (mirror U so it matches front when seen from the other side)
        if (back != null)
        {
            SetTexAndWhite(matBack, tex);
            ResetUV(matBack);
            if (flipUOnBack) FlipU(matBack);
            back.sharedMaterial = matBack;

            back.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            back.receiveShadows = false;
        }

        front.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        front.receiveShadows = false;
    }

    void SetTexAndWhite(Material m, Texture2D tex)
    {
        m.mainTexture = tex;
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);

        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
    }

    void ResetUV(Material m)
    {
        if (m.HasProperty("_MainTex"))
        {
            m.SetTextureScale("_MainTex", Vector2.one);
            m.SetTextureOffset("_MainTex", Vector2.zero);
        }
        if (m.HasProperty("_BaseMap"))
        {
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
        }
    }

    void FlipU(Material m)
    {
        // mirror left-right: scaleX=-1, offsetX=1
        if (m.HasProperty("_MainTex"))
        {
            m.SetTextureScale("_MainTex", new Vector2(-1f, 1f));
            m.SetTextureOffset("_MainTex", new Vector2(1f, 0f));
        }
        if (m.HasProperty("_BaseMap"))
        {
            m.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
            m.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
        }
    }

    void SafeDestroy(Object o)
    {
        if (o != null) Destroy(o);
    }
}