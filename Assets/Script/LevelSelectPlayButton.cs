using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectPlayButton : MonoBehaviour
{
    public string puzzleSceneName = "PuzzleScene";
    public int maxLevel = 25;

    public void OnClickPlayNext()
    {
        Debug.Log("[LevelSelectPlayButton] CLICK");

        int maxCleared = LevelProgress.GetMaxLevelCleared();
        int nextLevel = maxCleared + 1;
        Debug.Log($"[LevelSelectPlayButton] maxCleared={maxCleared}, nextLevel={nextLevel}");

        if (!Application.CanStreamedLevelBeLoaded(puzzleSceneName))
        {
            Debug.LogError($"[LevelSelectPlayButton] Scene '{puzzleSceneName}' cannot be loaded. Check Build Settings & scene name.");
        }

        if (nextLevel > maxLevel)
        {
            Debug.Log("[LevelSelectPlayButton] All levels cleared, do nothing");
            return;
        }

        CurrentLevel.Set(nextLevel);
        Debug.Log($"[LevelSelectPlayButton] Loading scene '{puzzleSceneName}' for level {nextLevel}");
        SceneManager.LoadScene(puzzleSceneName);
    }


}
