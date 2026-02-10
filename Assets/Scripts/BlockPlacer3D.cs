using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlockPlacer3D : MonoBehaviour
{
    [Header("Board")]
    public int n = 6;
    public float cellSize = 1f;
    public Transform ground;
    public Transform boardRoot;

    [Header("Prefabs")]
    public GameObject blockPrefab;

    [Header("Raycast")]
    public LayerMask groundMask; // optional; if Nothing, we'll still work.

    [Header("UI")]
    public Text statusText;

    [Header("Projection")]
    public WallProjector wallProjector;

    [Header("Options")]
    public bool forceBlockScale = true;

    TargetPattern target;
    bool[,] targetTop; // derived to be consistent with target.front & target.right

    Dictionary<Vector3Int, GameObject> blocks = new();

    struct Op
    {
        public Vector3Int key;
        public bool placed;
        public Op(Vector3Int key, bool placed) { this.key = key; this.placed = placed; }
    }
    Stack<Op> ops = new();

    Camera cam;
    int hoverX = -1, hoverZ = -1;

    // UI pacing
    float solvedSince = -1f;

    void Start()
    {
        cam = Camera.main;

        target = TargetPattern.DemoL(n);

        // IMPORTANT: derive targetTop from front/right so it's consistent
        targetTop = DeriveTopFromFrontRight(target.front, target.right);

        if (wallProjector != null)
            wallProjector.SetTargets(target.front, target.right, targetTop);

        UpdateAllVisuals();
    }

    void Update()
    {
        if (cam == null || ground == null || blockPrefab == null || boardRoot == null) return;

        if (Input.GetKeyDown(KeyCode.Z)) UndoOne();

        bool orbiting = Input.GetKey(KeyCode.Space);
        UpdateHoverCell();

        if (!orbiting && Input.GetMouseButtonDown(0))
        {
            bool removing = (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
            if (removing) TryRemoveTop();
            else TryPlaceOne();
        }
    }

    void UpdateHoverCell()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        bool hitOk;
        RaycastHit hit;

        if (groundMask.value == 0)
            hitOk = Physics.Raycast(ray, out hit, 500f);
        else
            hitOk = Physics.Raycast(ray, out hit, 500f, groundMask);

        if (!hitOk)
        {
            hoverX = hoverZ = -1;
            return;
        }

        // If we hit a block (when no mask), pick closest hit on ground
        if (hit.transform != ground && groundMask.value == 0)
        {
            var hits = Physics.RaycastAll(ray, 500f);
            float best = float.PositiveInfinity;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.transform == ground && h.distance < best)
                {
                    best = h.distance;
                    hit = h;
                    found = true;
                }
            }
            if (!found)
            {
                hoverX = hoverZ = -1;
                return;
            }
        }

        Vector3 local = hit.point - ground.position;

        int x = Mathf.FloorToInt((local.x + (n * cellSize) / 2f) / cellSize);
        int z = Mathf.FloorToInt((local.z + (n * cellSize) / 2f) / cellSize);

        if (x < 0 || x >= n || z < 0 || z >= n)
        {
            hoverX = hoverZ = -1;
            return;
        }

        hoverX = x;
        hoverZ = z;
    }

    void TryPlaceOne()
    {
        if (hoverX < 0 || hoverZ < 0) return;

        int y = NextY(hoverX, hoverZ);
        if (y < 0) return;

        var key = new Vector3Int(hoverX, y, hoverZ);
        if (blocks.ContainsKey(key)) return;

        GameObject go = Instantiate(blockPrefab, CellToWorld(hoverX, y, hoverZ), Quaternion.identity, boardRoot);
        if (forceBlockScale) go.transform.localScale = Vector3.one * cellSize;

        blocks[key] = go;
        ops.Push(new Op(key, true));

        UpdateAllVisuals();
    }

    void TryRemoveTop()
    {
        if (hoverX < 0 || hoverZ < 0) return;

        int y = TopY(hoverX, hoverZ);
        if (y < 0) return;

        var key = new Vector3Int(hoverX, y, hoverZ);
        if (!blocks.TryGetValue(key, out var go)) return;

        Destroy(go);
        blocks.Remove(key);
        ops.Push(new Op(key, false));

        UpdateAllVisuals();
    }

    void UndoOne()
    {
        if (ops.Count == 0) return;

        var op = ops.Pop();
        var key = op.key;

        if (op.placed)
        {
            if (blocks.TryGetValue(key, out var go))
            {
                Destroy(go);
                blocks.Remove(key);
            }
        }
        else
        {
            if (!blocks.ContainsKey(key))
            {
                GameObject go = Instantiate(blockPrefab, CellToWorld(key.x, key.y, key.z), Quaternion.identity, boardRoot);
                if (forceBlockScale) go.transform.localScale = Vector3.one * cellSize;
                blocks[key] = go;
            }
        }

        UpdateAllVisuals();
    }

    int NextY(int x, int z)
    {
        for (int y = 0; y < n; y++)
            if (!blocks.ContainsKey(new Vector3Int(x, y, z))) return y;
        return -1;
    }

    int TopY(int x, int z)
    {
        for (int y = n - 1; y >= 0; y--)
            if (blocks.ContainsKey(new Vector3Int(x, y, z))) return y;
        return -1;
    }

    Vector3 CellToWorld(int x, int y, int z)
    {
        float originX = ground.position.x - (n * cellSize) / 2f + cellSize / 2f;
        float originZ = ground.position.z - (n * cellSize) / 2f + cellSize / 2f;
        float worldY = ground.position.y + (cellSize * 0.5f) + y * cellSize;

        return new Vector3(originX + x * cellSize, worldY, originZ + z * cellSize);
    }

    void ComputeProjections(out bool[,] front, out bool[,] right, out bool[,] top)
    {
        front = new bool[n, n];
        right = new bool[n, n];
        top   = new bool[n, n];

        foreach (var key in blocks.Keys)
        {
            front[key.x, key.y] = true; // x,y
            right[key.z, key.y] = true; // z,y
            top[key.x, key.z]   = true; // x,z
        }
    }

    // returns (extra, missing)
    (int extra, int missing) DiffCount(bool[,] targetGrid, bool[,] curGrid)
    {
        int N0 = targetGrid.GetLength(0);
        int N1 = targetGrid.GetLength(1);
        int extra = 0, missing = 0;

        for (int a = 0; a < N0; a++)
        for (int b = 0; b < N1; b++)
        {
            bool T = targetGrid[a, b];
            bool C = curGrid[a, b];

            if (C && !T) extra++;
            else if (!C && T) missing++;
        }
        return (extra, missing);
    }

    bool CheckSolved(bool[,] curFront, bool[,] curRight, bool[,] curTop)
    {
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            if (curFront[x, y] != target.front[x, y]) return false;

        for (int y = 0; y < n; y++)
        for (int z = 0; z < n; z++)
            if (curRight[z, y] != target.right[z, y]) return false;

        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            if (curTop[x, z] != targetTop[x, z]) return false;

        return true;
    }

    void UpdateAllVisuals()
    {
        ComputeProjections(out var curFront, out var curRight, out var curTop);

        if (wallProjector != null)
            wallProjector.Redraw(curFront, curRight, curTop);

        // diff stats
        var dF = DiffCount(target.front, curFront);
        var dR = DiffCount(target.right, curRight);
        var dT = DiffCount(targetTop, curTop);

        bool solved = (dF.extra == 0 && dF.missing == 0 &&
                       dR.extra == 0 && dR.missing == 0 &&
                       dT.extra == 0 && dT.missing == 0);

        if (solved)
        {
            if (solvedSince < 0f) solvedSince = Time.time;
        }
        else
        {
            solvedSince = -1f;
        }
        if (statusText != null)
        {
            statusText.supportRichText = true;

            if (solved)
            {
                statusText.text =
                    "<size=52><b><color=#2ECC71>SUCCESS!</color></b></size>\n" +
                    "<size=26>All views match.</size>";
            }
            else
            {
                statusText.text =
                    "<size=44><b>3D View Builder</b></size>\n" +
                    "<size=24>Click = Place   Alt+Click = Delete   Z = Undo   Right Click = Switch View</size>";
            }
        }
    }

    // Derive a targetTop guaranteed consistent with target.front & target.right:
    // For each layer y, fill all (x,z) pairs where front[x,y] and right[z,y] are true.
    bool[,] DeriveTopFromFrontRight(bool[,] front, bool[,] right)
    {
        int N = front.GetLength(0);
        var top = new bool[N, N];

        for (int y = 0; y < N; y++)
        {
            List<int> xs = new();
            List<int> zs = new();
            for (int x = 0; x < N; x++) if (front[x, y]) xs.Add(x);
            for (int z = 0; z < N; z++) if (right[z, y]) zs.Add(z);

            for (int i = 0; i < xs.Count; i++)
            for (int j = 0; j < zs.Count; j++)
                top[xs[i], zs[j]] = true;
        }

        return top;
    }
}