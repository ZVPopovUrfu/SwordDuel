using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;

public class MLSword : Agent
{
    [Header("Arena Reference")]
    [SerializeField] private Transform _arenaCenter;  // Центр арены для локальных координат

    [Header("References")]
    [SerializeField] private Transform _enemyCharacter;
    [SerializeField] private Transform _myCharacter;
    [SerializeField] private Transform _enemySword;
    [SerializeField] private SwordPhysics _swordPhysics;
    [SerializeField] private Rigidbody2D _rb;

    [Header("Spawn Settings")]
    [SerializeField] private Transform _mySpawnPoint;
    [SerializeField] private Transform _enemySpawnPoint;
    [SerializeField] private float _spawnRandomOffset = 0.5f;  // Случайное смещение при спавне

    [Header("Movement Settings")]
    [SerializeField] private float _maxMoveForce = 5f;
    [SerializeField] private float _maxTorqueForce = 120f;     // Уменьшено для плавности
    [SerializeField] private float _angularDrag = 3f;          // Для инерции вращения

    [Header("Arena Settings")]
    [SerializeField] private float _arenaWidth = 20f;
    [SerializeField] private float _arenaHeight = 10f;

    [Header("Reward Settings")]
    [SerializeField] private float _hitReward = 2.0f;           // Главная награда
    [SerializeField] private float _gotHitPenalty = -2.0f;      // Главный штраф
    [SerializeField] private float _goodBlockReward = 0.15f;    // Малая награда за защиту
    [SerializeField] private float _badAttackPenalty = -0.1f;   // Малый штраф за плохую атаку
    [SerializeField] private float _handleHitReward = 0.1f;     // Награда за попадание по рукояти
    [SerializeField] private float _handleHitPenalty = -0.3f;   // Штраф за удар рукоятью
    [SerializeField] private float _timePenalty = -0.005f;      // Небольшой штраф за время
    [SerializeField] private float _outOfBoundsPenalty = -0.5f;

    [Header("Time Limit")]
    [SerializeField] private float _maxEpisodeTime = 30f;
    private float _episodeTimer;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer _swordRenderer;
    [SerializeField] private Color _defaultColor = Color.blue;
    [SerializeField] private Color _hitColor = Color.green;
    [SerializeField] private Color _damageColor = Color.red;
    [SerializeField] private Color _blockColor = Color.cyan;

    public int CurrentEpisode = 0;
    public float CumulativeReward = 0f;
    public int HitsScored = 0;
    public int HitsReceived = 0;

    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Coroutine _flashCoroutine;
    private float _lastCollisionTime = 0f;
    private float _collisionCooldown = 0.2f;
    private SwordPhysics _enemySwordPhysics;

    // События для GameManager
    public System.Action OnHit;  // Когда этот меч попал по врагу
    public System.Action OnGotHit; // Когда по этому мечу попали

    public override void Initialize()
    {
        _rb = GetComponent<Rigidbody2D>();
        _swordPhysics = GetComponent<SwordPhysics>();
        if (_swordRenderer == null) _swordRenderer = GetComponent<Renderer>();

        // Настройка физики для плавного вращения
        if (_rb != null)
        {
            _rb.angularDamping = _angularDrag;
        }

        _startPosition = transform.position;
        _startRotation = transform.rotation;

        CurrentEpisode = 0;
        CumulativeReward = 0f;
        HitsScored = 0;
        HitsReceived = 0;

        Debug.Log($"✅ {gameObject.name} initialized");
    }

    void Start()
    {
        if (_enemySword != null)
        {
            _enemySwordPhysics = _enemySword.GetComponent<SwordPhysics>();
        }
    }

    public override void OnEpisodeBegin()
    {
        CurrentEpisode++;
        CumulativeReward = 0f;
        _episodeTimer = _maxEpisodeTime;

        if (_swordRenderer != null) _swordRenderer.material.color = _defaultColor;

        ResetPositions();
    }

    private void ResetPositions()
    {
        // Случайное смещение для robust обучения (ключевой момент из статьи!)
        float randomX = Random.Range(-_spawnRandomOffset, _spawnRandomOffset);
        float randomY = Random.Range(-_spawnRandomOffset, _spawnRandomOffset);

        if (_mySpawnPoint != null)
        {
            transform.position = _mySpawnPoint.position + new Vector3(randomX, randomY, 0);
        }
        else
        {
            transform.position = _startPosition + new Vector3(randomX, randomY, 0);
        }

        transform.rotation = Quaternion.identity;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
    }

