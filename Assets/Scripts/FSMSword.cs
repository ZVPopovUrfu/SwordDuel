using UnityEngine;
using System.Collections;
using static SwordActionTypes;

public class FSMSword : MonoBehaviour
{
    private enum EnemyState
    {
        Opening,
        Defend,
        ContactControl,
        Breakthrough,
        Attack,
        Recover
    }

    private enum ContactMode
    {
        Defense,
        AttackClear
    }

    private enum OpeningPlan
    {
        Straight,
        Upper,
        Lower
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
    [SerializeField] private float _attackEnterAdvantage = 0.10f;
    [SerializeField] private float _attackExitAdvantage = -0.25f;
    [SerializeField] private float _attackTieMargin = 0.08f;

    [Header("Opening")]
    [SerializeField] private bool _useOpeningVariation = true;

    [Tooltip("—колько секунд FSM принудительно выполн€ет стартовый манЄвр.")]
    [SerializeField] private float _openingDuration = 1.75f;

    [Tooltip("ћинимальное врем€, в течение которого Opening не отмен€етс€ даже при близости мечей.")]
    [SerializeField] private float _openingMinDuration = 0.45f;

    [Tooltip("Ќасколько сильно верхний/нижний заход смещает цель.")]
    [SerializeField] private float _openingSideOffset = 1.6f;

    [Range(0f, 1f)]
    [SerializeField] private float _openingStraightChance = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float _openingUpperChance = 0.5f;

    [Tooltip("≈сли угроза ближе этого рассто€ни€ после минимального времени Opening, Opening досрочно отмен€етс€.")]
    [SerializeField] private float _openingCancelDangerRadius = 1.4f;

    [Tooltip("≈сли мечи слишком близко после минимального времени Opening, Opening досрочно отмен€етс€.")]
    [SerializeField] private float _openingCancelSwordDistance = 1.1f;

    [Header("Contact Control")]
    [SerializeField] private bool _useContactControl = true;
    [SerializeField] private float _contactControlRadius = 2.4f;
    [SerializeField] private float _contactDangerRadius = 2.8f;

    [Range(0.2f, 0.95f)]
    [SerializeField] private float _bladeContactT = 0.62f;

    [SerializeField] private float _handleTargetMaxDistance = 2.1f;
    [SerializeField] private float _tipTargetMaxDistance = 2.6f;
    [SerializeField] private float _contactStrikeThroughOffset = 0.22f;
    [SerializeField] private float _attackClearSideOffset = 0.85f;
    [SerializeField] private float _contactSuccessAttackWindow = 0.20f;
    [SerializeField] private float _contactControlMaxDuration = 0.65f;

    [Header("Attack")]
    [SerializeField] private float _attackCommitTime = 0.35f;
    [SerializeField] private float _attackMaxDuration = 1.8f;
    [SerializeField] private float _attackSuccessDistance = 0.25f;
    [SerializeField] private float _attackHandleDistance = 0.12f;

    [Header("Attack Obstacle Avoidance")]
    [SerializeField] private float _attackLineBlockDistance = 0.75f;
    [SerializeField] private float _attackFlankOffset = 0.75f;

    [Header("Defend")]
    [SerializeField] private float _deflectContactT = 0.72f;
    [SerializeField] private float _deflectSideOffset = 0.35f;
    [SerializeField] private float _deflectBladeAngle = 65f;

    [Header("Breakthrough")]
    [SerializeField] private float _breakthroughMaxDuration = 1.2f;
    [SerializeField] private float _breakthroughContactT = 0.78f;
    [SerializeField] private float _breakthroughSideOffset = 0.45f;
    [SerializeField] private float _breakthroughTowardTargetWeight = 0.85f;

    [Header("Recover")]
    [SerializeField] private float _recoverDuration = 0.20f;

    [Header("Discrete Action Tuning")]
    [SerializeField] private float _moveDeadZone = 0.03f;
    [SerializeField] private float _rotationDeadZone = 4f;

    [Header("Hit Detection")]
    [SerializeField] private float _hitCooldown = 0.15f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer _swordRenderer;
    [SerializeField] private Color _openingColor = new Color(0.35f, 1f, 0.35f);
    [SerializeField] private Color _defendColor = Color.yellow;
    [SerializeField] private Color _contactControlColor = new Color(0.1f, 0.8f, 1f);
    [SerializeField] private Color _breakthroughColor = Color.cyan;
    [SerializeField] private Color _attackColor = Color.red;
    [SerializeField] private Color _recoverColor = Color.gray;
    [SerializeField] private Color _hitColor = Color.magenta;

