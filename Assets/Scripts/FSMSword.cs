using UnityEngine;
using System.Collections;
using static SwordActionTypes;

public class FSMSword : MonoBehaviour
{
    private enum EnemyState
    {
        Defend,
        Pressure,
        Attack,
        Recover
    }

    [Header("Core")]
    [SerializeField] private SwordCombatContext _context;
    [SerializeField] private SwordActionExecutor _executor;
    [SerializeField] private SwordPhysics _swordPhysics;
    [SerializeField] private Rigidbody2D _rb;

    [Header("Spawn")]
    [SerializeField] private Transform _mySpawnPoint;

    [Header("State Decision")]
    [SerializeField] private float _dangerRadius = 2.2f;
    [SerializeField] private float _pressureRadius = 2.8f;
    [SerializeField] private float _attackEnterAdvantage = 0.25f;
    [SerializeField] private float _attackExitAdvantage = -0.10f;

    [Header("Attack")]
    [SerializeField] private float _attackCommitTime = 0.35f;
    [SerializeField] private float _attackMaxDuration = 1.6f;
    [SerializeField] private float _attackSuccessDistance = 0.25f;
    [SerializeField] private float _attackHandleDistance = 0.12f;

    [Header("Defend / Pressure")]
    [SerializeField] private float _deflectContactT = 0.72f;
    [SerializeField] private float _deflectSideOffset = 0.35f;
    [SerializeField] private float _deflectBladeAngle = 65f;
    [SerializeField] private float _pressureContactOffset = 0.65f;
    [SerializeField] private float _pressureTowardPlayerWeight = 0.8f;

    [Header("Recover")]
    [SerializeField] private float _recoverDuration = 0.25f;

    [Header("Discrete Action Tuning")]
    [SerializeField] private float _moveDeadZone = 0.03f;
    [SerializeField] private float _rotationDeadZone = 4f;

    [Header("Hit Detection")]
    [SerializeField] private float _hitCooldown = 0.15f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer _swordRenderer;
    [SerializeField] private Color _defendColor = Color.yellow;
    [SerializeField] private Color _pressureColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color _attackColor = Color.red;
    [SerializeField] private Color _recoverColor = Color.gray;
    [SerializeField] private Color _hitColor = Color.magenta;

    private EnemyState _currentState = EnemyState.Defend;
    private float _stateTimer;
    private float _attackStartTipDistance;
    private float _lastCollisionTime;
    private float _lastHitTime;

    private readonly float _collisionCooldown = 0.12f;
    private Coroutine _flashCoroutine;

    public System.Action OnHit;
    public System.Action OnGotHit;

    private bool IsKnockedBack
    {
        get
        {
            return _swordPhysics != null && _swordPhysics.IsKnockedBack();
        }
    }

    private void Awake()
    {
        if (_context == null) _context = GetComponent<SwordCombatContext>();
        if (_executor == null) _executor = GetComponent<SwordActionExecutor>();
        if (_swordPhysics == null) _swordPhysics = GetComponent<SwordPhysics>();
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        ResetState();
    }

    private void Update()
    {
        if (_context == null || _executor == null)
            return;

        _stateTimer += Time.deltaTime;

        if (IsKnockedBack)
        {
            _executor.SetAction(new SwordAction(SwordMoveAction.None, SwordRotateAction.None));
            return;
        }

        EvaluateTransitions();

        SwordAction action = ChooseActionByState();
        _executor.SetAction(action);
    }

