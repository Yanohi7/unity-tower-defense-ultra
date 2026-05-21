using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Method to load the level select screen when the Adventure button is clicked
    public void OpenAdventure()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("LevelSelect");
    }

    // Method to handle clicks on the Friend Mode button, which is not available yet
    public void OpenFriendMode()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("PlayVSFriend");
    }

    // Method to load the Play VS Computer screen when the corresponding button is clicked
    public void OpenPlayVSPC()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("PlayVSComputer");
    }

    // Method to quit the game when the Quit button is clicked
    public void QuitGame()
    {
        AudioManager.Instance.PlayClick();
        Application.Quit();
    }
}