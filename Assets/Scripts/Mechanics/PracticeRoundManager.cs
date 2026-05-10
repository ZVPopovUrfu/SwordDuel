using System.Collections;
using UnityEngine;
using TMPro;

public class PracticeRoundManager : MonoBehaviour
{
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

    [Header("AI")]
    [SerializeField] private MLSword _mlSword;
    [SerializeField] private FSMSword _fsmSword;

    [Header("UI")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _messageText;

    [Header("Round Settings")]
    [SerializeField] private float _restartDelay = 1.2f;
    [SerializeField] private float _roundStartGraceTime = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool _logToConsole = true;

    private int _playerScore;
    private int _enemyScore;

    private bool _roundActive;
    private bool _roundEnding;
    private float _roundStartTime;

    private Coroutine _restartRoutine;

    private void Awake()
    {
        AutoFindMissingReferences();
    }

    private void Start()
    {
        StartNewRound();
    }

    private void AutoFindMissingReferences()
    {
        if (_mlSword == null && _enemySwordTransform != null)
            _mlSword = _enemySwordTransform.GetComponent<MLSword>();

        if (_fsmSword == null && _enemySwordTransform != null)
            _fsmSword = _enemySwordTransform.GetComponent<FSMSword>();

        if (_playerRb == null && _playerSwordTransform != null)
            _playerRb = _playerSwordTransform.GetComponent<Rigidbody2D>();

        if (_enemyRb == null && _enemySwordTransform != null)
            _enemyRb = _enemySwordTransform.GetComponent<Rigidbody2D>();
    }

    public void ReportPlayerHit()
    {
        if (!CanEndRound())
            return;

        _playerScore++;
        EndRound("Вы выиграли раунд!");
    }

    public void ReportEnemyHit()
    {
        if (!CanEndRound())
            return;

        _enemyScore++;
        EndRound("Враг выиграл раунд!");
    }

    private bool CanEndRound()
    {
        if (!_roundActive)
            return false;

        if (_roundEnding)
            return false;

        if (Time.time - _roundStartTime < _roundStartGraceTime)
            return false;

        return true;
    }

    private void EndRound(string message)
    {
        _roundEnding = true;
        _roundActive = false;

        StopBothSwords();
        UpdateUI(message);

        if (_logToConsole)
        {
            Debug.Log($"[PracticeRoundManager] {message} Score: Player {_playerScore} - Enemy {_enemyScore}");
        }

        if (_restartRoutine != null)
            StopCoroutine(_restartRoutine);

        _restartRoutine = StartCoroutine(RestartRoundAfterDelay());
    }

    private IEnumerator RestartRoundAfterDelay()
    {
        yield return new WaitForSeconds(_restartDelay);

        StartNewRound();
    }

    private void StartNewRound()
    {
        _roundEnding = false;
        _roundActive = false;

        StopBothSwords();

        ResetTransform(_playerSwordTransform, _playerSwordSpawnPoint);
        ResetTransform(_enemySwordTransform, _enemySwordSpawnPoint);

        ResetTransform(_playerCharacter, _playerCharacterSpawnPoint);
        ResetTransform(_enemyCharacter, _enemyCharacterSpawnPoint);

        StopBothSwords();

        if (_mlSword != null)
            _mlSword.ResetAgentState();

        if (_fsmSword != null)
            _fsmSword.ResetState();

        ResetTransform(_playerSwordTransform, _playerSwordSpawnPoint);
        ResetTransform(_enemySwordTransform, _enemySwordSpawnPoint);

        ResetTransform(_playerCharacter, _playerCharacterSpawnPoint);
        ResetTransform(_enemyCharacter, _enemyCharacterSpawnPoint);

        StopBothSwords();

        _roundStartTime = Time.time;
        _roundActive = true;

        UpdateUI("Тренировка: атакуйте персонажа врага и защищайте своего.");

        if (_logToConsole)
            Debug.Log("[PracticeRoundManager] New practice round started.");
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
            _scoreText.text = $"Игрок: {_playerScore}   |   Враг: {_enemyScore}";

        if (_messageText != null)
            _messageText.text = message;
    }

    public void ResetScore()
    {
        _playerScore = 0;
        _enemyScore = 0;
        UpdateUI("Счёт сброшен.");
    }

    public void ForceRestartRound()
    {
        if (_restartRoutine != null)
            StopCoroutine(_restartRoutine);

        StartNewRound();
    }
}