    private EnemyState _currentState = EnemyState.Opening;
    private OpeningPlan _openingPlan = OpeningPlan.Straight;

    private float _stateTimer;
    private float _roundStartTime;
    private float _attackStartTipDistance;
    private float _lastCollisionTime;
    private float _lastHitTime;
    private float _lastSuccessfulContactTime = -999f;

    private readonly float _collisionCooldown = 0.12f;
    private Coroutine _flashCoroutine;

    public System.Action OnHit;
    public System.Action OnGotHit;

    public string CurrentStateName => _currentState.ToString();
    public string CurrentOpeningPlanName => _openingPlan.ToString();

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

        float swordDistance = Vector2.Distance(_context.MySwordPos, _context.OpponentSwordPos);

        bool canAttackByRace = myThreat <= opponentThreat + _attackTieMargin;
        bool hasClearAttackAdvantage = advantage > _attackEnterAdvantage;
        bool opponentDangerous = opponentThreat <= _dangerRadius;
        bool swordsAreClose = swordDistance <= _pressureRadius;
        bool recentlyWonContact = Time.time - _lastSuccessfulContactTime <= _contactSuccessAttackWindow;

        bool attackLineBlocked = IsOpponentSwordBlockingAttackLine(out _);
        bool shouldContactControl = ShouldEnterContactControl(
            canAttackByRace,
            hasClearAttackAdvantage,
            opponentThreat,
            swordDistance,
            attackLineBlocked
        );

        switch (_currentState)
        {
            case EnemyState.Opening:
                {
                    if (ShouldCancelOpening())
                    {
                        SetState(EnemyState.ContactControl);
                        return;
                    }

                    if (_stateTimer >= _openingDuration)
                    {
                        if (shouldContactControl)
                            SetState(EnemyState.ContactControl);
                        else
                            BeginAttack();

                        return;
                    }

                    break;
                }

            case EnemyState.Defend:
                {
                    if (recentlyWonContact)
                    {
                        BeginAttack();
                        return;
                    }

                    if (shouldContactControl)
                    {
                        SetState(EnemyState.ContactControl);
                        return;
                    }

                    if ((canAttackByRace || hasClearAttackAdvantage) && !opponentDangerous && !attackLineBlocked)
                    {
                        BeginAttack();
                        return;
                    }

                    if (opponentThreat <= _pressureRadius || swordsAreClose)
                    {
                        SetState(EnemyState.ContactControl);
                        return;
                    }

                    break;
                }

            case EnemyState.ContactControl:
                {
                    if (recentlyWonContact)
                    {
                        BeginAttack();
                        return;
                    }

                    if (_stateTimer > _contactControlMaxDuration)
                    {
                        if (canAttackByRace || hasClearAttackAdvantage)
                        {
                            BeginAttack();
                        }
                        else if (opponentDangerous || swordsAreClose)
                        {
                            SetState(EnemyState.ContactControl);
                        }
                        else
                        {
                            SetState(EnemyState.Defend);
                        }

                        return;
                    }

                    if (!shouldContactControl && (canAttackByRace || hasClearAttackAdvantage) && !attackLineBlocked)
                    {
                        BeginAttack();
                        return;
                    }

                    break;
                }

            case EnemyState.Breakthrough:
                {
                    if (recentlyWonContact)
                    {
                        BeginAttack();
                        return;
                    }

                    if (shouldContactControl)
                    {
                        SetState(EnemyState.ContactControl);
                        return;
                    }

                    if (canAttackByRace || hasClearAttackAdvantage)
                    {
                        BeginAttack();
                        return;
                    }

                    if (_stateTimer > _breakthroughMaxDuration)
                    {
                        if (opponentDangerous || swordsAreClose)
                            SetState(EnemyState.ContactControl);
                        else
                            BeginAttack();

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
                    bool lostAdvantageHard = advantage < _attackExitAdvantage && !canAttackByRace && !recentlyWonContact;

                    bool nearWallAndNotClose =
                        _context.IsNearArenaEdge(_context.MySwordPos, 0.55f) &&
                        currentTipDistance > 0.45f;

                    if (reachedTarget)
                        return;

                    if (shouldContactControl && !recentlyWonContact)
                    {
                        SetState(EnemyState.ContactControl);
                        return;
                    }

                    if (attackLineBlocked && swordDistance <= _pressureRadius)
                    {
                        SetState(EnemyState.ContactControl);
                        return;
                    }

                    if (attackTooLong)
                    {
                        if (swordsAreClose || attackLineBlocked)
                            SetState(EnemyState.ContactControl);
                        else
                            SetState(EnemyState.Recover);

                        return;
                    }

                    if (lostAdvantageHard || nearWallAndNotClose)
                    {
                        if (opponentDangerous || swordsAreClose)
                            SetState(EnemyState.ContactControl);
                        else
                            SetState(EnemyState.Recover);

                        return;
                    }

                    break;
                }

            case EnemyState.Recover:
                {
                    if (_stateTimer >= _recoverDuration)
                    {
                        if (shouldContactControl)
                            SetState(EnemyState.ContactControl);
                        else if (opponentThreat <= _pressureRadius || swordsAreClose)
                            SetState(EnemyState.ContactControl);
                        else
                            SetState(EnemyState.Defend);

                        return;
                    }

                    break;
                }
        }
    }

