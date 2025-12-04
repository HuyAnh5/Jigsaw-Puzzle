using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveName : MonoBehaviour
{
    [SerializeField] public TMP_InputField input;

    public void Save()
    {
        Debug.Log("Save:" + input.text);
        PlayerPrefs.SetString("input", input.text);
    }

    public void Load()
    {
        Debug.Log("Load:" + input.text);
        input.text = PlayerPrefs.GetString("input");
    }

    public void Delete()
    {
        Debug.Log("delete: " + input.text);
        PlayerPrefs.DeleteKey("input");
    }

}
