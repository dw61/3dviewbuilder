using UnityEngine;

[System.Serializable]
public class TargetPattern
{
    public int n = 3;

    public bool[,] front;
    public bool[,] right;

    public static TargetPattern DemoL()
    {
        return DemoL(3);
    }

    public static TargetPattern DemoL(int n)
    {
        var t = new TargetPattern();
        t.n = n;
        t.front = new bool[n, n];
        t.right = new bool[n, n];

        for (int y = 0; y < n; y++) t.front[0, y] = true;
        for (int x = 0; x < n; x++) t.front[x, 0] = true;

        for (int y = 0; y < n; y++) t.right[0, y] = true;

        return t;
    }
}