    private bool ShouldCancelOpening()
    {
        if (_stateTimer < _openingMinDuration)
            return false;

        float opponentThreat = _context.OpponentThreatDistance;
        float swordDistance = Vector2.Distance(_context.MySwordPos, _context.OpponentSwordPos);

        bool opponentVeryDangerous = opponentThreat <= _openingCancelDangerRadius;
        bool swordsVeryClose = swordDistance <= _openingCancelSwordDistance;

        return opponentVeryDangerous || swordsVeryClose;
    }

    private bool ShouldEnterContactControl(
        bool canAttackByRace,
        bool hasClearAttackAdvantage,
        float opponentThreat,
        float swordDistance,
        bool attackLineBlocked)
    {
        if (!_useContactControl)
            return false;

        bool closeToEnemySword = swordDistance <= _contactControlRadius;
        bool opponentThreatening = opponentThreat <= _contactDangerRadius;
        bool opponentCanRace = opponentThreat <= _context.MyThreatDistance + _attackTieMargin + 0.25f;

        if (opponentThreatening)
            return true;

        if (attackLineBlocked)
            return true;

        if (closeToEnemySword && opponentCanRace)
            return true;

        if (closeToEnemySword && !hasClearAttackAdvantage)
            return true;

        return false;
    }

    private SwordAction ChooseActionByState()
    {
        switch (_currentState)
        {
            case EnemyState.Opening:
                return ChooseOpeningAction();

            case EnemyState.Attack:
                return ChooseAttackAction();

            case EnemyState.ContactControl:
                return ChooseContactControlAction();

            case EnemyState.Breakthrough:
                return ChooseBreakthroughAction();

            case EnemyState.Recover:
                return ChooseRecoverAction();

            case EnemyState.Defend:
            default:
                return ChooseDefendAction();
        }
    }

