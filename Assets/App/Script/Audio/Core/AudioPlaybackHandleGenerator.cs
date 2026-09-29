/// <summary>
/// Audio再生ごとの一意なハンドルIDを発行するクラス。
/// 同じaudioIdを複数再生した場合でも、個別停止できるようにする。
/// </summary>
public static class AudioPlaybackHandleGenerator
{
    private static int currentHandle = 1;

    /// <summary>
    /// 新しい再生ハンドルを発行する。
    /// </summary>
    public static int Create()
    {
        int handle = currentHandle;
        currentHandle++;

        if (currentHandle == int.MaxValue)
        {
            currentHandle = 1;
        }

        return handle;
    }
}