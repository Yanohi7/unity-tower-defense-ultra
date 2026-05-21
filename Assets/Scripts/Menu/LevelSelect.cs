using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelect : MonoBehaviour
{
    // Methods to load Levels when the corresponding buttons are clicked
    public void OpenLevel1()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("Level1");
    }
    public void OpenLevel2()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("Level2");
    }
    public void OpenLevel3()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("Level3");
    }
    public void OpenLevel4()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("Level4");
    }
    public void OpenLevel5()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("Level5");
    }

    // Method to handle locked level button clicks
    public void LockedLevel()
    {
        AudioManager.Instance.PlayClick();
        Debug.Log("This level is locked or not ready yet.");
    }

    // Method to return to the main menu
    public void BackToMenu()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("MainMenu");
    }
}