    private IEnumerator FlashSword(Color targetColor, float duration)
    {
        if (_swordRenderer == null) yield break;

        float elapsedTime = 0f;
        Color originalColor = _swordRenderer.material.color;
        _swordRenderer.material.color = targetColor;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            _swordRenderer.material.color = Color.Lerp(targetColor, _defaultColor, elapsedTime / duration);
            yield return null;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_enemyCharacter == null || _enemySword == null) return;

        // Получаем локальные координаты
        Vector3 myPos = _arenaCenter != null ? transform.position - _arenaCenter.position : transform.position;
        Vector3 enemyCharPos = _arenaCenter != null ? _enemyCharacter.position - _arenaCenter.position : _enemyCharacter.position;
        Vector3 enemySwordPos = _arenaCenter != null ? _enemySword.position - _arenaCenter.position : _enemySword.position;
        Vector3 myCharPos = _myCharacter != null ? (_arenaCenter != null ? _myCharacter.position - _arenaCenter.position : _myCharacter.position) : Vector3.zero;

        // 1. Относительная позиция цели
        Vector3 relativeEnemyPos = enemyCharPos - myPos;
        sensor.AddObservation(relativeEnemyPos.x / _arenaWidth);
        sensor.AddObservation(relativeEnemyPos.y / _arenaHeight);

        // 2. Относительная позиция меча противника
        Vector3 relativeEnemySwordPos = enemySwordPos - myPos;
        sensor.AddObservation(relativeEnemySwordPos.x / _arenaWidth);
        sensor.AddObservation(relativeEnemySwordPos.y / _arenaHeight);

        // 3. Относительная позиция моего персонажа
        if (_myCharacter != null)
        {
            Vector3 relativeMyPos = myCharPos - myPos;
            sensor.AddObservation(relativeMyPos.x / _arenaWidth);
            sensor.AddObservation(relativeMyPos.y / _arenaHeight);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // 4. Углы мечей
        sensor.AddObservation(_enemySword.eulerAngles.z / 360f);
        sensor.AddObservation(transform.eulerAngles.z / 360f);

        // 5. Скорости
        if (_rb != null)
        {
            sensor.AddObservation(_rb.linearVelocity.x / 5f);
            sensor.AddObservation(_rb.linearVelocity.y / 5f);
            sensor.AddObservation(_rb.angularVelocity / 180f);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // 6. Расстояние до краев арены
        float halfWidth = _arenaWidth / 2f;
        float halfHeight = _arenaHeight / 2f;

        sensor.AddObservation((myPos.x + halfWidth) / _arenaWidth);
        sensor.AddObservation((halfWidth - myPos.x) / _arenaWidth);
        sensor.AddObservation((myPos.y + halfHeight) / _arenaHeight);
        sensor.AddObservation((halfHeight - myPos.y) / _arenaHeight);

        // 7. Время эпизода
        sensor.AddObservation(_episodeTimer / _maxEpisodeTime);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveY = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float rotate = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        MoveAgent(moveX, moveY, rotate);

        // Небольшой штраф за каждый шаг
        AddReward(_timePenalty * Time.fixedDeltaTime);
        _episodeTimer -= Time.fixedDeltaTime;

        if (_episodeTimer <= 0f)
        {
            EndEpisode();
        }

        CheckBounds();
        CumulativeReward = GetCumulativeReward();
    }

    private void MoveAgent(float moveX, float moveY, float rotate)
    {
        if (_rb == null) return;

        // Движение
        Vector2 moveForce = new Vector2(moveX, moveY).normalized * _maxMoveForce;
        _rb.AddForce(moveForce, ForceMode2D.Force);

        // Вращение с уменьшенной силой для плавности
        float torque = rotate * _maxTorqueForce;
        _rb.AddTorque(torque);

        // Ограничение скорости
        if (_rb.linearVelocity.magnitude > _maxMoveForce * 1.2f)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * _maxMoveForce * 1.2f;
        }

        if (Mathf.Abs(_rb.angularVelocity) > _maxTorqueForce)
        {
            _rb.angularVelocity = Mathf.Sign(_rb.angularVelocity) * _maxTorqueForce;
        }
    }

