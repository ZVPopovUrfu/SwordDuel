using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
using static SwordActionTypes;

public class MLSword : Agent
{
    [Header("Core")]
    [SerializeField] private SwordCombatContext _context;
    [SerializeField] private SwordActionExecutor _executor;
    [SerializeField] private SwordPhysics _swordPhysics;
    [SerializeField] private Rigidbody2D _rb;

    [Header("Opponent")]
    [SerializeField] private MLSword _opponentAgent;
    [SerializeField] private Transform _opponentCharacterRoot;

    [Header("Spawn")]
    [SerializeField] private Transform _mySpawnPoint;
    [SerializeField] private float _spawnRandomOffset = 0.5f;
    [SerializeField] private bool _randomizeRotation = true;

    [Header("Spawn Randomization")]
    [SerializeField] private bool _useHalfArenaRandomSpawn = true;
    [SerializeField] private bool _isLeftTeam = true;
    [SerializeField] private Transform _arenaCenter;
    [SerializeField] private float _arenaWidth = 20f;
    [SerializeField] private float _arenaHeight = 10f;
    [SerializeField] private float _arenaPadding = 1.0f;
    [SerializeField] private float _spawnMinDistanceFromCenter = 0.8f;

    [Header("Mode")]
    [Tooltip("False для ML-сцены обучения. True для TESTMLINGAME, где счет и сброс делает NewGameManager.")]
    [SerializeField] private bool _useGameManagerMode = false;

    [Header("Main Rewards")]
    [SerializeField] private float _hitReward = 3.0f;
    [SerializeField] private float _gotHitPenalty = -2.0f;
    [SerializeField] private float _timePenalty = -0.0015f;

    [Header("Defense Rewards")]
    [SerializeField] private float _dangerRadius = 2.4f;
    [SerializeField] private float _defensePositionReward = 0.002f;
    [SerializeField] private float _defenseMissPenalty = -0.006f;
    [SerializeField] private float _threatPushedAwayReward = 0.010f;

    [Header("Attack Timing Rewards")]
    [SerializeField] private float _safeAttackProgressReward = 0.014f;
    [SerializeField] private float _badAttackWhileDangerPenalty = -0.006f;
    [SerializeField] private float _badRaceAttackPenalty = -0.002f;

    [Header("Direct Attack Rewards")]
    [SerializeField] private float _directAttackMoveReward = 0.010f;
    [SerializeField] private float _directAttackAimReward = 0.006f;
    [SerializeField] private float _idleTargetApproachReward = 0.025f;
    [SerializeField] private float _closeIdleAttackPenalty = -0.015f;
    [SerializeField] private float _closeIdleDistance = 1.6f;
    [SerializeField] private float _goodAimScore = 0.65f;

    [Header("Counterattack Rewards")]
    [SerializeField] private float _counterAttackWindow = 1.5f;
    [SerializeField] private float _counterAttackProgressReward = 0.025f;
    [SerializeField] private float _counterAttackHitReward = 1.0f;
    [SerializeField] private float _passiveAfterDefensePenalty = -0.010f;

    [Header("Passivity Rewards")]
    [SerializeField] private float _safePassivityPenalty = -0.006f;
    [SerializeField] private float _safeNoThreatDistance = 2.6f;
    [SerializeField] private float _safePassiveTimePenalty = -0.004f;
    [SerializeField] private float _safePassiveGraceTime = 0.7f;

    [Header("Initiative Rewards")]
    [SerializeField] private float _initiativeDistance = 4.0f;
    [SerializeField] private float _initiativeProgressReward = 0.012f;
    [SerializeField] private float _idleOpponentAttackReward = 0.018f;
    [SerializeField] private float _idleOpponentPassivityPenalty = -0.008f;
    [SerializeField] private float _opponentIdleVelocityThreshold = 0.15f;

    [Header("Finishing Attack Rewards")]
    [SerializeField] private float _finishingDistance = 1.2f;
    [SerializeField] private float _finishingProgressReward = 0.020f;
    [SerializeField] private float _closeButNotFinishingPenalty = -0.010f;

