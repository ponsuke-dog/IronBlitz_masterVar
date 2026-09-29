using UnityEngine;

// 常にスクリプト再生 (タイルに反映させるため)
[ExecuteAlways]
public class FloorTile: MonoBehaviour
{
    public float tileSize = 1.0f;
    public enum TileDirection
    {
        Floor,          // 床
        Wall_Front,     // 正面壁
        Wall_Side       // 横壁
    }

    public TileDirection direction = TileDirection.Floor;   // デフォルトは床

    private Renderer r;
    private MaterialPropertyBlock mpb;

    void Awake()
    {
        r = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (r == null) return;

        Vector3 s = transform.localScale;

        Vector2 tiling = Vector2.one;

        switch (direction)
        {
            case TileDirection.Floor:
                tiling = new Vector2(s.x, s.z);
                break;

            case TileDirection.Wall_Side:
                tiling = new Vector2(s.x, s.y);
                break;

            case TileDirection.Wall_Front:
                tiling = new Vector2(s.z, s.y);
                break;
        }

        tiling /= tileSize;

        r.GetPropertyBlock(mpb);
        mpb.SetVector("_BaseMap_ST", new Vector4(tiling.x, tiling.y, 0, 0));
        r.SetPropertyBlock(mpb);
    }
}