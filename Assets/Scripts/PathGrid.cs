using System.Collections.Generic;
using UnityEngine;

/// <summary>Occupancy grid built from the scene colliders plus A* with line-of-sight smoothing.</summary>
public static class PathGrid
{
    static bool[] blocked;
    static int nx, nz;
    static float cell, ox, oz;

    public static void Build(float minX, float minZ, float maxX, float maxZ, float cellSize, float radius)
    {
        Physics.SyncTransforms();
        cell = cellSize; ox = minX; oz = minZ;
        nx = Mathf.CeilToInt((maxX - minX) / cell);
        nz = Mathf.CeilToInt((maxZ - minZ) / cell);
        blocked = new bool[nx * nz];
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                Vector3 c = new Vector3(ox + (ix + 0.5f) * cell, 0, oz + (iz + 0.5f) * cell);
                blocked[iz * nx + ix] = Physics.CheckCapsule(c + Vector3.up * 0.15f, c + Vector3.up * 1.3f, radius);
            }
    }

    static bool InB(int ix, int iz) { return ix >= 0 && iz >= 0 && ix < nx && iz < nz; }
    static int Idx(int ix, int iz) { return iz * nx + ix; }

    static bool Free(int ix, int iz) { return InB(ix, iz) && !blocked[Idx(ix, iz)]; }

    static bool FreeAt(Vector3 p)
    {
        int ix = Mathf.FloorToInt((p.x - ox) / cell), iz = Mathf.FloorToInt((p.z - oz) / cell);
        return Free(ix, iz);
    }

    public static bool IsFree(Vector3 p) { return FreeAt(p); }

    static Vector3 Center(int i)
    {
        int ix = i % nx, iz = i / nx;
        return new Vector3(ox + (ix + 0.5f) * cell, 0, oz + (iz + 0.5f) * cell);
    }

    static int Nearest(Vector3 p)
    {
        int ix = Mathf.Clamp(Mathf.FloorToInt((p.x - ox) / cell), 0, nx - 1);
        int iz = Mathf.Clamp(Mathf.FloorToInt((p.z - oz) / cell), 0, nz - 1);
        if (Free(ix, iz)) return Idx(ix, iz);
        for (int r = 1; r < 16; r++)
        {
            float best = float.MaxValue; int bi = -1;
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    if (!Free(ix + dx, iz + dz)) continue;
                    float d = dx * dx + dz * dz;
                    if (d < best) { best = d; bi = Idx(ix + dx, iz + dz); }
                }
            if (bi >= 0) return bi;
        }
        return -1;
    }

    static bool LineFree(Vector3 a, Vector3 b)
    {
        float d = Vector3.Distance(a, b);
        int n = Mathf.Max(2, Mathf.CeilToInt(d / (cell * 0.5f)));
        for (int i = 0; i <= n; i++)
            if (!FreeAt(Vector3.Lerp(a, b, i / (float)n))) return false;
        return true;
    }

    class Heap
    {
        List<KeyValuePair<float, int>> h = new List<KeyValuePair<float, int>>();
        public int Count { get { return h.Count; } }
        public void Push(float k, int v)
        {
            h.Add(new KeyValuePair<float, int>(k, v));
            int i = h.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (h[p].Key <= h[i].Key) break;
                var t = h[p]; h[p] = h[i]; h[i] = t; i = p;
            }
        }
        public int Pop()
        {
            int r = h[0].Value;
            var last = h[h.Count - 1]; h.RemoveAt(h.Count - 1);
            if (h.Count > 0)
            {
                h[0] = last;
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, rr = l + 1, m = i;
                    if (l < h.Count && h[l].Key < h[m].Key) m = l;
                    if (rr < h.Count && h[rr].Key < h[m].Key) m = rr;
                    if (m == i) break;
                    var t = h[m]; h[m] = h[i]; h[i] = t; i = m;
                }
            }
            return r;
        }
    }

    public static List<Vector3> Find(Vector3 from, Vector3 to)
    {
        var result = new List<Vector3>();
        from.y = 0; to.y = 0;
        int s = Nearest(from), g = Nearest(to);
        if (s < 0 || g < 0) { result.Add(to); return result; }
        int N = nx * nz;
        var gs = new float[N]; var came = new int[N]; var closed = new bool[N];
        for (int i = 0; i < N; i++) { gs[i] = float.MaxValue; came[i] = -1; }
        var open = new Heap();
        gs[s] = 0; open.Push(0, s);
        Vector3 gc = Center(g);
        while (open.Count > 0)
        {
            int cur = open.Pop();
            if (closed[cur]) continue;
            closed[cur] = true;
            if (cur == g) break;
            int cx = cur % nx, cz = cur / nx;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int x = cx + dx, z = cz + dz;
                    if (!Free(x, z)) continue;
                    if (dx != 0 && dz != 0 && (!Free(cx + dx, cz) || !Free(cx, cz + dz))) continue;
                    int ni = Idx(x, z);
                    float step = (dx != 0 && dz != 0) ? 1.414f : 1f;
                    float ng = gs[cur] + step;
                    if (ng < gs[ni])
                    {
                        gs[ni] = ng; came[ni] = cur;
                        float hh = Vector3.Distance(Center(ni), gc) / cell;
                        open.Push(ng + hh, ni);
                    }
                }
        }
        if (came[g] < 0 && g != s) { result.Add(to); return result; }
        var cells = new List<Vector3>();
        for (int c = g; c != -1; c = came[c]) cells.Add(Center(c));
        cells.Reverse();
        cells[0] = from;
        cells.Add(to);
        // string pulling
        int i0 = 0;
        result.Add(cells[0]);
        while (i0 < cells.Count - 1)
        {
            int j = cells.Count - 1;
            while (j > i0 + 1 && !LineFree(cells[i0], cells[j])) j--;
            result.Add(cells[j]);
            i0 = j;
        }
        result.RemoveAt(0);
        return result;
    }
}