    [Header("Anti-Pattern / Anti-Chaos Rewards")]
    [SerializeField] private float _rotationUsePenalty = -0.0002f;
    [SerializeField] private float _rotationSwitchPenalty = -0.002f;
    [SerializeField] private float _sameActionRepeatPenalty = -0.002f;
    [SerializeField] private int _sameActionRepeatLimit = 12;
    [SerializeField] private float _wallProximityPenalty = -0.002f;

    [Header("Episode")]
    [SerializeField] private float _maxEpisodeTime = 30f;

    [Header("Hit Detection")]
    [SerializeField] private float _hitCooldown = 0.15f;

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

    public System.Action OnHit;
    public System.Action OnGotHit;

    private float _episodeTimer;
    private float _lastHitTime;
    private float _lastCollisionTime;
    private readonly float _collisionCooldown = 0.12f;

    private float _previousMyThreat;
    private float _previousOpponentThreat;
    private float _lastSuccessfulDefenseTime = -999f;
    private float _safePassiveTimer = 0f;

    private SwordMoveAction _lastMoveAction = SwordMoveAction.None;
    private SwordRotateAction _lastRotateAction = SwordRotateAction.None;
    private int _sameActionCounter = 0;

    private Coroutine _flashCoroutine;

    private bool IsKnockedBack => _swordPhysics != null && _swordPhysics.IsKnockedBack();

    public override void Initialize()
    {
        if (_context == null) _context = GetComponent<SwordCombatContext>();
        if (_executor == null) _executor = GetComponent<SwordActionExecutor>();
        if (_swordPhysics == null) _swordPhysics = GetComponent<SwordPhysics>();
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        if (_swordRenderer == null) _swordRenderer = GetComponent<Renderer>();

        CurrentEpisode = 0;
        CumulativeReward = 0f;
        HitsScored = 0;
        HitsReceived = 0;
    }

    public override void OnEpisodeBegin()
    {
        CurrentEpisode++;
        CumulativeReward = 0f;

        _episodeTimer = _maxEpisodeTime;
        _lastHitTime = 0f;
        _lastCollisionTime = 0f;
        _lastSuccessfulDefenseTime = -999f;
        _safePassiveTimer = 0f;

        _lastMoveAction = SwordMoveAction.None;
        _lastRotateAction = SwordRotateAction.None;
        _sameActionCounter = 0;

        ResetTransformAndPhysics();

        if (_executor != null)
            _executor.ResetExecutor();

        if (_swordRenderer != null)
            _swordRenderer.material.color = _defaultColor;

        CacheThreatDistances();
    }

    private void ResetTransformAndPhysics()
    {
        if (_useHalfArenaRandomSpawn)
        {
            transform.position = GetRandomSpawnInOwnHalf();
        }
        else
        {
            Vector3 basePosition = _mySpawnPoint != null ? _mySpawnPoint.position : transform.position;

            float randomX = Random.Range(-_spawnRandomOffset, _spawnRandomOffset);
            float randomY = Random.Range(-_spawnRandomOffset, _spawnRandomOffset);

            transform.position = basePosition + new Vector3(randomX, randomY, 0f);
        }

        if (_randomizeRotation)
        {
            float randomZ = Random.Range(0f, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, randomZ);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
    }

    private Vector3 GetRandomSpawnInOwnHalf()
    {
        Vector2 center = _arenaCenter != null ? (Vector2)_arenaCenter.position : Vector2.zero;

        float halfW = _arenaWidth * 0.5f - _arenaPadding;
        float halfH = _arenaHeight * 0.5f - _arenaPadding;

        float xMin;
        float xMax;

        if (_isLeftTeam)
        {
            xMin = center.x - halfW;
            xMax = center.x - _spawnMinDistanceFromCenter;
        }
        else
        {
            xMin = center.x + _spawnMinDistanceFromCenter;
            xMax = center.x + halfW;
        }

        float x = Random.Range(xMin, xMax);
        float y = Random.Range(center.y - halfH, center.y + halfH);

        return new Vector3(x, y, transform.position.z);
    }

    private void CacheThreatDistances()
    {
        if (_context == null)
        {
            _previousMyThreat = 0f;
            _previousOpponentThreat = 0f;
            return;
        }

        _previousMyThreat = _context.MyThreatDistance;
        _previousOpponentThreat = _context.OpponentThreatDistance;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_context == null)
        {
            for (int i = 0; i < 29; i++)
                sensor.AddObservation(0f);

            return;
        }

        _context.AddObservationsTo(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_executor == null || _context == null)
            return;

        if (IsKnockedBack)
        {
            _executor.SetAction(new SwordAction(SwordMoveAction.None, SwordRotateAction.None));
            return;
        }

        int moveIndex = Mathf.Clamp(actions.DiscreteActions[0], 0, 8);
        int rotateIndex = Mathf.Clamp(actions.DiscreteActions[1], 0, 2);

        SwordMoveAction moveAction = (SwordMoveAction)moveIndex;
        SwordRotateAction rotateAction = (SwordRotateAction)rotateIndex;

        SwordAction action = new SwordAction(moveAction, rotateAction);
        _executor.SetAction(action);

        ApplyStepRewards(moveAction, rotateAction);
        CheckCharacterHitByBladeOrTip();

        _episodeTimer -= Time.fixedDeltaTime;

        if (!_useGameManagerMode && _episodeTimer <= 0f)
        {
            EndEpisode();
        }

        CumulativeReward = GetCumulativeReward();
    }

