using UnityEngine;
using UnityEngine.SceneManagement;

public class PuzzelSceneUIManager : MonoBehaviour
{
    public string levelSelectSceneName = "LevelSelect";
    public GameObject settingPanel;
    public GameObject blockBackground;
    public static bool IsSettingsOpen { get; private set; }


    public void OnClickNext()
    {
        SceneManager.LoadScene(levelSelectSceneName);
    }

    public void OnClickRestart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        IsSettingsOpen = false;
    }

    public void OpenSetting()
    {
        settingPanel.SetActive(true);
        blockBackground.SetActive(true);
        IsSettingsOpen = true;
    }

    public void CloseSetting()
    {
        settingPanel.SetActive(false);
        IsSettingsOpen = false;
    }

    public void OnClickExitToMainMenu()
    {
        SceneManager.LoadScene(levelSelectSceneName);
    }

}
