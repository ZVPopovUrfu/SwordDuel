using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private string enemyASceneName = "UserTest_EnemyA";
    [SerializeField] private string enemyBSceneName = "UserTest_EnemyB";

    [Header("Optional")]
    [SerializeField] private bool unlockCursorOnMenu = true;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (unlockCursorOnMenu)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void LoadTutorial()
    {
        LoadScene(tutorialSceneName);
    }

    public void LoadEnemyA()
    {
        LoadScene(enemyASceneName);
    }

    public void LoadEnemyB()
    {
        LoadScene(enemyBSceneName);
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[MainMenuController] Scene name is empty.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