    private void ApplyStepRewards(SwordMoveAction moveAction, SwordRotateAction rotateAction)
    {
        AddReward(_timePenalty * Time.fixedDeltaTime);

        float currentMyThreat = _context.MyThreatDistance;
        float currentOpponentThreat = _context.OpponentThreatDistance;

        float myThreatProgress = _previousMyThreat - currentMyThreat;
        float opponentThreatProgress = _previousOpponentThreat - currentOpponentThreat;
        float opponentThreatChange = currentOpponentThreat - _previousOpponentThreat;

        bool opponentClosingIn = opponentThreatProgress > 0.005f;

        bool opponentIsAttacking =
            currentOpponentThreat <= _dangerRadius &&
            (_context.OpponentAimAtMyCharacterScore > 0.35f || opponentClosingIn);

        bool mySwordBlocksThreat = IsMySwordBetweenOpponentTipAndMyCharacter();

        bool opponentPassive =
            !opponentIsAttacking &&
            currentOpponentThreat > _dangerRadius &&
            _context.OpponentAimAtMyCharacterScore < 0.35f;

        bool safeToAttack =
            !opponentIsAttacking &&
            _context.ThreatAdvantage > -0.15f;

        Vector2 moveDir = MoveActionToDirection(moveAction);
        Vector2 toTargetFromTip = _context.OpponentAttackTargetPos - _context.MyTipPos;

        Vector2 attackDir = toTargetFromTip.sqrMagnitude > 0.0001f
            ? toTargetFromTip.normalized
            : Vector2.zero;

        float moveTowardTargetScore = Vector2.Dot(moveDir, attackDir);

        if (safeToAttack)
        {
            if (myThreatProgress > 0.004f)
                AddReward(_idleTargetApproachReward);

            if (_context.MyAimAtOpponentScore > _goodAimScore)
                AddReward(_directAttackAimReward);

            if (moveTowardTargetScore > 0.5f)
                AddReward(_directAttackMoveReward);
        }

        if (opponentPassive && currentMyThreat <= _closeIdleDistance)
        {
            if (myThreatProgress <= 0.001f && currentMyThreat > 0.25f)
                AddReward(_closeIdleAttackPenalty);
        }

        bool recentlyDefended = Time.time - _lastSuccessfulDefenseTime <= _counterAttackWindow;

        bool opponentNoLongerDangerous =
            !opponentIsAttacking &&
            _context.OpponentThreatDistance > _dangerRadius;

        if (recentlyDefended && opponentNoLongerDangerous)
        {
            if (myThreatProgress > 0.004f)
                AddReward(_counterAttackProgressReward);
            else
                AddReward(_passiveAfterDefensePenalty);
        }

        if (opponentIsAttacking)
        {
            if (mySwordBlocksThreat)
                AddReward(_defensePositionReward);
            else
                AddReward(_defenseMissPenalty);

            if (opponentThreatChange > 0.01f)
            {
                AddReward(_threatPushedAwayReward);
                _lastSuccessfulDefenseTime = Time.time;
            }

            if (!mySwordBlocksThreat && myThreatProgress > 0.004f)
                AddReward(_badAttackWhileDangerPenalty);
        }
        else
        {
            if (myThreatProgress > 0.005f)
                AddReward(_safeAttackProgressReward);
        }

        bool safeSituation =
            !opponentIsAttacking &&
            _context.OpponentThreatDistance > _safeNoThreatDistance &&
            _context.ThreatAdvantage > -0.1f;

        if (safeSituation && myThreatProgress <= 0.001f)
            AddReward(_safePassivityPenalty);

        bool safeButPassive =
            !opponentIsAttacking &&
            _context.OpponentThreatDistance > _safeNoThreatDistance &&
            myThreatProgress <= 0.001f;

        if (safeButPassive)
        {
            _safePassiveTimer += Time.fixedDeltaTime;

            if (_safePassiveTimer > _safePassiveGraceTime)
                AddReward(_safePassiveTimePenalty);
        }
        else
        {
            _safePassiveTimer = 0f;
        }

        bool opponentAppearsIdle =
            _context.OpponentVelocity.magnitude < _opponentIdleVelocityThreshold &&
            currentOpponentThreat > _dangerRadius &&
            _context.OpponentAimAtMyCharacterScore < 0.35f;

        bool iAmNotThreatened =
            !opponentIsAttacking &&
            currentOpponentThreat > _dangerRadius &&
            _context.ThreatAdvantage > -0.1f;

        bool iAmFarEnoughToNeedInitiative =
            currentMyThreat > _initiativeDistance;

        if (iAmNotThreatened && myThreatProgress > 0.005f)
            AddReward(_initiativeProgressReward);

        if (opponentAppearsIdle && myThreatProgress > 0.005f)
            AddReward(_idleOpponentAttackReward);

        if (opponentAppearsIdle && iAmFarEnoughToNeedInitiative && myThreatProgress <= 0.001f)
            AddReward(_idleOpponentPassivityPenalty);

        bool inFinishingRange =
            currentMyThreat <= _finishingDistance &&
            !opponentIsAttacking;

        if (inFinishingRange && myThreatProgress > 0.003f)
            AddReward(_finishingProgressReward);

        if (inFinishingRange && myThreatProgress <= 0.0005f && currentMyThreat > 0.25f)
            AddReward(_closeButNotFinishingPenalty);

        if (_context.ThreatAdvantage < -0.2f && myThreatProgress > 0.004f && !mySwordBlocksThreat)
            AddReward(_badRaceAttackPenalty);

        if (rotateAction != SwordRotateAction.None)
            AddReward(_rotationUsePenalty);

        if (IsOppositeRotation(_lastRotateAction, rotateAction))
            AddReward(_rotationSwitchPenalty);

        if (moveAction == _lastMoveAction && rotateAction == _lastRotateAction)
            _sameActionCounter++;
        else
            _sameActionCounter = 0;

        bool activelyAttackingSafely =
            !opponentIsAttacking &&
            myThreatProgress > 0.003f;

        if (_sameActionCounter >= _sameActionRepeatLimit && !activelyAttackingSafely)
            AddReward(_sameActionRepeatPenalty);

        if (_context.IsNearArenaEdge(_context.MySwordPos, 0.45f))
            AddReward(_wallProximityPenalty);

        _lastMoveAction = moveAction;
        _lastRotateAction = rotateAction;

        _previousMyThreat = currentMyThreat;
        _previousOpponentThreat = currentOpponentThreat;
    }

