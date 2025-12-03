using UnityEngine;

public static class LevelProgress
{
    private const string MaxLevelKey = "MaxLevelCleared";

    public static int GetMaxLevelCleared()
    {
        return PlayerPrefs.GetInt(MaxLevelKey, 0);
    }

    public static void SaveLevelCompleted(int levelIndex)
    {
        int currentMax = GetMaxLevelCleared();
        if (levelIndex > currentMax)
        {
            PlayerPrefs.SetInt(MaxLevelKey, levelIndex);
            PlayerPrefs.Save();
        }
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(MaxLevelKey);
    }
}
