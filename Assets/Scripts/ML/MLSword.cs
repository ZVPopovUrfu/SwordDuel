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

    [Header("Arena Random Spawn")]
    [SerializeField] private bool _useHalfArenaRandomSpawn = true;
    [SerializeField] private bool _isLeftTeam = true;
    [SerializeField] private Transform _arenaCenter;
    [SerializeField] private float _arenaWidth = 20f;
    [SerializeField] private float _arenaHeight = 10f;
    [SerializeField] private float _arenaPadding = 1.0f;
    [SerializeField] private float _spawnMinDistanceFromCenter = 0.8f;

    [Header("Mode")]
    [Tooltip("False для обучения. True для тестовой сцены, где счет и сброс делает NewGameManager.")]
    [SerializeField] private bool _useGameManagerMode = false;

    [Header("Main Rewards")]
    [SerializeField] private float _hitReward = 3.0f;
    [SerializeField] private float _gotHitPenalty = -3.0f;

    [Tooltip("Штраф за каждое решение агента. НЕ умножается на Time.fixedDeltaTime.")]
    [SerializeField] private float _stepPenalty = -0.0015f;

    [Header("Threat Logic")]
    [SerializeField] private float _dangerRadius = 2.4f;
    [SerializeField] private float _attackTieMargin = 0.05f;
    [SerializeField] private float _opponentClosingThreshold = 0.005f;

    [Header("Defense Rewards")]
    [Tooltip("Награда за активное отталкивание угрозы от своего персонажа.")]
    [SerializeField] private float _activePushReward = 0.010f;

    [Tooltip("Маленькая награда за закрытие линии угрозы.")]
    [SerializeField] private float _guardLineReward = 0.002f;

    [Tooltip("Штраф, если противник опасен, а защиты нет.")]
    [SerializeField] private float _failedDefensePenalty = -0.008f;

    [Tooltip("Штраф за пассивную защиту: угроза есть, но не выталкивается.")]
    [SerializeField] private float _passiveDefensePenalty = -0.005f;

    [Tooltip("Событийная награда за хороший контакт клинком в опасной ситуации.")]
    [SerializeField] private float _dangerousBladeBlockReward = 0.05f;

    [Header("Attack Rewards")]
    [Tooltip("Награда за сокращение дистанции до цели, когда атака оправдана.")]
    [SerializeField] private float _attackProgressReward = 0.008f;

    [Tooltip("Штраф, если надо атаковать, но дистанция не сокращается.")]
    [SerializeField] private float _wastedAttackChancePenalty = -0.006f;

    [Tooltip("Штраф за атаку без защиты, когда противник опасен.")]
    [SerializeField] private float _badAttackWhileDangerPenalty = -0.008f;

    [Tooltip("Маленькая награда за добивание на близкой дистанции.")]
    [SerializeField] private float _finishProgressReward = 0.006f;

    [Tooltip("Штраф, если агент близко, но не дожимает.")]
    [SerializeField] private float _closeButNotFinishingPenalty = -0.004f;

    [SerializeField] private float _finishDistance = 1.25f;

    [Header("Counterattack Rewards")]
    [SerializeField] private float _counterAttackWindow = 1.2f;
    [SerializeField] private float _counterAttackProgressReward = 0.006f;
    [SerializeField] private float _counterAttackHitReward = 0.5f;
    [SerializeField] private float _passiveAfterDefensePenalty = -0.004f;

    [Header("Anti-Passive Rewards")]
    [SerializeField] private float _campingRadius = 2.2f;
    [SerializeField] private float _campingPenalty = -0.006f;
    [SerializeField] private float _safePassivityPenalty = -0.005f;
    [SerializeField] private float _mutualPassivePenalty = -0.006f;
    [SerializeField] private float _passiveVelocityThreshold = 0.12f;
    [SerializeField] private float _safeNoThreatDistance = 2.8f;

    [Header("Sword Orientation Rewards")]
    [SerializeField] private float _handleForwardPenalty = -0.008f;
    [SerializeField] private float _tipAimReward = 0.002f;
    [SerializeField] private float _goodAimScore = 0.65f;

    [Header("Anti-Chaos Rewards")]
    [SerializeField] private float _rotationUsePenalty = -0.00015f;
    [SerializeField] private float _rotationSwitchPenalty = -0.0015f;
    [SerializeField] private float _sameActionRepeatPenalty = -0.001f;
    [SerializeField] private int _sameActionRepeatLimit = 14;
    [SerializeField] private float _wallProximityPenalty = -0.002f;

    [Header("Episode")]
    [SerializeField] private float _maxEpisodeTime = 30f;

    [Header("Draw / Timeout")]
    [SerializeField] private float _drawPenalty = -0.5f;

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

    public SwordMoveAction LastMoveAction => _lastMoveAction;
    public SwordRotateAction LastRotateAction => _lastRotateAction;
    public string CurrentContextStateName => GetContextStateName();

    private float _episodeTimer;
    private float _lastHitTime;
    private float _lastCollisionTime;
    private readonly float _collisionCooldown = 0.12f;

    private float _previousMyThreat;
    private float _previousOpponentThreat;
    private Vector2 _previousOpponentTipPos;

    private float _lastSuccessfulDefenseTime = -999f;

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

        _sameActionCounter = 0;
        _lastMoveAction = SwordMoveAction.None;
        _lastRotateAction = SwordRotateAction.None;

        if (!_useGameManagerMode)
        {
            ResetTransformAndPhysics();
        }

        if (_executor != null)
            _executor.ResetExecutor();

        if (_swordRenderer != null)
            _swordRenderer.material.color = _defaultColor;

        CachePreviousState();
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


    private string GetContextStateName()
    {
        if (_context == null)
            return "Unknown";

        float swordDistance = Vector2.Distance(_context.MySwordPos, _context.OpponentSwordPos);

        if (swordDistance <= 1.25f)
            return "Contact";

        if (_context.OpponentThreatDistance <= _dangerRadius)
            return "Defending";

        if (_context.MyThreatDistance <= _context.OpponentThreatDistance + _attackTieMargin)
            return "Attacking";

        return "Neutral";
    }

    private void CachePreviousState()
    {
        if (_context == null)
        {
            _previousMyThreat = 0f;
            _previousOpponentThreat = 0f;
            _previousOpponentTipPos = Vector2.zero;
            return;
        }

        _previousMyThreat = _context.MyThreatDistance;
        _previousOpponentThreat = _context.OpponentThreatDistance;
        _previousOpponentTipPos = _context.OpponentTipPos;
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

        _executor.SetAction(new SwordAction(moveAction, rotateAction));

        ApplyStepRewards(moveAction, rotateAction);
        CheckCharacterHitByBladeOrTip();

        _episodeTimer -= Time.fixedDeltaTime;

        if (!_useGameManagerMode && _episodeTimer <= 0f)
        {
            EndDrawEpisode();
        }

        CumulativeReward = GetCumulativeReward();
    }

    private void EndDrawEpisode()
    {
        AddReward(_drawPenalty);

        if (_opponentAgent != null)
        {
            _opponentAgent.AddReward(_opponentAgent._drawPenalty);
            _opponentAgent.EndEpisode();
        }

        EndEpisode();
    }

    private void ApplyStepRewards(SwordMoveAction moveAction, SwordRotateAction rotateAction)
    {
        AddReward(_stepPenalty);

        float currentMyThreat = _context.MyThreatDistance;
        float currentOpponentThreat = _context.OpponentThreatDistance;

        float myThreatProgress = _previousMyThreat - currentMyThreat;
        float opponentThreatProgress = _previousOpponentThreat - currentOpponentThreat;
        float opponentThreatChange = currentOpponentThreat - _previousOpponentThreat;

        float advantage = _context.ThreatAdvantage;

        bool opponentClosingIn = opponentThreatProgress > _opponentClosingThreshold;

        bool opponentIsAttacking =
            currentOpponentThreat <= _dangerRadius &&
            (_context.OpponentAimAtMyCharacterScore > 0.35f || opponentClosingIn);

        bool opponentHasThreatAdvantage =
            advantage < -_attackTieMargin;

        bool opponentDangerous =
            opponentIsAttacking || opponentHasThreatAdvantage;

        bool mySwordBlocksThreat = IsMySwordBetweenOpponentTipAndMyCharacter();

        bool shouldAttack =
            currentMyThreat <= currentOpponentThreat + _attackTieMargin;

        ApplyOrientationReward();

        if (opponentDangerous)
        {
            ApplyDefenseReward(currentOpponentThreat, opponentThreatChange, myThreatProgress, mySwordBlocksThreat);
        }
        else if (shouldAttack)
        {
            ApplyAttackReward(myThreatProgress);
        }
        else
        {
            ApplyNeutralPassivePenalty(myThreatProgress);
        }

        ApplyCounterattackReward(myThreatProgress, opponentDangerous);
        ApplyGeneralAntiPassivePenalty(myThreatProgress, opponentDangerous, shouldAttack);
        ApplyControlPenalty(moveAction, rotateAction, myThreatProgress, opponentDangerous);

        _lastMoveAction = moveAction;
        _lastRotateAction = rotateAction;

        _previousMyThreat = currentMyThreat;
        _previousOpponentThreat = currentOpponentThreat;
        _previousOpponentTipPos = _context.OpponentTipPos;
    }

    private void ApplyOrientationReward()
    {
        float handleToTarget = Vector2.Distance(_context.MyHandlePos, _context.OpponentAttackTargetPos);
        float tipToTarget = Vector2.Distance(_context.MyTipPos, _context.OpponentAttackTargetPos);

        if (handleToTarget < tipToTarget)
        {
            AddReward(_handleForwardPenalty);
        }
        else if (_context.MyAimAtOpponentScore > _goodAimScore)
        {
            AddReward(_tipAimReward);
        }
    }

    private void ApplyDefenseReward(
        float currentOpponentThreat,
        float opponentThreatChange,
        float myThreatProgress,
        bool mySwordBlocksThreat)
    {
        float reward = 0f;

        bool pushedByDistance = opponentThreatChange > 0.01f;
        bool pushedByDirection = IsOpponentTipPushedAway();

        if (pushedByDistance || pushedByDirection)
        {
            reward += _activePushReward;
            _lastSuccessfulDefenseTime = Time.time;
        }
        else if (currentOpponentThreat <= _dangerRadius)
        {
            reward += _passiveDefensePenalty;
        }

        if (mySwordBlocksThreat)
        {
            reward += _guardLineReward;
        }
        else if (currentOpponentThreat <= _dangerRadius)
        {
            reward += _failedDefensePenalty;
        }

        if (!mySwordBlocksThreat && myThreatProgress > 0.005f)
        {
            reward += _badAttackWhileDangerPenalty;
        }

        AddReward(reward);
    }

    private bool IsOpponentTipPushedAway()
    {
        Vector2 opponentTipDelta = _context.OpponentTipPos - _previousOpponentTipPos;

        if (opponentTipDelta.magnitude <= 0.005f)
            return false;

        Vector2 desiredPushDir = _context.OpponentTipPos - _context.MyCharacterPos;

        if (desiredPushDir.sqrMagnitude < 0.0001f)
            return false;

        desiredPushDir.Normalize();

        float pushScore = Vector2.Dot(opponentTipDelta.normalized, desiredPushDir);

        return pushScore > 0.45f;
    }

    private void ApplyAttackReward(float myThreatProgress)
    {
        float reward = 0f;

        if (myThreatProgress > 0.005f)
        {
            reward += _attackProgressReward;

            if (_context.MyThreatDistance <= _finishDistance && myThreatProgress > 0.003f)
            {
                reward += _finishProgressReward;
            }
        }
        else
        {
            reward += _wastedAttackChancePenalty;

            if (_context.MyThreatDistance <= _finishDistance && _context.MyThreatDistance > 0.25f)
            {
                reward += _closeButNotFinishingPenalty;
            }
        }

        AddReward(reward);
    }

    private void ApplyNeutralPassivePenalty(float myThreatProgress)
    {
        bool safeSituation =
            _context.OpponentThreatDistance > _safeNoThreatDistance &&
            _context.ThreatAdvantage > -0.1f;

        if (safeSituation && myThreatProgress <= 0.001f)
        {
            AddReward(_safePassivityPenalty);
        }
    }

    private void ApplyCounterattackReward(float myThreatProgress, bool opponentDangerous)
    {
        bool recentlyDefended = Time.time - _lastSuccessfulDefenseTime <= _counterAttackWindow;

        if (!recentlyDefended || opponentDangerous)
            return;

        if (myThreatProgress > 0.004f)
        {
            AddReward(_counterAttackProgressReward);
        }
        else
        {
            AddReward(_passiveAfterDefensePenalty);
        }
    }

    private void ApplyGeneralAntiPassivePenalty(float myThreatProgress, bool opponentDangerous, bool shouldAttack)
    {
        bool safeSituation =
            !opponentDangerous &&
            _context.OpponentThreatDistance > _safeNoThreatDistance &&
            _context.ThreatAdvantage > -0.1f;

        bool noAttackProgress = myThreatProgress <= 0.001f;

        bool campingNearOwnCharacter =
            Vector2.Distance(_context.MySwordPos, _context.MyCharacterPos) < _campingRadius;

        if (safeSituation && campingNearOwnCharacter && noAttackProgress)
        {
            AddReward(_campingPenalty);
        }

        bool mutualPassive =
            safeSituation &&
            _context.MyVelocity.magnitude < _passiveVelocityThreshold &&
            _context.OpponentVelocity.magnitude < _passiveVelocityThreshold &&
            Mathf.Abs(_context.ThreatAdvantage) < 0.5f &&
            noAttackProgress;

        if (mutualPassive)
        {
            AddReward(_mutualPassivePenalty);
        }
    }

    private void ApplyControlPenalty(
        SwordMoveAction moveAction,
        SwordRotateAction rotateAction,
        float myThreatProgress,
        bool opponentDangerous)
    {
        if (rotateAction != SwordRotateAction.None)
        {
            AddReward(_rotationUsePenalty);
        }

        if (IsOppositeRotation(_lastRotateAction, rotateAction))
        {
            AddReward(_rotationSwitchPenalty);
        }

        if (moveAction == _lastMoveAction && rotateAction == _lastRotateAction)
        {
            _sameActionCounter++;
        }
        else
        {
            _sameActionCounter = 0;
        }

        bool attackingWithProgress =
            !opponentDangerous &&
            myThreatProgress > 0.003f;

        if (_sameActionCounter >= _sameActionRepeatLimit && !attackingWithProgress)
        {
            AddReward(_sameActionRepeatPenalty);
        }

        if (_context.IsNearArenaEdge(_context.MySwordPos, 0.45f))
        {
            AddReward(_wallProximityPenalty);
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
            (_context.OpponentThreatDistance <= _dangerRadius ||
             _context.ThreatAdvantage < -_attackTieMargin);

        if (myPart == "Blade" && otherPart == "Tip")
        {
            reward = opponentDangerous ? _dangerousBladeBlockReward : 0.02f;
            flashColor = _blockColor;

            if (opponentDangerous)
                _lastSuccessfulDefenseTime = Time.time;
        }
        else if (myPart == "Blade" && otherPart == "Blade")
        {
            reward = opponentDangerous ? _dangerousBladeBlockReward * 0.65f : 0.01f;
            flashColor = _blockColor;

            if (opponentDangerous)
                _lastSuccessfulDefenseTime = Time.time;
        }
        else if (myPart == "Tip" && otherPart == "Handle")
        {
            reward = 0.04f;
            flashColor = Color.magenta;
            _lastSuccessfulDefenseTime = Time.time;
        }
        else if (myPart == "Blade" && otherPart == "Handle")
        {
            reward = 0.04f;
            flashColor = Color.magenta;
            _lastSuccessfulDefenseTime = Time.time;
        }
        else if (myPart == "Tip" && otherPart == "Blade")
        {
            reward = -0.03f;
            flashColor = Color.yellow;
        }
        else if (myPart == "Handle")
        {
            reward = -0.08f;
            flashColor = Color.red;
        }
        else if (otherPart == "Handle")
        {
            reward = 0.02f;
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

        CachePreviousState();
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