    private Vector2 MoveActionToDirection(SwordMoveAction action)
    {
        switch (action)
        {
            case SwordMoveAction.Up:
                return Vector2.up;

            case SwordMoveAction.Down:
                return Vector2.down;

            case SwordMoveAction.Left:
                return Vector2.left;

            case SwordMoveAction.Right:
                return Vector2.right;

            case SwordMoveAction.UpLeft:
                return new Vector2(-1f, 1f).normalized;

            case SwordMoveAction.UpRight:
                return new Vector2(1f, 1f).normalized;

            case SwordMoveAction.DownLeft:
                return new Vector2(-1f, -1f).normalized;

            case SwordMoveAction.DownRight:
                return new Vector2(1f, -1f).normalized;

            default:
                return Vector2.zero;
        }
    }

    private bool IsMySwordBetweenOpponentTipAndMyCharacter()
    {
        Vector2 dangerStart = _context.OpponentTipPos;
        Vector2 dangerEnd = _context.MyCharacterPos;

        Vector2 myHandle = _context.MyHandlePos;
        Vector2 myTip = _context.MyTipPos;

        Vector2 dangerLine = dangerEnd - dangerStart;

        if (dangerLine.sqrMagnitude < 0.0001f)
            return false;

        float bestDistance = float.MaxValue;
        bool hasPointBetween = false;

        const int samples = 5;

        for (int i = 0; i < samples; i++)
        {
            float tSword = i / (float)(samples - 1);
            Vector2 swordPoint = Vector2.Lerp(myHandle, myTip, tSword);

            float tDanger = Vector2.Dot(swordPoint - dangerStart, dangerLine) / dangerLine.sqrMagnitude;
            tDanger = Mathf.Clamp01(tDanger);

            Vector2 closestOnDangerLine = dangerStart + dangerLine * tDanger;
            float distance = Vector2.Distance(swordPoint, closestOnDangerLine);

            if (distance < bestDistance)
                bestDistance = distance;

            if (tDanger > 0.12f && tDanger < 0.95f)
                hasPointBetween = true;
        }

        return hasPointBetween && bestDistance < 0.75f;
    }

