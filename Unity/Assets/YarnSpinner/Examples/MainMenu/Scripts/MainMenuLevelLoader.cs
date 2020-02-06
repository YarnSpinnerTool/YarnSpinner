using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLevelLoader : MonoBehaviour {
    public void LoadSceneNarrative() {
        SceneManager.LoadScene("Space");
    }

    public void LoadSceneVoiceOver() {
        SceneManager.LoadScene("Voice Over");
    }
    public void LoadSceneMenu() {
        SceneManager.LoadScene("MainMenu");
    }

}
