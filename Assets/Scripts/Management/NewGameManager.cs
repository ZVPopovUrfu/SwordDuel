using UnityEngine;
using System.Collections;

public class NewGameManager : MonoBehaviour
{ 
[Header("References")]
[SerializeField] private Transform _playerSword;
[SerializeField] private Transform _enemySword;
[SerializeField] private Transform _playerSpawnPoint;
[SerializeField] private Transform _enemySpawnPoint;
[SerializeField] private Transform _playerCharacter;
[SerializeField] private Transform _enemyCharacter;

[Header("Round Settings")]
[SerializeField] private float _resetDelay = 0.5f;
[SerializeField] private int _playerScore = 0;
[SerializeField] private int _enemyScore = 0;
[SerializeField] private int _winScore = 3;

[Header("Visual Feedback")]
[SerializeField] private GameObject _roundEndPanel;
[SerializeField] private TMPro.TextMeshProUGUI _scoreText;
[SerializeField] private TMPro.TextMeshProUGUI _roundResultText;

private bool _isRoundActive = true;
private bool _isResetting = false;

void Start()
{
        if (_playerSword != null)
        {
            var playerAgent = _playerSword.GetComponent<MLSword>();
            if (playerAgent != null)
                playerAgent.OnHit += OnPlayerHit;
            else
            {
                var playerFSM = _playerSword.GetComponent<FSMSword>();
                if (playerFSM != null)
                    playerFSM.OnHit += OnPlayerHit;
            }
        }

        if (_enemySword != null)
        {
            var enemyAgent = _enemySword.GetComponent<MLSword>();
            if (enemyAgent != null)
                enemyAgent.OnHit += OnEnemyHit;
            else
            {
                var enemyFSM = _enemySword.GetComponent<FSMSword>();
                if (enemyFSM != null)
                    enemyFSM.OnHit += OnEnemyHit;
            }
        }

        UpdateScoreUI();
}

public void OnPlayerHit()
{
    if (!_isRoundActive || _isResetting) return;

    _playerScore++;
    UpdateScoreUI();

    Debug.Log($"🎯 Игрок атаковал! Счет: {_playerScore} : {_enemyScore}");

    if (_playerScore >= _winScore)
    {
        EndGame("ПОБЕДА ИГРОКА!");
    }
    else
    {
        StartCoroutine(ResetRound("Игрок атаковал!"));
    }
}

public void OnEnemyHit()
{
    if (!_isRoundActive || _isResetting) return;

    _enemyScore++;
    UpdateScoreUI();

    Debug.Log($"🤖 Враг атаковал! Счет: {_playerScore} : {_enemyScore}");

    if (_enemyScore >= _winScore)
    {
        EndGame("ПОБЕДА ВРАГА!");
    }
    else
    {
        StartCoroutine(ResetRound("Враг атаковал!"));
    }
}

private IEnumerator ResetRound(string message)
{
    _isRoundActive = false;
    _isResetting = true;

    // Показываем сообщение
    if (_roundEndPanel != null && _roundResultText != null)
    {
        _roundResultText.text = message;
        _roundEndPanel.SetActive(true);
    }

    yield return new WaitForSeconds(_resetDelay);

    // Скрываем панель
    if (_roundEndPanel != null)
    {
        _roundEndPanel.SetActive(false);
    }

    // Сбрасываем позиции мечей и персонажей
    ResetPositions();

    // Сбрасываем скорости
    ResetVelocity(_playerSword);
    ResetVelocity(_enemySword);

    // Сбрасываем физику мечей
    ResetSwordPhysics(_playerSword);
    ResetSwordPhysics(_enemySword);

    // Сбрасываем состояние агентов (если они еще есть)
    ResetAgent(_playerSword);
    ResetAgent(_enemySword);

    _isResetting = false;
    _isRoundActive = true;

    Debug.Log("🔄 Раунд начался заново!");
}

private void ResetPositions()
{
    // Сброс мечей
    if (_playerSword != null && _playerSpawnPoint != null)
        _playerSword.position = _playerSpawnPoint.position;

    if (_enemySword != null && _enemySpawnPoint != null)
        _enemySword.position = _enemySpawnPoint.position;

    // Сброс вращения
    if (_playerSword != null) _playerSword.rotation = Quaternion.identity;
    if (_enemySword != null) _enemySword.rotation = Quaternion.identity;

    // Сброс персонажей (опционально, если они двигаются)
    // if (_playerCharacter != null) _playerCharacter.position = initialPlayerPos;
    // if (_enemyCharacter != null) _enemyCharacter.position = initialEnemyPos;
}

private void ResetVelocity(Transform sword)
{
    if (sword == null) return;

    var rb = sword.GetComponent<Rigidbody2D>();
    if (rb != null)
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
}

private void ResetSwordPhysics(Transform sword)
{
    if (sword == null) return;

    var physics = sword.GetComponent<SwordPhysics>();
    if (physics != null && physics.IsKnockedBack())
    {
        // Принудительно останавливаем отбрасывание
        var rb = sword.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}

    private void ResetAgent(Transform sword)
    {
        if (sword == null) return;

        var ml = sword.GetComponent<MLSword>();
        if (ml != null)
        {
            ml.ResetAgentState();
            return;
        }

        var fsm = sword.GetComponent<FSMSword>();
        if (fsm != null)
        {
            fsm.ResetState();
        }
    }

    private void EndGame(string message)
{
    _isRoundActive = false;
    Debug.Log($"🏆 ИГРА ОКОНЧЕНА! {message}");

    if (_roundEndPanel != null && _roundResultText != null)
    {
        _roundResultText.text = message;
        _roundEndPanel.SetActive(true);
    }

    // Здесь можно добавить кнопку "Новая игра"
}

private void UpdateScoreUI()
{
    if (_scoreText != null)
    {
        _scoreText.text = $"{_playerScore} : {_enemyScore}";
    }
}

public void ResetGame()
{
    _playerScore = 0;
    _enemyScore = 0;
    UpdateScoreUI();
    StartCoroutine(ResetRound("Новая игра!"));
}
}
