using UnityEngine;

[System.Serializable]
public class FadePreset
{
    //[Header("プリセット名")]
    public string presetname = "Default";

    //[Header("フェード方式")]
    public FadeType fadetype = FadeType.Black;

    //[Header("フェード時間")]//秒
    public float duration = 1.0f;

    //[Header("フェードの色")]
    public Color fadecolor = Color.black;

    //[Header("タイル設定: 列数")]
    public int columns = 12;

    //[Header("タイル設定: 行数")]
    public int rows = 8;

    //[Header("タイル境界の太さ")]
    [Range(0f, 0.2f)]
    public float edgewidth = 0.02f;

    //[Header("タイル境界色")]
    public Color edgecolor = Color.black;

    //[Header("ランダム性")]
    [Range(0f, 1f)]
    public float randomness = 0.15f;

    //[Header("ランダムシード")]
    public int randomSeed = 12345;

    //[Header("タイル出現方式")]
    public TileOrderMode tileOrderMode = TileOrderMode.LeftToRight;

    //[Header("開始コーナー")]
    public TileStartCorner tileStartCorner = TileStartCorner.LeftTop;

    //[Header("ランダム時: 一度に進むタイル単位")]
    [Min(1)]
    public int randomBatchSize = 1;

}
