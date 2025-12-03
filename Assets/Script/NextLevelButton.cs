using UnityEngine;
using UnityEngine.SceneManagement;

public class NextLevelButton : MonoBehaviour
{
    public string levelSelectSceneName = "LevelSelect"; // tên scene menu 25 ô

    public void OnClickNext()
    {
        SceneManager.LoadScene(levelSelectSceneName);
    }
}
