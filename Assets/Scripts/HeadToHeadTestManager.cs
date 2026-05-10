using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Globalization;
using static SwordActionTypes;

public class HeadToHeadTestManager : MonoBehaviour
{
    public enum AgentKind
    {
        FSM,
        ML
    }

    public enum StartSide
    {
        Left,
        Right
    }

    private enum RoundResult
    {
        None,
        FSMWin,
        MLWin,
        Draw
    }

    private enum FirstClashOwner
    {
        None,
        FSM,
        ML,
        Contested
    }

    private const string CsvSeparator = ";";
    private static readonly CultureInfo CsvCulture = new CultureInfo("ru-RU");

    [Header("Agents")]
    [SerializeField] private FSMSword _fsmSword;
    [SerializeField] private MLSword _mlSword;

    [Header("Executors")]
    [SerializeField] private SwordActionExecutor _fsmExecutor;
    [SerializeField] private SwordActionExecutor _mlExecutor;

    [Header("Contexts")]
    [SerializeField] private SwordCombatContext _fsmContext;
    [SerializeField] private SwordCombatContext _mlContext;

    [Header("Transforms")]
    [SerializeField] private Transform _fsmSwordTransform;
    [SerializeField] private Transform _mlSwordTransform;
    [SerializeField] private Rigidbody2D _fsmRb;
    [SerializeField] private Rigidbody2D _mlRb;

    [Header("Spawn Points")]
    [SerializeField] private Transform _fsmSpawnPoint;
    [SerializeField] private Transform _mlSpawnPoint;

    [Header("Side Setup")]
    [SerializeField] private StartSide _fsmStartSide = StartSide.Left;
    [SerializeField] private StartSide _mlStartSide = StartSide.Right;

    [Header("Round Settings")]
    [SerializeField] private int _maxRounds = 100;
    [SerializeField] private float _roundTimeLimit = 30f;
    [SerializeField] private float _roundStartGraceTime = 0.45f;
    [SerializeField] private float _timeBetweenRounds = 0.35f;
    [SerializeField] private bool _autoStart = true;

    [Header("Series Info")]
    [SerializeField] private string _seriesId = "S01";
    [SerializeField] private string _configurationName = "H2H_FSM_Left_ML_Right";
    [SerializeField] private string _mlModelName = "ML_Final";
    [SerializeField] private string _fsmVersionName = "FSM_Final_Calibrated";

    [Header("CSV Logging")]
    [SerializeField] private bool _writeCsv = true;
    [SerializeField] private string _fileNamePrefix = "h2h";
    [SerializeField] private bool _writeActionLog = true;
    [SerializeField] private float _actionSampleInterval = 0.10f;

    [Header("Debug")]
    [SerializeField] private bool _logToConsole = true;

    private int _currentRound;
    private int _fsmWins;
    private int _mlWins;
    private int _draws;
    private int _fsmFirstClashes;
    private int _mlFirstClashes;
    private int _contestedFirstClashes;
    private int _noFirstClashes;

    private bool _roundActive;
    private bool _seriesFinished;
    private float _roundStartTime;
    private float _nextActionSampleTime;

    private bool _firstClashRecorded;
    private float _firstClashTime = -1f;
    private FirstClashOwner _firstClashOwner = FirstClashOwner.None;
    private string _firstClashFsmPart = "";
    private string _firstClashMlPart = "";

    private StreamWriter _roundWriter;
    private StreamWriter _actionWriter;
    private string _roundCsvPath;
    private string _actionCsvPath;

    public int CurrentRound => _currentRound;
    public bool RoundActive => _roundActive;

    private void Awake()
    {
        AutoFindReferencesIfNeeded();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        CloseCsv();
    }

    private void Start()
    {
        if (_autoStart)
            StartHeadToHeadTest();
    }

    private void AutoFindReferencesIfNeeded()
    {
        if (_fsmSword == null)
            _fsmSword = FindObjectOfType<FSMSword>();

        if (_mlSword == null)
            _mlSword = FindObjectOfType<MLSword>();

        if (_fsmSword != null && _fsmSwordTransform == null)
            _fsmSwordTransform = _fsmSword.transform;

        if (_mlSword != null && _mlSwordTransform == null)
            _mlSwordTransform = _mlSword.transform;

        if (_fsmSwordTransform != null && _fsmRb == null)
            _fsmRb = _fsmSwordTransform.GetComponent<Rigidbody2D>();

        if (_mlSwordTransform != null && _mlRb == null)
            _mlRb = _mlSwordTransform.GetComponent<Rigidbody2D>();

        if (_fsmSwordTransform != null && _fsmExecutor == null)
            _fsmExecutor = _fsmSwordTransform.GetComponent<SwordActionExecutor>();

        if (_mlSwordTransform != null && _mlExecutor == null)
            _mlExecutor = _mlSwordTransform.GetComponent<SwordActionExecutor>();

        if (_fsmSwordTransform != null && _fsmContext == null)
            _fsmContext = _fsmSwordTransform.GetComponent<SwordCombatContext>();

        if (_mlSwordTransform != null && _mlContext == null)
            _mlContext = _mlSwordTransform.GetComponent<SwordCombatContext>();
    }