    private SwordAction ChooseOpeningAction()
    {
        Vector2 target = GetOpeningTarget();

        Vector2 desiredSwordDir = SafeNormalize(target - _context.MyHandlePos, _context.MySwordDir);

        Vector2 desiredHandlePos = target - desiredSwordDir * GetSwordLength();
        desiredHandlePos = _context.ClampPointToArena(desiredHandlePos);

        Vector2 moveToHandle = desiredHandlePos - _context.MySwordPos;
        Vector2 moveToTarget = target - _context.MyTipPos;

        Vector2 moveVector =
            (SafeNormalize(moveToTarget, desiredSwordDir) * 0.75f +
             SafeNormalize(moveToHandle, desiredSwordDir) * 0.25f).normalized;

        SwordMoveAction move = DirectionToMoveAction(moveVector);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private SwordAction ChooseContactControlAction()
    {
        ContactMode mode = DetermineContactMode();

        Vector2 desiredPushDir = mode == ContactMode.Defense
            ? GetDefensePushDirection()
            : GetAttackClearPushDirection();

        Vector2 targetPoint;
        float myContactT;
        Vector2 desiredSwordDir;

        ChooseBestContactTarget(mode, desiredPushDir, out targetPoint, out myContactT, out desiredSwordDir);

        Vector2 strikeTarget = targetPoint + desiredPushDir * _contactStrikeThroughOffset;

        Vector2 desiredHandlePos = strikeTarget - desiredSwordDir * (GetSwordLength() * myContactT);
        desiredHandlePos = _context.ClampPointToArena(desiredHandlePos);

        Vector2 moveVector = desiredHandlePos - _context.MySwordPos;

        if (moveVector.magnitude < _moveDeadZone * 2f)
        {
            moveVector = strikeTarget - GetMyContactPoint(myContactT);
        }

        SwordMoveAction move = DirectionToMoveAction(moveVector);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private ContactMode DetermineContactMode()
    {
        bool opponentDangerous =
            _context.OpponentThreatDistance <= _contactDangerRadius ||
            _context.ThreatAdvantage < -_attackTieMargin;

        if (opponentDangerous)
            return ContactMode.Defense;

        return ContactMode.AttackClear;
    }

    private Vector2 GetDefensePushDirection()
    {
        Vector2 pushDir = _context.OpponentSwordPos - _context.MyCharacterPos;

        if (pushDir.sqrMagnitude < 0.0001f)
            pushDir = _context.OpponentTipPos - _context.MyCharacterPos;

        return SafeNormalize(pushDir, Vector2.right);
    }

    private Vector2 GetAttackClearPushDirection()
    {
        Vector2 attackLine = GetCurrentAttackTarget() - _context.MyTipPos;

        if (attackLine.sqrMagnitude < 0.0001f)
            attackLine = _context.OpponentCharacterPos - _context.MySwordPos;

        attackLine = SafeNormalize(attackLine, _context.MySwordDir);

        Vector2 sideA = new Vector2(-attackLine.y, attackLine.x);
        Vector2 sideB = -sideA;

        Vector2 opponentFromLine = _context.OpponentSwordPos - _context.MyTipPos;

        float scoreA = Vector2.Dot(sideA, opponentFromLine);
        float scoreB = Vector2.Dot(sideB, opponentFromLine);

        Vector2 chosenSide = scoreA >= scoreB ? sideA : sideB;

        return SafeNormalize(chosenSide * _attackClearSideOffset + attackLine * 0.15f, chosenSide);
    }

    private void ChooseBestContactTarget(
        ContactMode mode,
        Vector2 desiredPushDir,
        out Vector2 targetPoint,
        out float myContactT,
        out Vector2 desiredSwordDir)
    {
        Vector2 handle = _context.OpponentHandlePos;
        Vector2 tip = _context.OpponentTipPos;
        Vector2 bladeMid = Vector2.Lerp(_context.OpponentHandlePos, _context.OpponentTipPos, 0.55f);

        float handleDistance = Vector2.Distance(GetMyContactPoint(_bladeContactT), handle);
        float tipDistance = Vector2.Distance(GetMyContactPoint(_bladeContactT), tip);

        bool handleAvailable =
            handleDistance <= _handleTargetMaxDistance ||
            Vector2.Distance(_context.MyTipPos, handle) <= _handleTargetMaxDistance;

        bool tipAvailable =
            tipDistance <= _tipTargetMaxDistance ||
            Vector2.Distance(_context.MySwordPos, tip) <= _tipTargetMaxDistance;

        if (handleAvailable)
        {
            targetPoint = handle;
            myContactT = _bladeContactT;
            desiredSwordDir = ChooseSwordDirectionForContact(targetPoint, myContactT, desiredPushDir, preferCross: false);
            return;
        }

        if (tipAvailable)
        {
            targetPoint = tip;
            myContactT = _bladeContactT;
            desiredSwordDir = ChooseBladeCrossDirection(_context.OpponentSwordDir, desiredPushDir);
            return;
        }

        targetPoint = bladeMid;
        myContactT = _bladeContactT;
        desiredSwordDir = ChooseBladeCrossDirection(_context.OpponentSwordDir, desiredPushDir);
    }

    private Vector2 ChooseSwordDirectionForContact(Vector2 targetPoint, float contactT, Vector2 desiredPushDir, bool preferCross)
    {
        if (preferCross)
            return ChooseBladeCrossDirection(_context.OpponentSwordDir, desiredPushDir);

        Vector2 toTargetFromHandle = targetPoint - _context.MyHandlePos;

        if (toTargetFromHandle.sqrMagnitude < 0.0001f)
            return _context.MySwordDir;

        Vector2 directDir = toTargetFromHandle.normalized;
        Vector2 crossDir = ChooseBladeCrossDirection(_context.OpponentSwordDir, desiredPushDir);

        float directPushScore = Vector2.Dot(directDir, desiredPushDir);
        float crossPushScore = Vector2.Dot(crossDir, desiredPushDir);

        if (crossPushScore > directPushScore + 0.25f)
            return crossDir;

        return directDir;
    }

    private Vector2 GetMyContactPoint(float t)
    {
        return Vector2.Lerp(_context.MyHandlePos, _context.MyTipPos, Mathf.Clamp01(t));
    }

    private SwordAction ChooseAttackAction()
    {
        Vector2 target = GetCurrentAttackTarget();

        Vector2 toTargetFromHandle = target - _context.MyHandlePos;
        Vector2 toTargetFromTip = target - _context.MyTipPos;

        Vector2 desiredSwordDir = SafeNormalize(toTargetFromHandle, _context.MySwordDir);

        Vector2 moveTarget = target;

        if (IsOpponentSwordBlockingAttackLine(out Vector2 blockPoint) && toTargetFromTip.magnitude > _attackSuccessDistance)
        {
            Vector2 attackLine = SafeNormalize(target - _context.MyTipPos, desiredSwordDir);
            Vector2 normalA = new Vector2(-attackLine.y, attackLine.x);
            Vector2 normalB = -normalA;

            Vector2 opponentCharacterDir = SafeNormalize(_context.OpponentCharacterPos - blockPoint, attackLine);

            Vector2 flankA = blockPoint + normalA * _attackFlankOffset + opponentCharacterDir * 0.35f;
            Vector2 flankB = blockPoint + normalB * _attackFlankOffset + opponentCharacterDir * 0.35f;

            float scoreA =
                -Vector2.Distance(flankA, target) +
                Vector2.Dot(SafeNormalize(flankA - _context.MySwordPos, attackLine), opponentCharacterDir) * 0.5f;

            float scoreB =
                -Vector2.Distance(flankB, target) +
                Vector2.Dot(SafeNormalize(flankB - _context.MySwordPos, attackLine), opponentCharacterDir) * 0.5f;

            moveTarget = scoreA >= scoreB ? flankA : flankB;
            moveTarget = _context.ClampPointToArena(moveTarget);

            desiredSwordDir = SafeNormalize(target - _context.MyHandlePos, _context.MySwordDir);
        }

        Vector2 desiredHandlePos = target - desiredSwordDir * GetSwordLength();
        desiredHandlePos = _context.ClampPointToArena(desiredHandlePos);

        Vector2 moveToIdealHandle = desiredHandlePos - _context.MySwordPos;
        Vector2 moveToTarget = moveTarget - _context.MyTipPos;

        float tipDistance = toTargetFromTip.magnitude;
        float handleDistance = moveToIdealHandle.magnitude;

        Vector2 moveVector;

        if (tipDistance > _attackSuccessDistance)
        {
            Vector2 tipChase = SafeNormalize(moveToTarget, desiredSwordDir);

            if (handleDistance > _attackHandleDistance)
            {
                Vector2 handleChase = SafeNormalize(moveToIdealHandle, tipChase);
                moveVector = (tipChase * 0.8f + handleChase * 0.2f).normalized;
            }
            else
            {
                moveVector = tipChase;
            }
        }
        else
        {
            moveVector = desiredSwordDir;
        }

        SwordMoveAction move = DirectionToMoveAction(moveVector);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private Vector2 GetOpeningTarget()
    {
        Vector2 target = _context.OpponentAttackTargetPos;

        if (!_useOpeningVariation)
            return target;

        if (_openingPlan == OpeningPlan.Straight)
            return target;

        Vector2 attackDir = target - _context.MyTipPos;

        if (attackDir.sqrMagnitude < 0.0001f)
            return target;

        attackDir.Normalize();

        Vector2 side = new Vector2(-attackDir.y, attackDir.x);

        if (_openingPlan == OpeningPlan.Lower)
            side = -side;

        Vector2 offsetTarget = target + side * _openingSideOffset;
        return _context.ClampPointToArena(offsetTarget);
    }

    private Vector2 GetCurrentAttackTarget()
    {
        if (_currentState == EnemyState.Opening)
            return GetOpeningTarget();

        return _context.OpponentAttackTargetPos;
    }

    private SwordAction ChooseDefendAction()
    {
        GetDeflectPose(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir);

        SwordMoveAction move = DirectionToMoveAction(desiredHandlePos - _context.MySwordPos);
        SwordRotateAction rotate = DirectionToRotateAction(desiredSwordDir);

        return new SwordAction(move, rotate);
    }

    private SwordAction ChooseBreakthroughAction()
    {
        GetBreakthroughPose(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir);

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

    private void GetBreakthroughPose(out Vector2 desiredHandlePos, out Vector2 desiredSwordDir)
    {
        Vector2 opponentHandle = _context.OpponentHandlePos;
        Vector2 opponentTip = _context.OpponentTipPos;
        Vector2 opponentDir = _context.OpponentSwordDir;

        Vector2 contactPoint = Vector2.Lerp(opponentHandle, opponentTip, _breakthroughContactT);

        Vector2 pushAwayFromMyChar = contactPoint - _context.MyCharacterPos;

        if (pushAwayFromMyChar.sqrMagnitude < 0.0001f)
            pushAwayFromMyChar = Vector2.right;

        pushAwayFromMyChar.Normalize();

        Vector2 towardTarget = GetCurrentAttackTarget() - _context.MyTipPos;

        if (towardTarget.sqrMagnitude < 0.0001f)
            towardTarget = _context.OpponentCharacterPos - _context.MySwordPos;

        towardTarget = SafeNormalize(towardTarget, _context.MySwordDir);

        Vector2 desiredPush =
            (pushAwayFromMyChar + towardTarget * _breakthroughTowardTargetWeight).normalized;

        Vector2 normalA = new Vector2(-opponentDir.y, opponentDir.x);
        Vector2 normalB = -normalA;

        Vector2 side =
            Vector2.Dot(normalA, desiredPush) >= Vector2.Dot(normalB, desiredPush)
                ? normalA
                : normalB;

        contactPoint += side * _breakthroughSideOffset;

        desiredSwordDir = ChooseBladeCrossDirection(opponentDir, desiredPush);

        desiredHandlePos = contactPoint - desiredSwordDir * (GetSwordLength() * 0.52f);
        desiredHandlePos += towardTarget * 0.25f;

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

    private bool IsOpponentSwordBlockingAttackLine(out Vector2 blockPoint)
    {
        Vector2 start = _context.MyTipPos;
        Vector2 end = GetCurrentAttackTarget();
        Vector2 line = end - start;

        blockPoint = _context.OpponentSwordPos;

        if (line.sqrMagnitude < 0.0001f)
            return false;

        Vector2[] samples =
        {
            _context.OpponentHandlePos,
            Vector2.Lerp(_context.OpponentHandlePos, _context.OpponentTipPos, 0.35f),
            Vector2.Lerp(_context.OpponentHandlePos, _context.OpponentTipPos, 0.65f),
            _context.OpponentTipPos
        };

        float bestDistance = float.MaxValue;
        Vector2 bestPoint = samples[0];

        foreach (Vector2 sample in samples)
        {
            float t = Vector2.Dot(sample - start, line) / line.sqrMagnitude;

            if (t < 0.05f || t > 0.95f)
                continue;

            Vector2 closest = start + line * t;
            float distance = Vector2.Distance(sample, closest);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = sample;
            }
        }

        blockPoint = bestPoint;
        return bestDistance <= _attackLineBlockDistance;
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
            case EnemyState.Opening:
                SetColor(_openingColor);
                break;

            case EnemyState.Defend:
                SetColor(_defendColor);
                break;

            case EnemyState.ContactControl:
                SetColor(_contactControlColor);
                break;

            case EnemyState.Breakthrough:
                SetColor(_breakthroughColor);
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
        if (!IsOpponentCharacterCollider(other))
            return;

        if (Time.time - _lastHitTime < _hitCooldown)
            return;

        if (!IsBladeOrTipTouchingCharacter(other))
            return;

        _lastHitTime = Time.time;
        RegisterHit();
    }

    private bool IsOpponentCharacterCollider(Collider2D col)
    {
        if (col == null)
            return false;

        if (gameObject.CompareTag("PlayerSword"))
            return col.CompareTag("Enemy");

        if (gameObject.CompareTag("EnemySword"))
            return col.CompareTag("Player");

        return col.CompareTag("Player") || col.CompareTag("Enemy");
    }

    private bool IsOpponentSwordCollision(Collision2D collision)
    {
        if (collision == null)
            return false;

        GameObject obj = collision.gameObject;

        if (IsOpponentSwordObject(obj))
            return true;

        if (collision.rigidbody != null && IsOpponentSwordObject(collision.rigidbody.gameObject))
            return true;

        Transform parent = obj.transform.parent;

        while (parent != null)
        {
            if (IsOpponentSwordObject(parent.gameObject))
                return true;

            parent = parent.parent;
        }

        return false;
    }

    private bool IsOpponentSwordObject(GameObject obj)
    {
        if (obj == null)
            return false;

        if (gameObject.CompareTag("PlayerSword"))
            return obj.CompareTag("EnemySword");

        if (gameObject.CompareTag("EnemySword"))
            return obj.CompareTag("PlayerSword");

        return obj.CompareTag("PlayerSword") || obj.CompareTag("EnemySword");
    }

    private SwordPhysics GetOtherSwordPhysics(Collision2D collision)
    {
        if (collision == null)
            return null;

        if (collision.rigidbody != null)
        {
            SwordPhysics physicsFromRb = collision.rigidbody.GetComponent<SwordPhysics>();

            if (physicsFromRb != null)
                return physicsFromRb;
        }

        if (collision.collider != null)
        {
            SwordPhysics physicsFromCollider = collision.collider.GetComponentInParent<SwordPhysics>();

            if (physicsFromCollider != null)
                return physicsFromCollider;
        }

        return collision.gameObject.GetComponentInParent<SwordPhysics>();
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
        if (!IsOpponentSwordCollision(collision))
            return;

        if (Time.time - _lastCollisionTime < _collisionCooldown)
            return;

        _lastCollisionTime = Time.time;

        if (collision.contactCount <= 0)
            return;

        SwordPhysics otherPhysics = GetOtherSwordPhysics(collision);

        if (_swordPhysics != null && otherPhysics != null)
        {
            ContactPoint2D contact = collision.GetContact(0);
            Vector2 contactPoint = contact.point;

            string myPart = _swordPhysics.GetCollisionPartString(contactPoint);
            string otherPart = otherPhysics.GetCollisionPartString(contactPoint);

            bool successfulContact =
                (myPart == "Blade" && otherPart == "Tip") ||
                (myPart == "Blade" && otherPart == "Handle") ||
                (myPart == "Tip" && otherPart == "Handle") ||
                (myPart == "Blade" && otherPart == "Blade");

            if (successfulContact)
            {
                _lastSuccessfulContactTime = Time.time;
                BeginAttack();
                return;
            }
        }

        if (_currentState == EnemyState.Attack && _stateTimer >= _attackCommitTime * 0.5f)
        {
            SetState(EnemyState.ContactControl);
            return;
        }

        if (_currentState == EnemyState.ContactControl)
        {
            return;
        }
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
        _currentState = EnemyState.Opening;
        _stateTimer = 0f;
        _roundStartTime = Time.time;
        _attackStartTipDistance = 0f;
        _lastCollisionTime = 0f;
        _lastHitTime = 0f;
        _lastSuccessfulContactTime = -999f;

        ChooseOpeningPlan();

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

        SetColor(_openingColor);
    }

    private void ChooseOpeningPlan()
    {
        if (!_useOpeningVariation)
        {
            _openingPlan = OpeningPlan.Straight;
            return;
        }

        float straightChance = Mathf.Clamp01(_openingStraightChance);
        float upperChance = Mathf.Clamp01(_openingUpperChance);

        if (straightChance + upperChance > 1f)
            upperChance = 1f - straightChance;

        float roll = Random.value;

        if (roll < straightChance)
            _openingPlan = OpeningPlan.Straight;
        else if (roll < straightChance + upperChance)
            _openingPlan = OpeningPlan.Upper;
        else
            _openingPlan = OpeningPlan.Lower;
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
            case EnemyState.Opening:
                _swordRenderer.material.color = _openingColor;
                break;

            case EnemyState.Defend:
                _swordRenderer.material.color = _defendColor;
                break;

            case EnemyState.ContactControl:
                _swordRenderer.material.color = _contactControlColor;
                break;

            case EnemyState.Breakthrough:
                _swordRenderer.material.color = _breakthroughColor;
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
        Gizmos.DrawLine(_context.MyTipPos, GetCurrentAttackTarget());

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_context.OpponentTipPos, _context.MyCharacterPos);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(_context.OpponentHandlePos, 0.11f);

        if (IsOpponentSwordBlockingAttackLine(out Vector2 blockPoint))
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(blockPoint, 0.12f);
        }
    }
}