    private void CheckBounds()
    {
        Vector3 pos = _arenaCenter != null ? transform.position - _arenaCenter.position : transform.position;

        if (Mathf.Abs(pos.x) > _arenaWidth / 2f || Mathf.Abs(pos.y) > _arenaHeight / 2f)
        {
            AddReward(_outOfBoundsPenalty);
            EndEpisode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Попадание по персонажу (ГЛАВНАЯ ЦЕЛЬ)
        if ((gameObject.CompareTag("PlayerSword") && other.CompareTag("Enemy")) ||
            (gameObject.CompareTag("EnemySword") && other.CompareTag("Player")))
        {
            HitCharacter();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Столкновение с мечом
        if (collision.gameObject.CompareTag("PlayerSword") || collision.gameObject.CompareTag("EnemySword"))
        {
            HandleSwordCollision(collision);
        }

        // Столкновение со стеной
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(_outOfBoundsPenalty * 0.1f);
        }
    }

    private void HitCharacter()
    {
        //Для обучения
        //AddReward(_hitReward);
        //HitsScored++;

        //if (_swordRenderer != null)
        //{
        //    if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        //    StartCoroutine(FlashSword(_hitColor, 0.8f));
        //}

        //// Сообщаем противнику
        //MLSword enemyAgent = _enemySword?.GetComponent<MLSword>();
        //if (enemyAgent != null)
        //{
        //    enemyAgent.GotHit();
        //}

        //CumulativeReward = GetCumulativeReward();

        //// Завершаем эпизод
        //EndEpisode();
        //if (enemyAgent != null) enemyAgent.EndEpisode();



        //Для тестирования
        // Убираем награды и завершение эпизода (это теперь в GameManager)
        HitsScored++;

        if (_swordRenderer != null)
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            StartCoroutine(FlashSword(_hitColor, 0.8f));
        }

        // Вызываем событие для GameManager
        OnHit?.Invoke();

        // Сообщаем противнику, что по нему попали
        MLSword enemyAgent = _enemySword?.GetComponent<MLSword>();
        if (enemyAgent != null)
        {
            enemyAgent.GotHit();
        }

        // НЕ вызываем EndEpisode() здесь!
    }

    public void GotHit()
    {
        //Для обучения
        //AddReward(_gotHitPenalty);
        //HitsReceived++;

        //if (_swordRenderer != null)
        //{
        //    if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        //    StartCoroutine(FlashSword(_damageColor, 0.8f));
        //}



        //Для тестирования
        HitsReceived++;

        if (_swordRenderer != null)
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            StartCoroutine(FlashSword(_damageColor, 0.8f));
        }

        // Вызываем событие для GameManager
        OnGotHit?.Invoke();

        // НЕ вызываем AddReward и EndEpisode()!
    }

    
    public void ResetAgentState() //Для тестирования
    {
        // Сбрасываем внутреннее состояние агента без завершения эпизода
        // Это нужно для GameManager при сбросе раунда

        // Сбрасываем физику
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        // Сбрасываем позицию (если не сброшена GameManager-ом)
        // transform.position = _mySpawnPoint.position;
        // transform.rotation = Quaternion.identity;

        // Сбрасываем таймеры и флаги
        _episodeTimer = _maxEpisodeTime;
        _lastCollisionTime = 0f;

        // Визуальный сброс
        if (_swordRenderer != null)
        {
            _swordRenderer.material.color = _defaultColor;
        }

        Debug.Log($"{gameObject.name}: Agent state reset");
    }

    private void HandleSwordCollision(Collision2D collision)
    {
        if (Time.time - _lastCollisionTime < _collisionCooldown) return;
        _lastCollisionTime = Time.time;

        if (_swordPhysics == null || _enemySwordPhysics == null) return;

        ContactPoint2D contact = collision.GetContact(0);
        Vector2 contactPoint = contact.point;

        string myPart = _swordPhysics.GetCollisionPartString(contactPoint);
        string otherPart = _enemySwordPhysics.GetCollisionPartString(contactPoint);

        float reward = 0f;
        Color flashColor = _defaultColor;

        // Малые награды за столкновения (не должны перевешивать главную цель!)
        if (myPart == "Blade" && otherPart == "Tip")
        {
            reward = _goodBlockReward;           // Успешная защита
            flashColor = _blockColor;
        }
        else if (myPart == "Tip" && otherPart == "Blade")
        {
            reward = _badAttackPenalty;          // Неудачная атака
            flashColor = Color.yellow;
        }
        else if (otherPart == "Handle")
        {
            reward = _handleHitReward;           // Попал по рукояти
            flashColor = Color.magenta;
        }
        else if (myPart == "Handle")
        {
            reward = _handleHitPenalty;          // Удар рукоятью
            flashColor = Color.red;
        }

        if (reward != 0f)
        {
            AddReward(reward);

            if (_swordRenderer != null)
            {
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                StartCoroutine(FlashSword(flashColor, 0.15f));
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;

        float moveX = 0f;
        float moveY = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveY += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveY -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveX += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveX -= 1f;

        Vector2 moveDirection = new Vector2(moveX, moveY).normalized;
        continuousActions[0] = moveDirection.x;
        continuousActions[1] = moveDirection.y;

        float rotate = 0f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow)) rotate = 1f;
        else if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) rotate = -1f;

        continuousActions[2] = rotate;
    }
}