    private void EvaluateTransitions()
    {
        float myThreat = _context.MyThreatDistance;
        float opponentThreat = _context.OpponentThreatDistance;
        float advantage = _context.ThreatAdvantage;

        float opponentTipToMyChar = opponentThreat;
        float mySwordToOpponentSword = Vector2.Distance(_context.MySwordPos, _context.OpponentSwordPos);

        switch (_currentState)
        {
            case EnemyState.Defend:
                {
                    bool opponentIsDangerous = opponentTipToMyChar <= _pressureRadius;
                    bool swordsAreClose = mySwordToOpponentSword <= _pressureRadius;

                    if (opponentIsDangerous || swordsAreClose)
                    {
                        SetState(EnemyState.Pressure);
                        return;
                    }

                    if (advantage > _attackEnterAdvantage && opponentThreat > _dangerRadius)
                    {
                        BeginAttack();
                        return;
                    }

                    break;
                }

            case EnemyState.Pressure:
                {
                    bool opponentStillDangerous = opponentThreat <= _dangerRadius;
                    bool opponentSwordCloseToMe = opponentTipToMyChar <= _dangerRadius + 0.6f;

                    if (!opponentStillDangerous && !opponentSwordCloseToMe && advantage > _attackEnterAdvantage)
                    {
                        BeginAttack();
                        return;
                    }

                    if (mySwordToOpponentSword > _pressureRadius + 1.2f)
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

                    float currentTipDistance = _context.MyThreatDistance;

                    bool reachedTarget = currentTipDistance <= _attackSuccessDistance;
                    bool attackTooLong = _stateTimer > _attackMaxDuration;
                    bool opponentTooDangerous = opponentThreat <= _dangerRadius;
                    bool lostAdvantage = advantage < _attackExitAdvantage;

                    bool nearWallAndNotClose =
                        _context.IsNearArenaEdge(_context.MySwordPos, 0.55f) &&
                        currentTipDistance > 0.45f;

                    if (reachedTarget)
                    {
                        // Попадание должно засчитаться через BladeZone/TipZone.
                        // Состояние специально не меняем, чтобы trigger/stay успел сработать.
                        return;
                    }

                    if (attackTooLong || opponentTooDangerous || lostAdvantage || nearWallAndNotClose)
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
                        if (opponentThreat <= _pressureRadius || mySwordToOpponentSword <= _pressureRadius)
                            SetState(EnemyState.Pressure);
                        else
                            SetState(EnemyState.Defend);

                        return;
                    }

                    break;
                }
        }
    }

    private SwordAction ChooseActionByState()
    {
        switch (_currentState)
        {
            case EnemyState.Attack:
                return ChooseAttackAction();

            case EnemyState.Pressure:
                return ChoosePressureAction();

            case EnemyState.Recover:
                return ChooseRecoverAction();

            case EnemyState.Defend:
            default:
                return ChooseDefendAction();
        }
    }

    private SwordAction ChooseAttackAction()
    {
        Vector2 target = _context.OpponentAttackTargetPos;

        Vector2 toTargetFromHandle = target - _context.MyHandlePos;
        Vector2 toTargetFromTip = target - _context.MyTipPos;

        Vector2 desiredSwordDir = SafeNormalize(toTargetFromHandle, _context.MySwordDir);

        Vector2 desiredHandlePos = target - desiredSwordDir * GetSwordLength();
        desiredHandlePos = _context.ClampPointToArena(desiredHandlePos);

        Vector2 moveToIdealHandle = desiredHandlePos - _context.MySwordPos;

        float tipDistance = toTargetFromTip.magnitude;
        float handleDistance = moveToIdealHandle.magnitude;

        Vector2 moveVector;

        // Главная правка:
        // если кончик еще не достал цель, двигаемся по направлению кончика к цели,
        // а не останавливаемся из-за того, что рукоять уже близко к "идеальной" позиции.
        if (tipDistance > _attackSuccessDistance)
        {
            Vector2 tipChase = SafeNormalize(toTargetFromTip, desiredSwordDir);

            if (handleDistance > _attackHandleDistance)
            {
                Vector2 handleChase = SafeNormalize(moveToIdealHandle, tipChase);

                // Смешиваем подведение рукояти и дотягивание кончиком.
                // Приоритет у кончика, чтобы меч не застревал рядом с персонажем.
                moveVector = (tipChase * 0.75f + handleChase * 0.25f).normalized;
            }
            else
            {
                moveVector = tipChase;
            }
        }
        else
        {
            // Уже почти достали — продолжаем легкое давление вперед,
            // чтобы OnTriggerStay2D/OnTriggerEnter2D успел зарегистрировать попадание.
            moveVector = desiredSwordDir;
        }

        SwordMoveAction move = DirectionToMoveAction(moveVector);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private SwordAction ChooseDefendAction()
    {
        GetDeflectPose(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir);

        SwordMoveAction move = DirectionToMoveAction(desiredHandlePos - _context.MySwordPos);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private SwordAction ChoosePressureAction()
    {
        GetPressurePose(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir);

        SwordMoveAction move = DirectionToMoveAction(desiredHandlePos - _context.MySwordPos);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private SwordAction ChooseRecoverAction()
    {
        Vector2 away = _context.MySwordPos - _context.OpponentTipPos;

        if (away.sqrMagnitude < 0.0001f)
            away = _context.MySwordPos - _context.MyCharacterPos;

        if (away.sqrMagnitude < 0.0001f)
            away = Vector2.left;

        Vector2 lookToOpponentSword = _context.OpponentSwordPos - _context.MyHandlePos;
        Vector2 desiredSwordDir = SafeNormalize(lookToOpponentSword, _context.MySwordDir);

        SwordMoveAction move = DirectionToMoveAction(away);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private void GetDeflectPose(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir)
    {
        Vector2 opponentHandle = _context.OpponentHandlePos;
        Vector2 opponentTip = _context.OpponentTipPos;
        Vector2 opponentDir = _context.OpponentSwordDir;

        Vector2 normalA = new Vector2(-opponentDir.y, opponentDir.x);
        Vector2 normalB = -normalA;

        Vector2 opponentSwordMid = (opponentHandle + opponentTip) * 0.5f;
        Vector2 toMyCharacter = _context.MyCharacterPos - opponentSwordMid;

        if (toMyCharacter.sqrMagnitude < 0.0001f)
            toMyCharacter = Vector2.left;

        toMyCharacter.Normalize();

        Vector2 chosenNormal =
            Vector2.Dot(normalA, toMyCharacter) > Vector2.Dot(normalB, toMyCharacter)
                ? normalA
                : normalB;

        Vector2 contactPoint = Vector2.Lerp(opponentHandle, opponentTip, _deflectContactT);
        contactPoint += chosenNormal * _deflectSideOffset;

        Vector2 pushAwayFromMyChar = contactPoint - _context.MyCharacterPos;
        if (pushAwayFromMyChar.sqrMagnitude < 0.0001f)
            pushAwayFromMyChar = Vector2.right;

        pushAwayFromMyChar.Normalize();

        Vector2 towardOpponentCharacter = _context.OpponentCharacterPos - contactPoint;
        if (towardOpponentCharacter.sqrMagnitude < 0.0001f)
            towardOpponentCharacter = Vector2.right;

        towardOpponentCharacter.Normalize();

        Vector2 desiredPush = (pushAwayFromMyChar + towardOpponentCharacter * 0.55f).normalized;

        desiredSwordDir = ChooseBladeCrossDirection(opponentDir, desiredPush);

        desiredHandlePos = contactPoint - desiredSwordDir * (GetSwordLength() * 0.55f);
        desiredHandlePos = _context.ClampPointToArena(desiredHandlePos);
    }

    private void GetPressurePose(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir)
    {
        Vector2 opponentSwordPos = _context.OpponentSwordPos;

        Vector2 pushOutDir = opponentSwordPos - _context.MyCharacterPos;
        if (pushOutDir.sqrMagnitude < 0.0001f)
            pushOutDir = Vector2.right;

        pushOutDir.Normalize();

        Vector2 towardOpponentCharacter = _context.OpponentCharacterPos - opponentSwordPos;
        if (towardOpponentCharacter.sqrMagnitude < 0.0001f)
            towardOpponentCharacter = Vector2.right;

        towardOpponentCharacter.Normalize();

        Vector2 pressureDir = (pushOutDir + towardOpponentCharacter * _pressureTowardPlayerWeight).normalized;

        desiredSwordDir = ChooseBladeCrossDirection(_context.OpponentSwordDir, pressureDir);

        Vector2 contactPoint = opponentSwordPos + pressureDir * _pressureContactOffset;
        desiredHandlePos = contactPoint - desiredSwordDir * (GetSwordLength() * 0.55f);
        desiredHandlePos = _context.ClampPointToArena(desiredHandlePos);
    }

    private Vector2 ChooseBladeCrossDirection(Vector2 opponentSwordDir, Vector2 desiredPushDir)
    {
        float baseAngle = Mathf.Atan2(opponentSwordDir.y, opponentSwordDir.x) * Mathf.Rad2Deg;

        Vector2 candidateA = DirectionFromAngle(baseAngle + _deflectBladeAngle);
        Vector2 candidateB = DirectionFromAngle(baseAngle - _deflectBladeAngle);

        float scoreA = Vector2.Dot(candidateA, desiredPushDir);
        float scoreB = Vector2.Dot(candidateB, desiredPushDir);

        return scoreA >= scoreB ? candidateA : candidateB;
    }

    private SwordMoveAction DirectionToMoveAction(Vector2 direction)
    {
        if (direction.magnitude < _moveDeadZone)
            return SwordMoveAction.None;

        direction.Normalize();

        float x = direction.x;
        float y = direction.y;

        bool right = x > 0.35f;
        bool left = x < -0.35f;
        bool up = y > 0.35f;
        bool down = y < -0.35f;

        if (up && right) return SwordMoveAction.UpRight;
        if (up && left) return SwordMoveAction.UpLeft;
        if (down && right) return SwordMoveAction.DownRight;
        if (down && left) return SwordMoveAction.DownLeft;

        if (up) return SwordMoveAction.Up;
        if (down) return SwordMoveAction.Down;
        if (right) return SwordMoveAction.Right;
        if (left) return SwordMoveAction.Left;

        return SwordMoveAction.None;
    }

    private SwordRotateAction DirectionToRotateAction(Vector2 desiredSwordDir)
    {
        if (desiredSwordDir.sqrMagnitude < 0.0001f)
            return SwordRotateAction.None;

        desiredSwordDir.Normalize();

        float signedAngle = Vector2.SignedAngle(_context.MySwordDir, desiredSwordDir);

        if (Mathf.Abs(signedAngle) <= _rotationDeadZone)
            return SwordRotateAction.None;

        return signedAngle > 0f
            ? SwordRotateAction.CounterClockwise
            : SwordRotateAction.Clockwise;
    }

    private void BeginAttack()
    {
        _attackStartTipDistance = _context.MyThreatDistance;
        SetState(EnemyState.Attack);
    }

    private void SetState(EnemyState newState)
    {
        if (_currentState == newState)
            return;

        _currentState = newState;
        _stateTimer = 0f;

        switch (_currentState)
        {
            case EnemyState.Defend:
                SetColor(_defendColor);
                break;

            case EnemyState.Pressure:
                SetColor(_pressureColor);
                break;

            case EnemyState.Attack:
                SetColor(_attackColor);
                break;

            case EnemyState.Recover:
                SetColor(_recoverColor);
                break;
        }
    }

    private void SetColor(Color color)
    {
        if (_swordRenderer != null)
            _swordRenderer.material.color = color;
    }

    private float GetSwordLength()
    {
        return Mathf.Max(0.1f, Vector2.Distance(_context.MyHandlePos, _context.MyTipPos));
    }

    private Vector2 SafeNormalize(Vector2 value, Vector2 fallback)
    {
        if (value.sqrMagnitude < 0.0001f)
        {
            if (fallback.sqrMagnitude < 0.0001f)
                return Vector2.right;

            return fallback.normalized;
        }

        return value.normalized;
    }

    private Vector2 DirectionFromAngle(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRegisterCharacterHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryRegisterCharacterHit(other);
    }

    private void TryRegisterCharacterHit(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (Time.time - _lastHitTime < _hitCooldown)
            return;

        if (!IsBladeOrTipTouchingCharacter(other))
            return;

        _lastHitTime = Time.time;
        RegisterHit();
    }

    private bool IsBladeOrTipTouchingCharacter(Collider2D characterCollider)
    {
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();

        foreach (Collider2D col in myColliders)
        {
            if (col == null) continue;

            bool isBladeOrTip =
                col.CompareTag("BladeZone") ||
                col.CompareTag("TipZone");

            if (!isBladeOrTip)
                continue;

            if (col.IsTouching(characterCollider))
                return true;
        }

        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("PlayerSword"))
            return;

        if (Time.time - _lastCollisionTime < _collisionCooldown)
            return;

        _lastCollisionTime = Time.time;

        if (_currentState == EnemyState.Attack && _stateTimer >= _attackCommitTime * 0.5f)
            SetState(EnemyState.Recover);
    }

    public void RegisterHit()
    {
        FlashColor(_hitColor, 0.2f);
        OnHit?.Invoke();
    }

    public void GotHit()
    {
        FlashColor(_hitColor, 0.3f);
        OnGotHit?.Invoke();
        SetState(EnemyState.Defend);
    }

    public void ResetState()
    {
        _currentState = EnemyState.Defend;
        _stateTimer = 0f;
        _attackStartTipDistance = 0f;
        _lastCollisionTime = 0f;
        _lastHitTime = 0f;

        if (_mySpawnPoint != null)
            transform.position = _mySpawnPoint.position;

        transform.rotation = Quaternion.identity;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        if (_executor != null)
            _executor.ResetExecutor();

        SetColor(_defendColor);
    }

    private void FlashColor(Color color, float duration)
    {
        if (_swordRenderer == null)
            return;

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
            case EnemyState.Defend:
                _swordRenderer.material.color = _defendColor;
                break;

            case EnemyState.Pressure:
                _swordRenderer.material.color = _pressureColor;
                break;

            case EnemyState.Attack:
                _swordRenderer.material.color = _attackColor;
                break;

            case EnemyState.Recover:
                _swordRenderer.material.color = _recoverColor;
                break;

            default:
                _swordRenderer.material.color = oldColor;
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_context == null)
            _context = GetComponent<SwordCombatContext>();

        if (_context == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(_context.OpponentAttackTargetPos, 0.12f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(_context.MyTipPos, _context.OpponentAttackTargetPos);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_context.OpponentTipPos, _context.MyCharacterPos);

        GetDeflectPoseSafeForGizmos(out Vector2 deflectHandle, out Vector2 deflectDir);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(deflectHandle, 0.1f);
        Gizmos.DrawRay(deflectHandle, deflectDir * GetSwordLengthSafeForGizmos());
    }

    private void GetDeflectPoseSafeForGizmos(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir)
    {
        if (_context == null)
        {
            desiredHandlePos = transform.position;
            desiredSwordDir = Vector2.right;
            return;
        }

        Vector2 opponentHandle = _context.OpponentHandlePos;
        Vector2 opponentTip = _context.OpponentTipPos;
        Vector2 opponentDir = _context.OpponentSwordDir;

        Vector2 normalA = new Vector2(-opponentDir.y, opponentDir.x);
        Vector2 normalB = -normalA;

        Vector2 opponentSwordMid = (opponentHandle + opponentTip) * 0.5f;
        Vector2 toMyCharacter = _context.MyCharacterPos - opponentSwordMid;

        if (toMyCharacter.sqrMagnitude < 0.0001f)
            toMyCharacter = Vector2.left;

        toMyCharacter.Normalize();

        Vector2 chosenNormal =
            Vector2.Dot(normalA, toMyCharacter) > Vector2.Dot(normalB, toMyCharacter)
                ? normalA
                : normalB;

        Vector2 contactPoint = Vector2.Lerp(opponentHandle, opponentTip, _deflectContactT);
        contactPoint += chosenNormal * _deflectSideOffset;

        Vector2 pushAwayFromMyChar = contactPoint - _context.MyCharacterPos;
        if (pushAwayFromMyChar.sqrMagnitude < 0.0001f)
            pushAwayFromMyChar = Vector2.right;

        pushAwayFromMyChar.Normalize();

        desiredSwordDir = ChooseBladeCrossDirection(opponentDir, pushAwayFromMyChar);
        desiredHandlePos = contactPoint - desiredSwordDir * (GetSwordLengthSafeForGizmos() * 0.55f);
        desiredHandlePos = _context.ClampPointToArena(desiredHandlePos);
    }

    private float GetSwordLengthSafeForGizmos()
    {
        if (_context == null)
            return 1.5f;

        return Mathf.Max(0.1f, Vector2.Distance(_context.MyHandlePos, _context.MyTipPos));
    }
}