    private bool IsOppositeRotation(SwordRotateAction previous, SwordRotateAction current)
    {
        return
            (previous == SwordRotateAction.Clockwise && current == SwordRotateAction.CounterClockwise) ||
            (previous == SwordRotateAction.CounterClockwise && current == SwordRotateAction.Clockwise);
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
        if (Time.time - _lastHitTime < _hitCooldown)
            return;

        if (!IsOpponentCharacterCollider(other))
            return;

        if (!IsBladeOrTipTouchingCollider(other))
            return;

        _lastHitTime = Time.time;
        HitCharacter();
    }

    private void CheckCharacterHitByBladeOrTip()
    {
        if (Time.time - _lastHitTime < _hitCooldown)
            return;

        Collider2D[] opponentColliders = GetOpponentCharacterColliders();

        if (opponentColliders == null || opponentColliders.Length == 0)
            return;

        foreach (Collider2D opponentCol in opponentColliders)
        {
            if (opponentCol == null) continue;

            if (IsBladeOrTipTouchingCollider(opponentCol))
            {
                _lastHitTime = Time.time;
                HitCharacter();
                return;
            }
        }
    }

    private bool IsBladeOrTipTouchingCollider(Collider2D characterCollider)
    {
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();

        foreach (Collider2D myCol in myColliders)
        {
            if (myCol == null) continue;

            bool isBladeOrTip =
                myCol.CompareTag("BladeZone") ||
                myCol.CompareTag("TipZone");

            if (!isBladeOrTip)
                continue;

            if (myCol.IsTouching(characterCollider))
                return true;
        }

        return false;
    }

    private Collider2D[] GetOpponentCharacterColliders()
    {
        if (_opponentCharacterRoot != null)
            return _opponentCharacterRoot.GetComponentsInChildren<Collider2D>();

        if (_context == null)
            return null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_context.OpponentCharacterPos, 1.5f);

