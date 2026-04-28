using UnityEngine;
using System.Collections;

public class FSMSwordOld : MonoBehaviour
{
    private enum EnemyState
    {
        Defend,
        Pressure,
        Attack,
        Recover
    }

    [Header("Arena")]
    [SerializeField] private Transform _arenaCenter;
    [SerializeField] private float _arenaWidth = 20f;
    [SerializeField] private float _arenaHeight = 10f;
    [SerializeField] private float _arenaPadding = 0.5f;

    [Header("References")]
    [SerializeField] private Transform _playerSword;
    [SerializeField] private Transform _playerCharacter;
    [SerializeField] private Transform _myCharacter;

    [SerializeField] private Transform _tipPoint;
    [SerializeField] private Transform _playerTipPoint;
    [SerializeField] private Transform _playerHandlePoint;
    [SerializeField] private Transform _playerAttackPoint;

    [SerializeField] private SwordPhysics _swordPhysics;
    [SerializeField] private Rigidbody2D _rb;

    [Header("Spawn")]
    [SerializeField] private Transform _mySpawnPoint;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _pressureMoveSpeed = 6.5f;
    [SerializeField] private float _defenseMoveSpeed = 6.0f;
    [SerializeField] private float _recoverMoveSpeed = 5.0f;
    [SerializeField] private float _rotationSpeed = 300f;
    [SerializeField] private float _acceleration = 12f;
    [SerializeField] private float _deceleration = 15f;

    [Header("Attack")]
    [SerializeField] private Vector2 _fallbackAttackOffset = new Vector2(0f, 1.0f);
    [SerializeField] private float _attackTipSlowdownDistance = 0.22f;
    [SerializeField] private float _attackCommitTime = 0.22f;
    [SerializeField] private float _attackMaxDuration = 0.65f;
    [SerializeField] private float _attackMinSpeedFactor = 0.35f;
    [SerializeField] private float _attackCorrectionWeight = 0.12f;

    [Header("Pressure / Defense")]
    [SerializeField] private float _defenseArriveDistance = 0.08f;
    [SerializeField] private float _pressureTowardPlayer = 0.55f;
    [SerializeField] private float _pushForce = 8f;
    [SerializeField] private float _collisionCooldown = 0.12f;
    [SerializeField] private float _defenseTouchBias = 0.18f;
    [SerializeField] private float _pressureDesiredDistanceToPlayerSword = 1.0f;

    [Header("Recover")]
    [SerializeField] private float _recoverDuration = 0.22f;

    [Header("Decision")]
    [SerializeField] private float _attackEnterAdvantage = 0.25f;
    [SerializeField] private float _attackExitAdvantage = -0.10f;
    [SerializeField] private float _dangerRadius = 2.2f;
    [SerializeField] private float _pressureRadius = 2.8f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer _swordRenderer;
    [SerializeField] private Color _defaultColor = Color.blue;
    [SerializeField] private Color _attackColor = Color.red;
    [SerializeField] private Color _defendColor = Color.yellow;
    [SerializeField] private Color _pressureColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color _recoverColor = Color.gray;
    [SerializeField] private Color _hitColor = Color.magenta;

    private EnemyState _currentState = EnemyState.Defend;

    private Vector2 _desiredVelocity;
    private float _targetRotation;
    private float _lastCollisionTime;
    private float _stateTimer;
    private float _attackStartTipDistance;
    private Coroutine _flashCoroutine;

    public System.Action OnHit;
    public System.Action OnGotHit;

    private Vector2 ArenaCenterPos => _arenaCenter != null ? (Vector2)_arenaCenter.position : Vector2.zero;
    private Vector2 HandlePos => transform.position;

    private Vector2 MyTipPos
    {
        get
        {
            if (_tipPoint != null) return _tipPoint.position;
            return transform.position;
        }
    }

    private Vector2 PlayerTipPos
    {
        get
        {
            if (_playerTipPoint != null) return _playerTipPoint.position;
            if (_playerSword != null) return _playerSword.position;
            return Vector2.zero;
        }
    }