    private void SubscribeEvents()
    {
        if (_fsmSword != null)
            _fsmSword.OnHit += HandleFSMHit;

        if (_mlSword != null)
            _mlSword.OnHit += HandleMLHit;
    }

    private void UnsubscribeEvents()
    {
        if (_fsmSword != null)
            _fsmSword.OnHit -= HandleFSMHit;

        if (_mlSword != null)
            _mlSword.OnHit -= HandleMLHit;
    }

    [ContextMenu("Start Head-to-Head Test")]
    public void StartHeadToHeadTest()
    {
        if (_seriesFinished)
            return;

        if (!ValidateSetup())
            return;

        PrepareCsv();
        ResetCounters();

        _seriesFinished = false;
        StartCoroutine(TestRoutine());
    }

    private void ResetCounters()
    {
        _currentRound = 0;
        _fsmWins = 0;
        _mlWins = 0;
        _draws = 0;
        _fsmFirstClashes = 0;
        _mlFirstClashes = 0;
        _contestedFirstClashes = 0;
        _noFirstClashes = 0;
    }

    private bool ValidateSetup()
    {
        bool valid = true;

        if (_fsmSword == null) { Debug.LogError("[H2H] FSMSword is not assigned."); valid = false; }
        if (_mlSword == null) { Debug.LogError("[H2H] MLSword is not assigned."); valid = false; }
        if (_fsmSwordTransform == null) { Debug.LogError("[H2H] FSM sword transform is not assigned."); valid = false; }
        if (_mlSwordTransform == null) { Debug.LogError("[H2H] ML sword transform is not assigned."); valid = false; }
        if (_fsmSpawnPoint == null) { Debug.LogError("[H2H] FSM spawn point is not assigned."); valid = false; }
        if (_mlSpawnPoint == null) { Debug.LogError("[H2H] ML spawn point is not assigned."); valid = false; }
        if (_fsmExecutor == null) { Debug.LogError("[H2H] FSM executor is not assigned."); valid = false; }
        if (_mlExecutor == null) { Debug.LogError("[H2H] ML executor is not assigned."); valid = false; }
        if (_fsmContext == null) { Debug.LogError("[H2H] FSM context is not assigned."); valid = false; }
        if (_mlContext == null) { Debug.LogError("[H2H] ML context is not assigned."); valid = false; }

        return valid;
    }

    private IEnumerator TestRoutine()
    {
        while (_currentRound < _maxRounds)
        {
            yield return StartNewRoundRoutine();

            while (_roundActive)
            {
                float elapsed = Time.time - _roundStartTime;

                if (_writeActionLog && Time.time >= _nextActionSampleTime)
                {
                    WriteActionSample(AgentKind.FSM);
                    WriteActionSample(AgentKind.ML);
                    _nextActionSampleTime = Time.time + _actionSampleInterval;
                }

                if (elapsed >= _roundTimeLimit)
                {
                    EndRound(RoundResult.Draw);
                    break;
                }

                yield return null;
            }

            StopBothSwords();
            yield return new WaitForSeconds(_timeBetweenRounds);
        }

        FinishSeries();
    }

    private IEnumerator StartNewRoundRoutine()
    {
        _roundActive = false;
        _currentRound++;

        _firstClashRecorded = false;
        _firstClashTime = -1f;
        _firstClashOwner = FirstClashOwner.None;
        _firstClashFsmPart = "";
        _firstClashMlPart = "";

        StopBothSwords();

        ResetSword(_fsmSwordTransform, _fsmRb, _fsmSpawnPoint);
        ResetSword(_mlSwordTransform, _mlRb, _mlSpawnPoint);

        if (_fsmSword != null)
            _fsmSword.ResetState();

        if (_mlSword != null)
            _mlSword.ResetAgentState();

        ResetSword(_fsmSwordTransform, _fsmRb, _fsmSpawnPoint);
        ResetSword(_mlSwordTransform, _mlRb, _mlSpawnPoint);

        StopBothSwords();

        yield return new WaitForFixedUpdate();

        StopBothSwords();

        _roundStartTime = Time.time;
        _nextActionSampleTime = Time.time;
        _roundActive = true;

        if (_logToConsole)
            Debug.Log($"[H2H] Round {_currentRound}/{_maxRounds} started.");
    }