        int count = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && IsOpponentCharacterCollider(hits[i]))
                count++;
        }

        Collider2D[] result = new Collider2D[count];
        int index = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && IsOpponentCharacterCollider(hits[i]))
            {
                result[index] = hits[i];
                index++;
            }
        }

        return result;
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time - _lastCollisionTime < _collisionCooldown)
            return;

        if (collision.gameObject.CompareTag("PlayerSword") || collision.gameObject.CompareTag("EnemySword"))
        {
            _lastCollisionTime = Time.time;
            HandleSwordCollision(collision);
            return;
        }

        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(_wallProximityPenalty * 2f);
        }
    }

    private void HandleSwordCollision(Collision2D collision)
    {
        if (_swordPhysics == null)
            return;

        SwordPhysics otherPhysics = collision.gameObject.GetComponent<SwordPhysics>();

        if (otherPhysics == null || otherPhysics == _swordPhysics)
            return;

        ContactPoint2D contact = collision.GetContact(0);
        Vector2 contactPoint = contact.point;

        string myPart = _swordPhysics.GetCollisionPartString(contactPoint);
        string otherPart = otherPhysics.GetCollisionPartString(contactPoint);

        float reward = 0f;
        Color flashColor = _defaultColor;

        bool opponentDangerous =
            _context != null &&
            _context.OpponentThreatDistance <= _dangerRadius;

        if (myPart == "Blade" && otherPart == "Tip")
        {
            reward = opponentDangerous ? 0.12f : 0.04f;
            flashColor = _blockColor;

            if (opponentDangerous)
                _lastSuccessfulDefenseTime = Time.time;
        }
        else if (myPart == "Blade" && otherPart == "Blade")
        {
            reward = opponentDangerous ? 0.08f : 0.02f;
            flashColor = _blockColor;

            if (opponentDangerous)
                _lastSuccessfulDefenseTime = Time.time;
        }
        else if (myPart == "Tip" && otherPart == "Blade")
        {
            reward = -0.06f;
            flashColor = Color.yellow;
        }
        else if (myPart == "Handle")
        {
            reward = -0.12f;
            flashColor = Color.red;
        }
        else if (otherPart == "Handle")
        {
            reward = 0.03f;
            flashColor = Color.magenta;
        }

        if (Mathf.Abs(reward) > 0.0001f)
        {
            AddReward(reward);
            FlashColor(flashColor, 0.15f);
        }
    }

    private void HitCharacter()
    {
        HitsScored++;

        AddReward(_hitReward);

        if (Time.time - _lastSuccessfulDefenseTime <= _counterAttackWindow)
        {
            AddReward(_counterAttackHitReward);
        }

        FlashColor(_hitColor, 0.25f);

        OnHit?.Invoke();

        if (_opponentAgent != null)
        {
            _opponentAgent.GotHit();
        }

        CumulativeReward = GetCumulativeReward();

        if (!_useGameManagerMode)
        {
            EndEpisode();

            if (_opponentAgent != null)
                _opponentAgent.EndEpisode();
        }
    }

    public void GotHit()
    {
        HitsReceived++;

        AddReward(_gotHitPenalty);
        FlashColor(_damageColor, 0.25f);

        OnGotHit?.Invoke();

        CumulativeReward = GetCumulativeReward();

        if (!_useGameManagerMode)
        {
            EndEpisode();
        }
    }

    public void ResetAgentState()
    {
        _episodeTimer = _maxEpisodeTime;
        _lastHitTime = 0f;
        _lastCollisionTime = 0f;
        _lastSuccessfulDefenseTime = -999f;
        _safePassiveTimer = 0f;

        _sameActionCounter = 0;
        _lastMoveAction = SwordMoveAction.None;
        _lastRotateAction = SwordRotateAction.None;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        if (_executor != null)
            _executor.ResetExecutor();

        if (_swordRenderer != null)
            _swordRenderer.material.color = _defaultColor;

        CacheThreatDistances();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        int move = 0;
        int rotate = 0;

        bool up = Input.GetKey(KeyCode.W);
        bool down = Input.GetKey(KeyCode.S);
        bool left = Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.D);

        if (up && right) move = (int)SwordMoveAction.UpRight;
        else if (up && left) move = (int)SwordMoveAction.UpLeft;
        else if (down && right) move = (int)SwordMoveAction.DownRight;
        else if (down && left) move = (int)SwordMoveAction.DownLeft;
        else if (up) move = (int)SwordMoveAction.Up;
        else if (down) move = (int)SwordMoveAction.Down;
        else if (right) move = (int)SwordMoveAction.Right;
        else if (left) move = (int)SwordMoveAction.Left;

        if (Input.GetKey(KeyCode.LeftArrow))
            rotate = (int)SwordRotateAction.CounterClockwise;
        else if (Input.GetKey(KeyCode.RightArrow))
            rotate = (int)SwordRotateAction.Clockwise;

        discreteActions[0] = move;
        discreteActions[1] = rotate;
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
        Color original = _swordRenderer.material.color;
        _swordRenderer.material.color = color;

        yield return new WaitForSeconds(duration);

        _swordRenderer.material.color = original;
    }
}