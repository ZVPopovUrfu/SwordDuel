using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UserTestRoundManager : MonoBehaviour
{
    public enum EnemyAIType
    {
        FSM,
        ML
    }

    public enum RoundResult
    {
        None,
        PlayerWin,
        EnemyWin,
        Draw
    }

    [Header("Session Info")]
    [Tooltip("A или B. Именно это видит игрок.")]
    [SerializeField] private string _enemyLabel = "A";

    [Tooltip("Реальный тип ИИ. Игроку это не показываем, нужно только для логов.")]
    [SerializeField] private EnemyAIType _enemyAIType = EnemyAIType.FSM;

    [Header("Round Settings")]
    [SerializeField] private int _maxRounds = 5;
    [SerializeField] private float _roundTimeLimit = 30f;
    [SerializeField] private float _roundStartGraceTime = 0.45f;
    [SerializeField] private float _restartDelay = 1.2f;
    [SerializeField] private bool _useRoundTimeLimit = true;

    [Header("Swords")]
    [SerializeField] private Transform _playerSwordTransform;
    [SerializeField] private Transform _enemySwordTransform;
    [SerializeField] private Rigidbody2D _playerRb;
    [SerializeField] private Rigidbody2D _enemyRb;

    [Header("Spawn Points")]
    [SerializeField] private Transform _playerSwordSpawnPoint;
    [SerializeField] private Transform _enemySwordSpawnPoint;

    [Header("Characters")]
    [SerializeField] private Transform _playerCharacter;
    [SerializeField] private Transform _enemyCharacter;
    [SerializeField] private Transform _playerCharacterSpawnPoint;
    [SerializeField] private Transform _enemyCharacterSpawnPoint;

    [Header("Enemy Controllers")]
    [SerializeField] private FSMSword _fsmSword;
    [SerializeField] private MLSword _mlSword;

    [Header("UI")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private GameObject _sessionCompletePanel;
    [SerializeField] private TMP_Text _sessionCompleteText;

    [Header("Debug")]
    [SerializeField] private bool _logToConsole = true;

    private UserTestSessionData _sessionData;

    private int _currentRound;
    private int _playerWins;
    private int _enemyWins;
    private int _draws;

    private bool _roundActive;
    private bool _roundEnding;
    private bool _sessionComplete;

    private float _roundStartTime;
    private Coroutine _restartRoutine;

    public int CurrentRound => _currentRound;
    public bool IsRoundActive => _roundActive && !_roundEnding && !_sessionComplete;
    public string EnemyLabel => _enemyLabel;
    public string EnemyAITypeName => _enemyAIType.ToString();

    private void Awake()
    {
        _sessionData = UserTestSessionData.EnsureInstance();
        AutoFindMissingReferences();
    }

    private void Start()
    {
        if (_sessionCompletePanel != null)
            _sessionCompletePanel.SetActive(false);

        StartNewRound();
    }

    private void Update()
    {
        if (_sessionComplete)
            return;

        if (!_roundActive)
            return;

        if (_useRoundTimeLimit)
        {
            float elapsed = Time.time - _roundStartTime;

            if (elapsed >= _roundTimeLimit)
                EndRound(RoundResult.Draw);
        }
    }

    private void AutoFindMissingReferences()
    {
        if (_playerRb == null && _playerSwordTransform != null)
            _playerRb = _playerSwordTransform.GetComponent<Rigidbody2D>();

        if (_enemyRb == null && _enemySwordTransform != null)
            _enemyRb = _enemySwordTransform.GetComponent<Rigidbody2D>();

        if (_fsmSword == null && _enemySwordTransform != null)
            _fsmSword = _enemySwordTransform.GetComponent<FSMSword>();

        if (_mlSword == null && _enemySwordTransform != null)
            _mlSword = _enemySwordTransform.GetComponent<MLSword>();
    }

    public void ReportPlayerHit()
    {
        if (!CanEndRound())
            return;

        EndRound(RoundResult.PlayerWin);
    }

    public void ReportEnemyHit()
    {
        if (!CanEndRound())
            return;

        EndRound(RoundResult.EnemyWin);
    }

    private bool CanEndRound()
    {
        if (_sessionComplete)
            return false;

        if (!_roundActive)
            return false;

        if (_roundEnding)
            return false;

        if (Time.time - _roundStartTime < _roundStartGraceTime)
            return false;

        return true;
    }

    private void EndRound(RoundResult result)
    {
        if (_roundEnding)
            return;

        _roundEnding = true;
        _roundActive = false;

        float duration = Time.time - _roundStartTime;

        string winner = "None";
        string message;

        switch (result)
        {
            case RoundResult.PlayerWin:
                _playerWins++;
                winner = "Player";
                message = "Вы выиграли раунд!";
                break;

            case RoundResult.EnemyWin:
                _enemyWins++;
                winner = "Enemy";
                message = $"Противник {_enemyLabel} выиграл раунд!";
                break;

            case RoundResult.Draw:
                _draws++;
                winner = "Draw";
                message = "Ничья.";
                break;

            default:
                message = "";
                break;
        }

        StopBothSwords();
        AddRoundRow(result, winner, duration);

        // Сохраняем workbook после каждого раунда, чтобы данные не потерялись.
        if (_sessionData != null)
            _sessionData.SaveWorkbook();

        UpdateUI(message);

        if (_logToConsole)
        {
            Debug.Log(
                $"[UserTestRoundManager] Enemy {_enemyLabel}. Round {_currentRound} ended. " +
                $"Result: {result}. Duration: {duration:F2}s."
            );
        }

        if (_currentRound >= _maxRounds)
        {
            CompleteSession();
            return;
        }

        if (_restartRoutine != null)
            StopCoroutine(_restartRoutine);

        _restartRoutine = StartCoroutine(RestartRoundAfterDelay());
    }

    private void AddRoundRow(RoundResult result, string winner, float duration)
    {
        if (_sessionData == null)
            return;

        int scoreDiff = _playerWins - _enemyWins;

        string[] row =
        {
            _sessionData.SessionId,
            _sessionData.TestStartTimestamp,
            _enemyLabel,
            _enemyAIType.ToString(),
            _currentRound.ToString(),
            result.ToString(),
            winner,
            duration.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _playerWins.ToString(),
            _enemyWins.ToString(),
            _draws.ToString(),
            scoreDiff.ToString()
        };

        _sessionData.AddRoundRow(row);
    }

    private IEnumerator RestartRoundAfterDelay()
    {
        yield return new WaitForSeconds(_restartDelay);
        StartNewRound();
    }

    private void StartNewRound()
    {
        if (_sessionComplete)
            return;

        _currentRound++;

        _roundEnding = false;
        _roundActive = false;

        StopBothSwords();

        ResetTransform(_playerSwordTransform, _playerSwordSpawnPoint);
        ResetTransform(_enemySwordTransform, _enemySwordSpawnPoint);
        ResetTransform(_playerCharacter, _playerCharacterSpawnPoint);
        ResetTransform(_enemyCharacter, _enemyCharacterSpawnPoint);

        StopBothSwords();

        if (_fsmSword != null)
            _fsmSword.ResetState();

        if (_mlSword != null)
            _mlSword.ResetAgentState();

        ResetTransform(_playerSwordTransform, _playerSwordSpawnPoint);
        ResetTransform(_enemySwordTransform, _enemySwordSpawnPoint);
        ResetTransform(_playerCharacter, _playerCharacterSpawnPoint);
        ResetTransform(_enemyCharacter, _enemyCharacterSpawnPoint);

        StopBothSwords();

        _roundStartTime = Time.time;
        _roundActive = true;

        UpdateUI($"Раунд {_currentRound}/{_maxRounds}. Противник {_enemyLabel}.");

        if (_logToConsole)
            Debug.Log($"[UserTestRoundManager] Enemy {_enemyLabel}. Round {_currentRound}/{_maxRounds} started.");
    }

    private void CompleteSession()
    {
        _sessionComplete = true;
        _roundActive = false;
        _roundEnding = true;

        StopBothSwords();

        if (_sessionData != null)
            _sessionData.SaveWorkbook();

        string path = _sessionData != null ? _sessionData.WorkbookPath : "не найден";

        string text =
            $"Сессия против Противника {_enemyLabel} завершена.\n\n" +
            $"Ваши победы: {_playerWins}\n" +
            $"Победы противника: {_enemyWins}\n" +
            $"Ничьи: {_draws}\n\n" +
            $"Файл результатов сохранён:\n{path}\n\n" +
            $"Если вы ещё не играли со вторым противником, вернитесь в меню и выберите другую сцену.\n" +
            $"После прохождения обоих противников заполните итоговую анкету.";

        if (_sessionCompletePanel != null)
            _sessionCompletePanel.SetActive(true);

        if (_sessionCompleteText != null)
            _sessionCompleteText.text = text;

        if (_messageText != null)
            _messageText.text = text;

        if (_logToConsole)
            Debug.Log($"[UserTestRoundManager] Enemy {_enemyLabel}. Session complete.");
    }

    private void ResetTransform(Transform target, Transform spawnPoint)
    {
        if (target == null || spawnPoint == null)
            return;

        target.position = spawnPoint.position;
        target.rotation = spawnPoint.rotation;
    }

    private void StopBothSwords()
    {
        StopRigidbody(_playerRb);
        StopRigidbody(_enemyRb);
    }

    private void StopRigidbody(Rigidbody2D rb)
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void UpdateUI(string message)
    {
        if (_scoreText != null)
        {
            _scoreText.text =
                $"Раунд: {_currentRound}/{_maxRounds}   " +
                $"Игрок: {_playerWins}   |   Противник {_enemyLabel}: {_enemyWins}   |   Ничьи: {_draws}";
        }

        if (_messageText != null)
            _messageText.text = message;
    }

    public void OpenResultsFolder()
    {
        if (_sessionData != null)
            _sessionData.OpenResultsFolder();
    }

    public void ForceCompleteSession()
    {
        CompleteSession();
    }

    public void LoadMainMenu(string mainMenuSceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