    private void ResetSword(Transform swordTransform, Rigidbody2D rb, Transform spawnPoint)
    {
        if (swordTransform == null || spawnPoint == null)
            return;

        swordTransform.position = spawnPoint.position;
        swordTransform.rotation = spawnPoint.rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void StopBothSwords()
    {
        if (_fsmRb != null)
        {
            _fsmRb.linearVelocity = Vector2.zero;
            _fsmRb.angularVelocity = 0f;
        }

        if (_mlRb != null)
        {
            _mlRb.linearVelocity = Vector2.zero;
            _mlRb.angularVelocity = 0f;
        }
    }

    private void HandleFSMHit()
    {
        if (!_roundActive || IsInRoundStartGracePeriod())
            return;

        EndRound(RoundResult.FSMWin);
    }

    private void HandleMLHit()
    {
        if (!_roundActive || IsInRoundStartGracePeriod())
            return;

        EndRound(RoundResult.MLWin);
    }

    private bool IsInRoundStartGracePeriod()
    {
        return Time.time - _roundStartTime < _roundStartGraceTime;
    }

    public void ReportSwordClash(AgentKind reporterKind, string reporterPart, AgentKind otherKind, string otherPart)
    {
        if (!_roundActive || IsInRoundStartGracePeriod() || _firstClashRecorded)
            return;

        if (reporterKind == otherKind)
            return;

        string fsmPart;
        string mlPart;

        if (reporterKind == AgentKind.FSM)
        {
            fsmPart = NormalizePartName(reporterPart);
            mlPart = NormalizePartName(otherPart);
        }
        else
        {
            fsmPart = NormalizePartName(otherPart);
            mlPart = NormalizePartName(reporterPart);
        }

        FirstClashOwner owner = EvaluateFirstClashOwner(fsmPart, mlPart);

        if (owner == FirstClashOwner.None)
            return;

        _firstClashRecorded = true;
        _firstClashTime = Time.time - _roundStartTime;
        _firstClashOwner = owner;
        _firstClashFsmPart = fsmPart;
        _firstClashMlPart = mlPart;

        switch (owner)
        {
            case FirstClashOwner.FSM: _fsmFirstClashes++; break;
            case FirstClashOwner.ML: _mlFirstClashes++; break;
            case FirstClashOwner.Contested: _contestedFirstClashes++; break;
        }
    }

    private string NormalizePartName(string part)
    {
        if (string.IsNullOrWhiteSpace(part))
            return "Unknown";

        part = part.Trim();

        if (part.Equals("Tip", StringComparison.OrdinalIgnoreCase)) return "Tip";
        if (part.Equals("Blade", StringComparison.OrdinalIgnoreCase)) return "Blade";
        if (part.Equals("Handle", StringComparison.OrdinalIgnoreCase)) return "Handle";

        return part;
    }

    private FirstClashOwner EvaluateFirstClashOwner(string fsmPart, string mlPart)
    {
        bool fsmTip = fsmPart == "Tip";
        bool fsmBlade = fsmPart == "Blade";
        bool fsmHandle = fsmPart == "Handle";

        bool mlTip = mlPart == "Tip";
        bool mlBlade = mlPart == "Blade";
        bool mlHandle = mlPart == "Handle";

        if (fsmTip && mlTip) return FirstClashOwner.Contested;
        if (fsmBlade && mlBlade) return FirstClashOwner.Contested;

        if (fsmBlade && mlTip) return FirstClashOwner.FSM;
        if (fsmTip && mlBlade) return FirstClashOwner.ML;

        if ((fsmTip || fsmBlade) && mlHandle) return FirstClashOwner.FSM;
        if (fsmHandle && (mlTip || mlBlade)) return FirstClashOwner.ML;

        return FirstClashOwner.None;
    }

    private void EndRound(RoundResult result)
    {
        if (!_roundActive)
            return;

        _roundActive = false;
        float duration = Time.time - _roundStartTime;

        if (!_firstClashRecorded)
            _noFirstClashes++;

        switch (result)
        {
            case RoundResult.FSMWin: _fsmWins++; break;
            case RoundResult.MLWin: _mlWins++; break;
            case RoundResult.Draw: _draws++; break;
        }

        WriteRoundToCsv(_currentRound, result, duration);

        if (_logToConsole)
        {
            Debug.Log(
                $"[H2H] Round {_currentRound} ended. Result: {result}. " +
                $"Duration: {duration.ToString("F2", CsvCulture)}s. FirstClash: {_firstClashOwner}."
            );
        }
    }

    private void FinishSeries()
    {
        _seriesFinished = true;
        CloseCsv();

        Debug.Log(
            "\n========== HEAD-TO-HEAD TEST FINISHED ==========\n" +
            $"Series: {_seriesId}\n" +
            $"Configuration: {_configurationName}\n" +
            $"Rounds: {_maxRounds}\n" +
            $"FSM Wins: {_fsmWins}\n" +
            $"ML Wins: {_mlWins}\n" +
            $"Draws: {_draws}\n" +
            $"FSM First Clashes: {_fsmFirstClashes}\n" +
            $"ML First Clashes: {_mlFirstClashes}\n" +
            $"Contested First Clashes: {_contestedFirstClashes}\n" +
            $"No First Clash: {_noFirstClashes}\n" +
            $"Rounds CSV: {_roundCsvPath}\n" +
            $"Actions CSV: {_actionCsvPath}\n" +
            "================================================"
        );
    }

    private void PrepareCsv()
    {
        if (!_writeCsv)
            return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string safeConfig = MakeSafeFileName(_configurationName);
        string safeSeries = MakeSafeFileName(_seriesId);

        _roundCsvPath = Path.Combine(
            Application.persistentDataPath,
            $"{_fileNamePrefix}_rounds_{safeConfig}_{safeSeries}_{timestamp}.csv"
        );

        _roundWriter = new StreamWriter(_roundCsvPath, false, Encoding.UTF8);
        _roundWriter.WriteLine("sep=;");
        _roundWriter.WriteLine(string.Join(CsvSeparator, new string[]
        {
            "series_id",
            "round",
            "configuration",
            "ml_model",
            "fsm_version",
            "result",
            "winner",
            "winner_side",
            "duration_seconds",
            "score_differential_ml_minus_fsm",
            "first_clash_owner",
            "time_to_first_clash_seconds",
            "first_clash_fsm_part",
            "first_clash_ml_part",
            "fsm_wins_total",
            "ml_wins_total",
            "draws_total",
            "fsm_first_clashes_total",
            "ml_first_clashes_total",
            "contested_first_clashes_total",
            "no_first_clashes_total"
        }));
        _roundWriter.Flush();

        if (_writeActionLog)
        {
            _actionCsvPath = Path.Combine(
                Application.persistentDataPath,
                $"{_fileNamePrefix}_actions_{safeConfig}_{safeSeries}_{timestamp}.csv"
            );

            _actionWriter = new StreamWriter(_actionCsvPath, false, Encoding.UTF8);
            _actionWriter.WriteLine("sep=;");
            _actionWriter.WriteLine(string.Join(CsvSeparator, new string[]
            {
                "series_id",
                "round",
                "configuration",
                "time_seconds",
                "agent",
                "agent_side",
                "move_action",
                "rotate_action",
                "combined_action",
                "behavior_state",
                "opening_plan",
                "sword_x",
                "sword_y",
                "tip_x",
                "tip_y",
                "handle_x",
                "handle_y",
                "my_threat_distance",
                "opponent_threat_distance",
                "threat_advantage",
                "sword_distance"
            }));
            _actionWriter.Flush();
        }
    }

    private void WriteRoundToCsv(int round, RoundResult result, float duration)
    {
        if (!_writeCsv || _roundWriter == null)
            return;

        string winner = GetWinnerName(result);
        string winnerSide = GetWinnerSide(result);
        int scoreDiff = _mlWins - _fsmWins;
        string firstClashTimeText = _firstClashRecorded ? _firstClashTime.ToString("F4", CsvCulture) : "";

        string line = string.Join(CsvSeparator, new string[]
        {
            EscapeCsvCell(_seriesId),
            round.ToString(CsvCulture),
            EscapeCsvCell(_configurationName),
            EscapeCsvCell(_mlModelName),
            EscapeCsvCell(_fsmVersionName),
            result.ToString(),
            winner,
            winnerSide,
            duration.ToString("F4", CsvCulture),
            scoreDiff.ToString(CsvCulture),
            _firstClashOwner.ToString(),
            firstClashTimeText,
            EscapeCsvCell(_firstClashFsmPart),
            EscapeCsvCell(_firstClashMlPart),
            _fsmWins.ToString(CsvCulture),
            _mlWins.ToString(CsvCulture),
            _draws.ToString(CsvCulture),
            _fsmFirstClashes.ToString(CsvCulture),
            _mlFirstClashes.ToString(CsvCulture),
            _contestedFirstClashes.ToString(CsvCulture),
            _noFirstClashes.ToString(CsvCulture)
        });

        _roundWriter.WriteLine(line);
        _roundWriter.Flush();
    }

    private void WriteActionSample(AgentKind agentKind)
    {
        if (!_writeCsv || !_writeActionLog || _actionWriter == null)
            return;

        SwordActionExecutor executor = agentKind == AgentKind.FSM ? _fsmExecutor : _mlExecutor;
        SwordCombatContext context = agentKind == AgentKind.FSM ? _fsmContext : _mlContext;
        Transform swordTransform = agentKind == AgentKind.FSM ? _fsmSwordTransform : _mlSwordTransform;

        if (executor == null || context == null || swordTransform == null)
            return;

        SwordAction action = executor.LastAction;
        string state = GetBehaviorState(agentKind, context);
        string openingPlan = agentKind == AgentKind.FSM && _fsmSword != null
            ? _fsmSword.CurrentOpeningPlanName
            : "";

        float elapsed = Time.time - _roundStartTime;
        float swordDistance = Vector2.Distance(context.MySwordPos, context.OpponentSwordPos);

        string line = string.Join(CsvSeparator, new string[]
        {
            EscapeCsvCell(_seriesId),
            _currentRound.ToString(CsvCulture),
            EscapeCsvCell(_configurationName),
            elapsed.ToString("F4", CsvCulture),
            agentKind.ToString(),
            GetAgentSide(agentKind),
            action.Move.ToString(),
            action.Rotate.ToString(),
            $"{action.Move}_{action.Rotate}",
            state,
            openingPlan,
            context.MySwordPos.x.ToString("F4", CsvCulture),
            context.MySwordPos.y.ToString("F4", CsvCulture),
            context.MyTipPos.x.ToString("F4", CsvCulture),
            context.MyTipPos.y.ToString("F4", CsvCulture),
            context.MyHandlePos.x.ToString("F4", CsvCulture),
            context.MyHandlePos.y.ToString("F4", CsvCulture),
            context.MyThreatDistance.ToString("F4", CsvCulture),
            context.OpponentThreatDistance.ToString("F4", CsvCulture),
            context.ThreatAdvantage.ToString("F4", CsvCulture),
            swordDistance.ToString("F4", CsvCulture)
        });

        _actionWriter.WriteLine(line);
    }

    private string GetBehaviorState(AgentKind agentKind, SwordCombatContext context)
    {
        if (agentKind == AgentKind.FSM && _fsmSword != null)
            return _fsmSword.CurrentStateName;

        if (agentKind == AgentKind.ML && _mlSword != null)
            return _mlSword.CurrentContextStateName;

        if (context == null)
            return "Unknown";

        float swordDistance = Vector2.Distance(context.MySwordPos, context.OpponentSwordPos);

        if (swordDistance <= 1.25f)
            return "Contact";

        if (context.OpponentThreatDistance <= 2.4f)
            return "Defending";

        if (context.MyThreatDistance <= context.OpponentThreatDistance + 0.1f)
            return "Attacking";

        return "Neutral";
    }

    private string GetWinnerName(RoundResult result)
    {
        switch (result)
        {
            case RoundResult.FSMWin: return "FSM";
            case RoundResult.MLWin: return "ML";
            case RoundResult.Draw: return "Draw";
            default: return "None";
        }
    }

    private string GetWinnerSide(RoundResult result)
    {
        switch (result)
        {
            case RoundResult.FSMWin: return _fsmStartSide.ToString();
            case RoundResult.MLWin: return _mlStartSide.ToString();
            case RoundResult.Draw: return "Draw";
            default: return "None";
        }
    }

    private string GetAgentSide(AgentKind agentKind)
    {
        return agentKind == AgentKind.FSM ? _fsmStartSide.ToString() : _mlStartSide.ToString();
    }

    private string EscapeCsvCell(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        bool mustQuote =
            value.Contains(CsvSeparator) ||
            value.Contains("\"") ||
            value.Contains("\n") ||
            value.Contains("\r");

        if (!mustQuote)
            return value;

        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private string MakeSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(' ', '_');
    }

    private void CloseCsv()
    {
        if (_roundWriter != null)
        {
            _roundWriter.Flush();
            _roundWriter.Close();
            _roundWriter = null;
        }

        if (_actionWriter != null)
        {
            _actionWriter.Flush();
            _actionWriter.Close();
            _actionWriter = null;
        }
    }
}