    private Vector2 PlayerHandlePos
    {
        get
        {
            if (_playerHandlePoint != null) return _playerHandlePoint.position;
            if (_playerSword != null) return _playerSword.position;
            return Vector2.zero;
        }
    }

    private Vector2 PlayerSwordDir
    {
        get
        {
            Vector2 dir = PlayerTipPos - PlayerHandlePos;
            if (dir.sqrMagnitude < 0.0001f) return Vector2.right;
            return dir.normalized;
        }
    }

    private Vector2 PlayerAttackTargetPos
    {
        get
        {
            if (_playerAttackPoint != null)
                return _playerAttackPoint.position;

            if (_playerCharacter != null)
            {
                SpriteRenderer sr = _playerCharacter.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    Bounds b = sr.bounds;
                    return new Vector2(b.center.x, b.center.y + b.size.y * 0.15f);
                }

                Collider2D col = _playerCharacter.GetComponentInChildren<Collider2D>();
                if (col != null)
                {
                    Bounds b = col.bounds;
                    return new Vector2(b.center.x, b.center.y + b.size.y * 0.15f);
                }

                return (Vector2)_playerCharacter.position + _fallbackAttackOffset;
            }

            return Vector2.zero;
        }
    }

    private Vector2 PlayerCharPos => _playerCharacter != null ? (Vector2)_playerCharacter.position : Vector2.zero;
    private Vector2 MyCharPos => _myCharacter != null ? (Vector2)_myCharacter.position : Vector2.zero;

    private float SwordLength
    {
        get
        {
            if (_tipPoint == null) return 1.5f;
            return Mathf.Max(0.1f, Vector2.Distance(HandlePos, MyTipPos));
        }
    }

    private float MyThreatDistance => Vector2.Distance(MyTipPos, PlayerAttackTargetPos);
    private float PlayerThreatDistance => Vector2.Distance(PlayerTipPos, MyCharPos);
    private bool IsKnockedBack => _swordPhysics != null && _swordPhysics.IsKnockedBack();

    void Start()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        if (_swordPhysics == null) _swordPhysics = GetComponent<SwordPhysics>();

        if (_playerSword != null)
        {
            if (_playerTipPoint == null)
            {
                Transform foundTip = _playerSword.Find("TipZone");
                if (foundTip != null) _playerTipPoint = foundTip;
            }

            if (_playerHandlePoint == null)
            {
                Transform foundHandle = _playerSword.Find("HandleZone");
                if (foundHandle != null) _playerHandlePoint = foundHandle;
            }
        }

        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.linearDamping = 2f;
            _rb.angularDamping = 2f;
        }

        ResetState();
    }

    void Update()
    {
        if (_playerSword == null || _playerCharacter == null || _myCharacter == null)
            return;

        _stateTimer += Time.deltaTime;

        if (IsKnockedBack)
        {
            _desiredVelocity = Vector2.zero;
            return;
        }

        switch (_currentState)
        {
            case EnemyState.Defend:
                UpdateDefend();
                break;
            case EnemyState.Pressure:
                UpdatePressure();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.Recover:
                UpdateRecover();
                break;
        }

        EvaluateTransitions();
        ClampTransformToArena();
    }

    void FixedUpdate()
    {
        if (IsKnockedBack)
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
            return;
        }

        ApplyMovement();
        ApplyRotation();
    }

    private void UpdateDefend()
    {
        GetOptimalDeflectPose(out Vector2 desiredHandlePos, out float desiredRotation);

        _targetRotation = desiredRotation;

        Vector2 toTarget = desiredHandlePos - HandlePos;
        float distance = toTarget.magnitude;

        if (distance > _defenseArriveDistance)
        {
            _desiredVelocity = toTarget.normalized * _defenseMoveSpeed;
            return;
        }

        Vector2 toPlayerSword = ((_playerSword != null ? (Vector2)_playerSword.position : PlayerTipPos) - HandlePos);

        if (toPlayerSword.sqrMagnitude > 0.0001f)
        {
            Vector2 pressDir = toPlayerSword.normalized;
            Vector2 towardPlayer = PlayerCharPos - HandlePos;

            if (towardPlayer.sqrMagnitude > 0.0001f)
                towardPlayer.Normalize();
            else
                towardPlayer = Vector2.right;

            _desiredVelocity =
                (pressDir * 0.7f + towardPlayer * 0.3f).normalized *
                (_defenseMoveSpeed * 0.45f);
        }
        else
        {
            _desiredVelocity = Vector2.zero;
        }
    }

    private void UpdatePressure()
    {
        Vector2 playerSwordPos = _playerSword != null ? (Vector2)_playerSword.position : PlayerTipPos;
        Vector2 toSword = playerSwordPos - HandlePos;

        Vector2 pushOutDir = playerSwordPos - MyCharPos;
        if (pushOutDir.sqrMagnitude < 0.0001f)
            pushOutDir = Vector2.right;
        pushOutDir.Normalize();

        Vector2 towardPlayer = PlayerCharPos - playerSwordPos;
        if (towardPlayer.sqrMagnitude < 0.0001f)
            towardPlayer = Vector2.right;
        towardPlayer.Normalize();

        Vector2 desiredPressureDir = (pushOutDir + towardPlayer * 0.8f).normalized;

        float enemySwordAngle = Mathf.Atan2(PlayerSwordDir.y, PlayerSwordDir.x) * Mathf.Rad2Deg;
        float candidateA = enemySwordAngle + 65f;
        float candidateB = enemySwordAngle - 65f;

        Vector2 dirA = DirectionFromAngle(candidateA);
        Vector2 dirB = DirectionFromAngle(candidateB);

        _targetRotation = Vector2.Dot(dirA, desiredPressureDir) >= Vector2.Dot(dirB, desiredPressureDir)
            ? candidateA
            : candidateB;

        Vector2 pressureContact = playerSwordPos + desiredPressureDir * 0.65f;
        Vector2 mySwordDir = DirectionFromAngle(_targetRotation);
        Vector2 desiredHandlePos = pressureContact - mySwordDir * (SwordLength * 0.55f);
        desiredHandlePos -= mySwordDir * _defenseTouchBias;
        desiredHandlePos = ClampPointToArena(desiredHandlePos);

        Vector2 move = desiredHandlePos - HandlePos;

        if (move.magnitude > _defenseArriveDistance)
        {
            _desiredVelocity = move.normalized * _pressureMoveSpeed;
        }
        else
        {
            _desiredVelocity = desiredPressureDir * (_pressureMoveSpeed * 0.6f);
        }

        float distToSword = toSword.magnitude;
        if (distToSword < _pressureDesiredDistanceToPlayerSword)
        {
            _desiredVelocity += towardPlayer * (_pressureMoveSpeed * 0.25f);
            if (_desiredVelocity.sqrMagnitude > 0.0001f)
                _desiredVelocity = _desiredVelocity.normalized * _pressureMoveSpeed;
        }
    }

    private void UpdateAttack()
    {
        Vector2 target = PlayerAttackTargetPos;
        Vector2 toTarget = target - HandlePos;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            _desiredVelocity = Vector2.zero;
            return;
        }

        Vector2 dirToTarget = toTarget.normalized;

        // Всегда разворачиваем кончик прямо в цель
        _targetRotation = Mathf.Atan2(dirToTarget.y, dirToTarget.x) * Mathf.Rad2Deg;

        // Идеальная позиция рукояти:
        // если рукоять встанет сюда, то кончик окажется в target
        Vector2 desiredHandlePos = target - dirToTarget * SwordLength;
        desiredHandlePos = ClampAttackHandlePos(desiredHandlePos, dirToTarget);

        Vector2 toDesiredHandle = desiredHandlePos - HandlePos;
        float handleDistance = toDesiredHandle.magnitude;
        float tipDistance = Vector2.Distance(MyTipPos, target);

        // Пока не довёрнут — не стоим, но и не "роем вниз"
        Vector2 swordForward = (MyTipPos - HandlePos).normalized;
        float align = Vector2.Dot(swordForward, dirToTarget);

        float moveFactor = Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(0.4f, 0.95f, align));

        if (handleDistance > 0.03f)
        {
            _desiredVelocity = toDesiredHandle.normalized * (_moveSpeed * moveFactor);
        }
        else
        {
            _desiredVelocity = Vector2.zero;
        }

        // Финальное мягкое дотягивание
        if (tipDistance < _attackTipSlowdownDistance)
        {
            _desiredVelocity *= 0.35f;
        }
    }

    private Vector2 ClampAttackHandlePos(Vector2 desiredHandlePos, Vector2 attackDir)
    {
        Vector2 center = ArenaCenterPos;

        float halfW = _arenaWidth * 0.5f - _arenaPadding;
        float halfH = _arenaHeight * 0.5f - _arenaPadding;

        // Дополнительный запас под длину меча,
        // чтобы кончик не пытался уехать за край арены
        float xMargin = Mathf.Abs(attackDir.x) * SwordLength * 0.65f;
        float yMargin = Mathf.Abs(attackDir.y) * SwordLength * 0.65f;

        float minX = center.x - halfW + xMargin;
        float maxX = center.x + halfW - xMargin;
        float minY = center.y - halfH + yMargin;
        float maxY = center.y + halfH - yMargin;

        desiredHandlePos.x = Mathf.Clamp(desiredHandlePos.x, minX, maxX);
        desiredHandlePos.y = Mathf.Clamp(desiredHandlePos.y, minY, maxY);

        return desiredHandlePos;
    }

    private void UpdateRecover()
    {
        Vector2 awayFromPlayerSword = HandlePos - (_playerSword != null ? (Vector2)_playerSword.position : PlayerTipPos);
        if (awayFromPlayerSword.sqrMagnitude < 0.0001f)
            awayFromPlayerSword = HandlePos - MyCharPos;

        if (awayFromPlayerSword.sqrMagnitude < 0.0001f)
            awayFromPlayerSword = Vector2.left;

        awayFromPlayerSword.Normalize();

        _desiredVelocity = awayFromPlayerSword * (_recoverMoveSpeed * 0.7f);

        Vector2 towardPlayerSword = ((_playerSword != null ? (Vector2)_playerSword.position : PlayerTipPos) - HandlePos);
        if (towardPlayerSword.sqrMagnitude > 0.0001f)
            _targetRotation = Mathf.Atan2(towardPlayerSword.y, towardPlayerSword.x) * Mathf.Rad2Deg;
    }

    private void EvaluateTransitions()
    {
        float myThreat = MyThreatDistance;
        float playerThreat = PlayerThreatDistance;
        float advantage = playerThreat - myThreat;

        float playerSwordToMyChar = Vector2.Distance(
            _playerSword != null ? (Vector2)_playerSword.position : PlayerTipPos,
            MyCharPos
        );

        float distanceToPlayerSword = Vector2.Distance(
            HandlePos,
            _playerSword != null ? (Vector2)_playerSword.position : PlayerTipPos
        );

        switch (_currentState)
        {
            case EnemyState.Defend:
                {
                    bool swordIsClose = playerSwordToMyChar <= _pressureRadius;
                    bool safeToPress = swordIsClose || distanceToPlayerSword <= (_pressureRadius + 0.5f);

                    if (safeToPress)
                    {
                        SetState(EnemyState.Pressure);
                        return;
                    }

                    if (advantage > _attackEnterAdvantage && playerThreat > _dangerRadius)
                    {
                        BeginAttack();
                        return;
                    }

                    break;
                }

            case EnemyState.Pressure:
                {
                    bool playerStillDangerous =
                        playerThreat <= _dangerRadius ||
                        playerSwordToMyChar <= (_dangerRadius + 0.6f);

                    if (!playerStillDangerous && advantage > _attackEnterAdvantage)
                    {
                        BeginAttack();
                        return;
                    }

                    if (distanceToPlayerSword > _pressureRadius + 1.0f)
                    {
                        SetState(EnemyState.Defend);
                        return;
                    }

                    break;
                }

            case EnemyState.Attack:
                {
                    if (_stateTimer < _attackCommitTime)
                        return;

                    float currentTipDistance = Vector2.Distance(MyTipPos, PlayerAttackTargetPos);

                    bool failedAttack =
                        _stateTimer > _attackMaxDuration;

                    bool shouldAbort =
                        playerThreat <= _dangerRadius ||
                        advantage < _attackExitAdvantage;

                    bool wallStuck =
                        IsNearArenaEdge(HandlePos, 0.45f) &&
                        currentTipDistance > 0.35f;

                    // Нет реального прогресса к цели
                    bool noProgress =
                        _stateTimer > (_attackCommitTime + 0.18f) &&
                        currentTipDistance > _attackStartTipDistance - 0.03f;

                    if (failedAttack || shouldAbort || wallStuck || noProgress)
                    {
                        SetState(EnemyState.Recover);
                        return;
                    }

                    break;
                }

            case EnemyState.Recover:
                {
                    if (_stateTimer >= _recoverDuration)
                    {
                        if (playerThreat <= _pressureRadius || playerSwordToMyChar <= _pressureRadius)
                            SetState(EnemyState.Pressure);
                        else
                            SetState(EnemyState.Defend);

                        return;
                    }

                    break;
                }
        }
    }

    private void BeginAttack()
    {
        _attackStartTipDistance = Vector2.Distance(MyTipPos, PlayerAttackTargetPos);
        SetState(EnemyState.Attack);
    }

    private void GetOptimalDeflectPose(out Vector2 desiredHandlePos, out float desiredRotation)
    {
        Vector2 playerHandle = PlayerHandlePos;
        Vector2 playerTip = PlayerTipPos;
        Vector2 playerDir = PlayerSwordDir;

        Vector2 playerNormalA = new Vector2(-playerDir.y, playerDir.x);
        Vector2 playerNormalB = -playerNormalA;

        Vector2 playerSwordMid = (playerHandle + playerTip) * 0.5f;
        Vector2 toMyCharFromPlayerSword = MyCharPos - playerSwordMid;

        if (toMyCharFromPlayerSword.sqrMagnitude < 0.0001f)
            toMyCharFromPlayerSword = Vector2.left;

        toMyCharFromPlayerSword.Normalize();

        Vector2 chosenNormal =
            Vector2.Dot(playerNormalA, toMyCharFromPlayerSword) >
            Vector2.Dot(playerNormalB, toMyCharFromPlayerSword)
                ? playerNormalA
                : playerNormalB;

        Vector2 contactPoint = Vector2.Lerp(playerHandle, playerTip, 0.72f);
        contactPoint += chosenNormal * 0.35f;

        Vector2 pushAwayFromMyChar = contactPoint - MyCharPos;
        if (pushAwayFromMyChar.sqrMagnitude < 0.0001f)
            pushAwayFromMyChar = Vector2.right;
        pushAwayFromMyChar.Normalize();

        Vector2 towardPlayer = PlayerCharPos - contactPoint;
        if (towardPlayer.sqrMagnitude < 0.0001f)
            towardPlayer = Vector2.right;
        towardPlayer.Normalize();

        Vector2 desiredPush = (pushAwayFromMyChar + towardPlayer * 0.55f).normalized;

        float playerAngle = Mathf.Atan2(playerDir.y, playerDir.x) * Mathf.Rad2Deg;
        float candidateA = playerAngle + 65f;
        float candidateB = playerAngle - 65f;

        Vector2 dirA = DirectionFromAngle(candidateA);
        Vector2 dirB = DirectionFromAngle(candidateB);

        float scoreA = Vector2.Dot(dirA, desiredPush);
        float scoreB = Vector2.Dot(dirB, desiredPush);

        desiredRotation = scoreA >= scoreB ? candidateA : candidateB;

        Vector2 mySwordDir = DirectionFromAngle(desiredRotation);
        desiredHandlePos = contactPoint - mySwordDir * (SwordLength * 0.55f);
        desiredHandlePos -= mySwordDir * _defenseTouchBias;
        desiredHandlePos = ClampPointToArena(desiredHandlePos);
    }

    private void ApplyMovement()
    {
        if (_rb == null) return;

        if (_desiredVelocity.magnitude > 0.01f)
        {
            _rb.linearVelocity = Vector2.Lerp(
                _rb.linearVelocity,
                _desiredVelocity,
                _acceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            _rb.linearVelocity = Vector2.Lerp(
                _rb.linearVelocity,
                Vector2.zero,
                _deceleration * Time.fixedDeltaTime
            );
        }

        float maxSpeed = GetCurrentMaxSpeed();
        if (_rb.linearVelocity.magnitude > maxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }
    }

    private float GetCurrentMaxSpeed()
    {
        switch (_currentState)
        {
            case EnemyState.Attack: return _moveSpeed;
            case EnemyState.Pressure: return _pressureMoveSpeed;
            case EnemyState.Recover: return _recoverMoveSpeed;
            default: return _defenseMoveSpeed;
        }
    }

    private void ApplyRotation()
    {
        float currentZ = transform.eulerAngles.z;
        float angleDiff = Mathf.DeltaAngle(currentZ, _targetRotation);
        float step = _rotationSpeed * Time.fixedDeltaTime;

        if (Mathf.Abs(angleDiff) <= step)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, _targetRotation);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, currentZ + Mathf.Sign(angleDiff) * step);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            HitCharacter();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("PlayerSword")) return;
        if (Time.time - _lastCollisionTime < _collisionCooldown) return;

        _lastCollisionTime = Time.time;

        if (_currentState == EnemyState.Defend || _currentState == EnemyState.Pressure)
        {
            ApplyOptimalPush();
        }
        else if (_currentState == EnemyState.Attack && _stateTimer >= _attackCommitTime * 0.5f)
        {
            SetState(EnemyState.Recover);
        }
    }

    private void ApplyOptimalPush()
    {
        if (_playerSword == null) return;

        Rigidbody2D playerRb = _playerSword.GetComponent<Rigidbody2D>();
        if (playerRb == null) return;

        Vector2 playerSwordPos = _playerSword.position;

        Vector2 pushOutDir = playerSwordPos - MyCharPos;
        if (pushOutDir.sqrMagnitude < 0.0001f)
            pushOutDir = Vector2.right;
        pushOutDir.Normalize();

        Vector2 towardPlayer = PlayerCharPos - playerSwordPos;
        if (towardPlayer.sqrMagnitude < 0.0001f)
            towardPlayer = Vector2.right;
        towardPlayer.Normalize();

        Vector2 finalPush = (pushOutDir + towardPlayer * 0.7f).normalized;

        playerRb.linearVelocity = finalPush * _pushForce;

        Vector2 playerLocalForward = PlayerTipPos - playerSwordPos;
        if (playerLocalForward.sqrMagnitude < 0.0001f)
            playerLocalForward = Vector2.right;
        playerLocalForward.Normalize();

        float signed = Vector2.SignedAngle(playerLocalForward, finalPush);
        playerRb.angularVelocity = Mathf.Sign(signed) * (_rotationSpeed * 0.45f);
    }

    private void SetState(EnemyState newState, bool force = false)
    {
        if (!force && newState == _currentState) return;

        _currentState = newState;
        _stateTimer = 0f;

        switch (_currentState)
        {
            case EnemyState.Defend:
                SetBaseColor(_defendColor);
                break;
            case EnemyState.Pressure:
                SetBaseColor(_pressureColor);
                break;
            case EnemyState.Attack:
                SetBaseColor(_attackColor);
                break;
            case EnemyState.Recover:
                SetBaseColor(_recoverColor);
                break;
        }
    }

    private void SetBaseColor(Color color)
    {
        if (_swordRenderer == null) return;
        _swordRenderer.material.color = color;
    }

    public void GotHit()
    {
        FlashColor(_hitColor, 0.3f);
        OnGotHit?.Invoke();
        SetState(EnemyState.Defend, true);
    }

    public void ResetState()
    {
        _desiredVelocity = Vector2.zero;
        _targetRotation = 0f;
        _lastCollisionTime = 0f;
        _stateTimer = 0f;
        _attackStartTipDistance = 0f;

        if (_mySpawnPoint != null)
        {
            transform.position = _mySpawnPoint.position;
        }

        transform.rotation = Quaternion.identity;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        SetState(EnemyState.Defend, true);
    }

    private void HitCharacter()
    {
        FlashColor(_hitColor, 0.25f);
        OnHit?.Invoke();
    }

    private Vector2 DirectionFromAngle(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    private Vector2 ClampPointToArena(Vector2 worldPoint)
    {
        Vector2 center = ArenaCenterPos;
        float halfW = _arenaWidth * 0.5f - _arenaPadding;
        float halfH = _arenaHeight * 0.5f - _arenaPadding;

        worldPoint.x = Mathf.Clamp(worldPoint.x, center.x - halfW, center.x + halfW);
        worldPoint.y = Mathf.Clamp(worldPoint.y, center.y - halfH, center.y + halfH);
        return worldPoint;
    }

    private void ClampTransformToArena()
    {
        transform.position = ClampPointToArena(transform.position);
    }

    private bool IsNearArenaEdge(Vector2 pos, float margin = 0.6f)
    {
        float left = ArenaCenterPos.x - _arenaWidth * 0.5f + _arenaPadding;
        float right = ArenaCenterPos.x + _arenaWidth * 0.5f - _arenaPadding;
        float bottom = ArenaCenterPos.y - _arenaHeight * 0.5f + _arenaPadding;
        float top = ArenaCenterPos.y + _arenaHeight * 0.5f - _arenaPadding;

        return
            pos.x < left + margin ||
            pos.x > right - margin ||
            pos.y < bottom + margin ||
            pos.y > top - margin;
    }

    private void FlashColor(Color color, float duration)
    {
        if (_swordRenderer == null) return;

        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        Color oldColor = _swordRenderer.material.color;
        _swordRenderer.material.color = color;
        yield return new WaitForSeconds(duration);

        switch (_currentState)
        {
            case EnemyState.Attack:
                _swordRenderer.material.color = _attackColor;
                break;
            case EnemyState.Defend:
                _swordRenderer.material.color = _defendColor;
                break;
            case EnemyState.Pressure:
                _swordRenderer.material.color = _pressureColor;
                break;
            case EnemyState.Recover:
                _swordRenderer.material.color = _recoverColor;
                break;
            default:
                _swordRenderer.material.color = oldColor;
                break;
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = _arenaCenter != null ? (Vector2)_arenaCenter.position : Vector2.zero;

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(center, new Vector3(_arenaWidth, _arenaHeight, 0f));

        if (_tipPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(_tipPoint.position, 0.08f);
            Gizmos.DrawLine(transform.position, _tipPoint.position);
        }

        if (_playerTipPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_playerTipPoint.position, 0.08f);
        }

        if (_playerHandlePoint != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawSphere(_playerHandlePoint.position, 0.08f);
        }

        if (_playerAttackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_playerAttackPoint.position, 0.1f);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(MyTipPos, PlayerAttackTargetPos);

            GetOptimalDeflectPose(out Vector2 handlePos, out float rot);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(handlePos, 0.12f);
            Gizmos.DrawRay(handlePos, DirectionFromAngle(rot) * SwordLength);
        }
    }
}