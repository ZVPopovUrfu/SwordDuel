using UnityEngine;

public class UserTestActionLogger : MonoBehaviour
{
    public enum EnemyAIType
    {
        FSM,
        ML
    }

    [Header("Session Info")]
    [SerializeField] private string _enemyLabel = "A";
    [SerializeField] private EnemyAIType _enemyAIType = EnemyAIType.FSM;

    [Header("Logging")]
    [SerializeField] private float _sampleInterval = 0.10f;
    [SerializeField] private bool _logOnlyWhenRoundActive = true;

    [Header("Enemy References")]
    [SerializeField] private Transform _enemySwordTransform;
    [SerializeField] private SwordActionExecutor _enemyExecutor;
    [SerializeField] private SwordCombatContext _enemyContext;
    [SerializeField] private FSMSword _fsmSword;
    [SerializeField] private MLSword _mlSword;

    [Header("Optional Round Manager")]
    [SerializeField] private UserTestRoundManager _roundManager;

    private UserTestSessionData _sessionData;
    private float _lastSampleTime;

    private void Awake()
    {
        _sessionData = UserTestSessionData.EnsureInstance();
        AutoFindMissingReferences();
    }

    private void Update()
    {
        if (_sessionData == null)
            return;

        if (_logOnlyWhenRoundActive && _roundManager != null && !_roundManager.IsRoundActive)
            return;

        if (Time.time - _lastSampleTime < _sampleInterval)
            return;

        _lastSampleTime = Time.time;
        LogEnemyAction();
    }

    private void AutoFindMissingReferences()
    {
        if (_enemyExecutor == null && _enemySwordTransform != null)
            _enemyExecutor = _enemySwordTransform.GetComponent<SwordActionExecutor>();

        if (_enemyContext == null && _enemySwordTransform != null)
            _enemyContext = _enemySwordTransform.GetComponent<SwordCombatContext>();

        if (_fsmSword == null && _enemySwordTransform != null)
            _fsmSword = _enemySwordTransform.GetComponent<FSMSword>();

        if (_mlSword == null && _enemySwordTransform != null)
            _mlSword = _enemySwordTransform.GetComponent<MLSword>();

        if (_roundManager == null)
            _roundManager = FindFirstObjectByType<UserTestRoundManager>();

        if (_roundManager != null)
        {
            _enemyLabel = _roundManager.EnemyLabel;

            if (_roundManager.EnemyAITypeName == EnemyAIType.ML.ToString())
                _enemyAIType = EnemyAIType.ML;
            else
                _enemyAIType = EnemyAIType.FSM;
        }
    }

    private void LogEnemyAction()
    {
        if (_enemyContext == null)
            return;

        string moveAction = GetEnemyMoveAction();
        string rotateAction = GetEnemyRotateAction();
        string combinedAction = moveAction + "_" + rotateAction;

        string behaviorState = GetEnemyBehaviorState();
        string openingPlan = GetEnemyOpeningPlan();

        float swordDistance = Vector2.Distance(_enemyContext.MySwordPos, _enemyContext.OpponentSwordPos);

        int round = _roundManager != null ? _roundManager.CurrentRound : 0;

        string[] row =
        {
            _sessionData.SessionId,
            _sessionData.TestStartTimestamp,
            _enemyLabel,
            _enemyAIType.ToString(),
            round.ToString(),
            Time.time.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            "Enemy",
            moveAction,
            rotateAction,
            combinedAction,
            behaviorState,
            openingPlan,
            _enemyContext.MySwordPos.x.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.MySwordPos.y.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.MyTipPos.x.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.MyTipPos.y.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.MyHandlePos.x.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.MyHandlePos.y.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.MyThreatDistance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.OpponentThreatDistance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            _enemyContext.ThreatAdvantage.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            swordDistance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
        };

        _sessionData.AddActionRow(row);
    }

    private string GetEnemyMoveAction()
    {
        if (_enemyAIType == EnemyAIType.ML && _mlSword != null)
            return _mlSword.LastMoveAction.ToString();

        if (_enemyExecutor != null)
            return _enemyExecutor.LastMoveActionName;

        return "Unknown";
    }

    private string GetEnemyRotateAction()
    {
        if (_enemyAIType == EnemyAIType.ML && _mlSword != null)
            return _mlSword.LastRotateAction.ToString();

        if (_enemyExecutor != null)
            return _enemyExecutor.LastRotateActionName;

        return "Unknown";
    }

    private string GetEnemyBehaviorState()
    {
        if (_enemyAIType == EnemyAIType.FSM && _fsmSword != null)
            return _fsmSword.CurrentStateName;

        if (_enemyAIType == EnemyAIType.ML && _mlSword != null)
            return _mlSword.CurrentContextStateName;

        return "Unknown";
    }

    private string GetEnemyOpeningPlan()
    {
        if (_enemyAIType == EnemyAIType.FSM && _fsmSword != null)
            return _fsmSword.CurrentOpeningPlanName;

        return "";
    }
}
