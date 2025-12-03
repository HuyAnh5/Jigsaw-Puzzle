using UnityEngine;

public static class CurrentLevel
{
    private const string CurrentLevelKey = "CurrentLevelIndex";

    public static void Set(int levelIndex)
    {
        PlayerPrefs.SetInt(CurrentLevelKey, levelIndex);
    }

    public static int Get()
    {
        // Mặc định level 1 nếu chưa lưu gì
        return PlayerPrefs.GetInt(CurrentLevelKey, 1);
    }
}
