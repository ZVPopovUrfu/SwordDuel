using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Input")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private bool pauseAllowed = true;

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhenPaused = true;

    private bool _isPaused;

    public bool IsPaused => _isPaused;

    private void Awake()
    {
        Time.timeScale = 1f;
        SetPausePanel(false);
    }

    private void Update()
    {
        if (!pauseAllowed)
            return;

        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (_isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        SetPausePanel(true);

        if (showCursorWhenPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Resume()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        SetPausePanel(false);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("[PauseMenuController] Main menu scene name is empty.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void SetPauseAllowed(bool value)
    {
        pauseAllowed = value;

        if (!pauseAllowed && _isPaused)
            Resume();
    }

    private void SetPausePanel(bool active)
    {
        if (pausePanel != null)
            pausePanel.SetActive(active);
    }
}
