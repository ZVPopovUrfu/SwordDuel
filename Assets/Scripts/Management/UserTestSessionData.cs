using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class UserTestSessionData : MonoBehaviour
{
    public static UserTestSessionData Instance { get; private set; }

    [Header("Output")]
    [SerializeField] private string _outputFolderName = "UserTestResults";
    [SerializeField] private string _filePrefix = "user_test_session";
    [SerializeField] private bool _saveOnApplicationQuit = true;
    [SerializeField] private bool _logToConsole = true;

    public string SessionId { get; private set; }
    public string TestStartTimestamp { get; private set; }
    public string OutputFolderPath { get; private set; }
    public string WorkbookPath { get; private set; }

    private readonly List<string[]> _roundRows = new List<string[]>();
    private readonly List<string[]> _actionRows = new List<string[]>();

    private readonly string[] _roundHeaders =
    {
        "session_id",
        "test_start_timestamp",
        "enemy_label",
        "ai_type",
        "round",
        "result",
        "winner",
        "duration_seconds",
        "player_wins_total",
        "enemy_wins_total",
        "draws_total",
        "score_differential_player_minus_enemy"
    };

    private readonly string[] _actionHeaders =
    {
        "session_id",
        "test_start_timestamp",
        "enemy_label",
        "ai_type",
        "round",
        "time_seconds",
        "agent",
        "move_action",
        "rotate_action",
        "combined_action",
        "behavior_state",
        "opening_plan",
        "sword_x",
        "sword_y",
        "tip_x",
        "tip_y",
        "handle_x",
        "handle_y",
        "my_threat_distance",
        "opponent_threat_distance",
        "threat_advantage",
        "sword_distance"
    };

    public static UserTestSessionData EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        UserTestSessionData existing = FindFirstObjectByType<UserTestSessionData>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("UserTestSessionData");
        return go.AddComponent<UserTestSessionData>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (string.IsNullOrEmpty(SessionId))
            RegenerateSession();
    }

    public void RegenerateSession()
    {
        DateTime now = DateTime.Now;

        SessionId = now.ToString("yyyyMMdd_HHmmss");
        TestStartTimestamp = now.ToString("yyyy-MM-dd HH:mm:ss");

        _roundRows.Clear();
        _actionRows.Clear();

        OutputFolderPath = GetEasyAccessResultsFolder();
        Directory.CreateDirectory(OutputFolderPath);

        WorkbookPath = Path.Combine(OutputFolderPath, $"{_filePrefix}_{SessionId}.xlsx");

        SaveWorkbook();

        if (_logToConsole)
        {
            Debug.Log($"[UserTestSessionData] New session created: {SessionId}");
            Debug.Log($"[UserTestSessionData] Workbook path: {WorkbookPath}");
        }
    }

    public void AddRoundRow(string[] row)
    {
        if (row == null)
            return;

        _roundRows.Add(row);
    }

    public void AddActionRow(string[] row)
    {
        if (row == null)
            return;

        _actionRows.Add(row);
    }

    public void SaveWorkbook()
    {
        if (string.IsNullOrEmpty(WorkbookPath))
            return;

        try
        {
            UserTestExcelWriter.WriteWorkbook(
                WorkbookPath,
                _roundHeaders,
                _roundRows,
                _actionHeaders,
                _actionRows
            );

            if (_logToConsole)
            {
                Debug.Log($"[UserTestSessionData] Workbook saved: {WorkbookPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserTestSessionData] Failed to save workbook: {ex.Message}");
        }
    }

    public void OpenResultsFolder()
    {
        if (string.IsNullOrEmpty(OutputFolderPath))
            OutputFolderPath = GetEasyAccessResultsFolder();

        Directory.CreateDirectory(OutputFolderPath);
        Application.OpenURL(OutputFolderPath);
    }

    private void OnApplicationQuit()
    {
        if (_saveOnApplicationQuit)
            SaveWorkbook();
    }

    private string GetEasyAccessResultsFolder()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        if (!string.IsNullOrWhiteSpace(desktopPath) && Directory.Exists(desktopPath))
            return Path.Combine(desktopPath, _outputFolderName);

        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (!string.IsNullOrWhiteSpace(documentsPath) && Directory.Exists(documentsPath))
            return Path.Combine(documentsPath, _outputFolderName);

        return Path.Combine(Application.persistentDataPath, _outputFolderName);
    }
}
