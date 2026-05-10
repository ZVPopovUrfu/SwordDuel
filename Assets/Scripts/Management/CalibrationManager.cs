using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Globalization;

public class CalibrationManager : MonoBehaviour
{
    public enum AgentSide
    {
        FSM,
        ML
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

    [Header("Transforms")]
    [SerializeField] private Transform _fsmSwordTransform;
    [SerializeField] private Transform _mlSwordTransform;

    [SerializeField] private Rigidbody2D _fsmRb;
    [SerializeField] private Rigidbody2D _mlRb;

    [Header("Spawn Points")]
    [SerializeField] private Transform _fsmSpawnPoint;
    [SerializeField] private Transform _mlSpawnPoint;

    [Header("Round Settings")]
    [SerializeField] private int _maxRounds = 100;
    [SerializeField] private float _roundTimeLimit = 30f;
    [SerializeField] private float _roundStartGraceTime = 0.25f;
    [SerializeField] private float _timeBetweenRounds = 0.35f;
    [SerializeField] private bool _autoStart = true;

    [Header("Experiment Info")]
    [Tooltip("Например: FSM_Left_ML_Right или ML_Left_FSM_Right")]
    [SerializeField] private string _configurationName = "FSM_Left_ML_Right";

    [Tooltip("Например: FencingSword_CounterPenalty_Draw_15M")]
    [SerializeField] private string _mlModelName = "ML_Model";

    [Tooltip("Например: Current_FSMSword")]
    [SerializeField] private string _fsmVersionName = "FSM_Current";

    [Header("CSV Logging")]
    [SerializeField] private bool _writeCsv = true;
    [SerializeField] private string _fileNamePrefix = "calibration_results";

    [Header("Debug")]
    [SerializeField] private bool _logToConsole = true;

    private int _currentRound = 0;

    private int _fsmWins = 0;
    private int _mlWins = 0;
    private int _draws = 0;

    private int _fsmFirstClashes = 0;
    private int _mlFirstClashes = 0;
    private int _contestedFirstClashes = 0;
    private int _noFirstClashes = 0;

    private bool _roundActive = false;
    private bool _seriesFinished = false;

    private float _roundStartTime = 0f;

    private bool _firstClashRecorded = false;
    private float _firstClashTime = -1f;
    private FirstClashOwner _firstClashOwner = FirstClashOwner.None;
    private string _firstClashFsmPart = "";
    private string _firstClashMlPart = "";

    private RoundResult _currentResult = RoundResult.None;

    private StreamWriter _writer;
    private string _csvPath;

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
        {
            StartCalibration();
        }
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

    [ContextMenu("Start Calibration")]
    public void StartCalibration()
    {
        if (_seriesFinished)
            return;

        if (!ValidateSetup())
            return;

        PrepareCsv();

        _currentRound = 0;
        _fsmWins = 0;
        _mlWins = 0;
        _draws = 0;

        _fsmFirstClashes = 0;
        _mlFirstClashes = 0;
        _contestedFirstClashes = 0;
        _noFirstClashes = 0;

        _seriesFinished = false;

        StartCoroutine(CalibrationRoutine());
    }

    private bool ValidateSetup()
    {
        bool valid = true;

        if (_fsmSword == null)
        {
            Debug.LogError("[CalibrationManager] FSMSword is not assigned.");
            valid = false;
        }

        if (_mlSword == null)
        {
            Debug.LogError("[CalibrationManager] MLSword is not assigned.");
            valid = false;
        }

        if (_fsmSwordTransform == null)
        {
            Debug.LogError("[CalibrationManager] FSM sword transform is not assigned.");
            valid = false;
        }

        if (_mlSwordTransform == null)
        {
            Debug.LogError("[CalibrationManager] ML sword transform is not assigned.");
            valid = false;
        }

        if (_fsmSpawnPoint == null)
        {
            Debug.LogError("[CalibrationManager] FSM spawn point is not assigned.");
            valid = false;
        }

        if (_mlSpawnPoint == null)
        {
            Debug.LogError("[CalibrationManager] ML spawn point is not assigned.");
            valid = false;
        }

        return valid;
    }

    private IEnumerator CalibrationRoutine()
    {
        while (_currentRound < _maxRounds)
        {
            yield return StartNewRoundRoutine();

            while (_roundActive)
            {
                float elapsed = Time.time - _roundStartTime;

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

        _currentResult = RoundResult.None;

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

        // Даём Unity Physics обновить trigger/collision состояние после телепортации.
        yield return new WaitForFixedUpdate();

        StopBothSwords();

        _roundStartTime = Time.time;
        _roundActive = true;

        if (_logToConsole)
        {
            Debug.Log($"[CalibrationManager] Round {_currentRound}/{_maxRounds} started.");
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

    private void HandleFSMHit()
    {
        if (!_roundActive)
            return;

        if (IsInRoundStartGracePeriod())
            return;

        EndRound(RoundResult.FSMWin);
    }

    private void HandleMLHit()
    {
        if (!_roundActive)
            return;

        if (IsInRoundStartGracePeriod())
            return;

        EndRound(RoundResult.MLWin);
    }

    private bool IsInRoundStartGracePeriod()
    {
        return Time.time - _roundStartTime < _roundStartGraceTime;
    }

    public void ReportSwordClash(
        AgentSide reporterSide,
        string reporterPart,
        AgentSide otherSide,
        string otherPart)
    {
        if (!_roundActive)
            return;

        if (IsInRoundStartGracePeriod())
            return;

        if (_firstClashRecorded)
            return;

        if (reporterSide == otherSide)
            return;

        string fsmPart;
        string mlPart;

        if (reporterSide == AgentSide.FSM)
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

        // Handle vs Handle и неизвестные варианты не считаем значимым первым clash.
        if (owner == FirstClashOwner.None)
            return;

        _firstClashRecorded = true;
        _firstClashTime = Time.time - _roundStartTime;
        _firstClashOwner = owner;
        _firstClashFsmPart = fsmPart;
        _firstClashMlPart = mlPart;

        switch (owner)
        {
            case FirstClashOwner.FSM:
                _fsmFirstClashes++;
                break;

            case FirstClashOwner.ML:
                _mlFirstClashes++;
                break;

            case FirstClashOwner.Contested:
                _contestedFirstClashes++;
                break;
        }

        if (_logToConsole)
        {
            Debug.Log(
                $"[CalibrationManager] First clash: {owner}. " +
                $"FSM part: {fsmPart}. ML part: {mlPart}. " +
                $"Time: {_firstClashTime.ToString("F2", CsvCulture)}s."
            );
        }
    }

    private string NormalizePartName(string part)
    {
        if (string.IsNullOrWhiteSpace(part))
            return "Unknown";

        part = part.Trim();

        if (part.Equals("Tip", StringComparison.OrdinalIgnoreCase))
            return "Tip";

        if (part.Equals("Blade", StringComparison.OrdinalIgnoreCase))
            return "Blade";

        if (part.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            return "Handle";

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

        // Оба столкнулись равнозначными сильными зонами.
        if (fsmTip && mlTip)
            return FirstClashOwner.Contested;

        if (fsmBlade && mlBlade)
            return FirstClashOwner.Contested;

        // Blade против Tip: преимущество у Blade, потому что Tip отлетает сильнее.
        if (fsmBlade && mlTip)
            return FirstClashOwner.FSM;

        if (fsmTip && mlBlade)
            return FirstClashOwner.ML;

        // Tip/Blade против Handle: преимущество у того, кто попал не рукоятью.
        if ((fsmTip || fsmBlade) && mlHandle)
            return FirstClashOwner.FSM;

        if (fsmHandle && (mlTip || mlBlade))
            return FirstClashOwner.ML;

        // Handle vs Handle не является успешным первым столкновением.
        if (fsmHandle && mlHandle)
            return FirstClashOwner.None;

        return FirstClashOwner.None;
    }

    private void EndRound(RoundResult result)
    {
        if (!_roundActive)
            return;

        _roundActive = false;
        _currentResult = result;

        float duration = Time.time - _roundStartTime;

        if (!_firstClashRecorded)
        {
            _noFirstClashes++;
        }

        switch (result)
        {
            case RoundResult.FSMWin:
                _fsmWins++;
                break;

            case RoundResult.MLWin:
                _mlWins++;
                break;

            case RoundResult.Draw:
                _draws++;
                break;
        }

        WriteRoundToCsv(_currentRound, result, duration);

        if (_logToConsole)
        {
            string firstClashText = _firstClashRecorded
                ? $"{_firstClashOwner} at {_firstClashTime.ToString("F2", CsvCulture)}s"
                : "None";

            Debug.Log(
                $"[CalibrationManager] Round {_currentRound} ended. " +
                $"Result: {result}. Duration: {duration.ToString("F2", CsvCulture)}s. " +
                $"FirstClash: {firstClashText}."
            );
        }
    }

    private void FinishSeries()
    {
        _seriesFinished = true;
        CloseCsv();

        float fsmWinRate = _maxRounds > 0 ? (float)_fsmWins / _maxRounds : 0f;
        float mlWinRate = _maxRounds > 0 ? (float)_mlWins / _maxRounds : 0f;
        float drawRate = _maxRounds > 0 ? (float)_draws / _maxRounds : 0f;

        float fsmFirstClashRate = _maxRounds > 0 ? (float)_fsmFirstClashes / _maxRounds : 0f;
        float mlFirstClashRate = _maxRounds > 0 ? (float)_mlFirstClashes / _maxRounds : 0f;
        float contestedFirstClashRate = _maxRounds > 0 ? (float)_contestedFirstClashes / _maxRounds : 0f;
        float noFirstClashRate = _maxRounds > 0 ? (float)_noFirstClashes / _maxRounds : 0f;

        Debug.Log(
            "\n========== CALIBRATION FINISHED ==========\n" +
            $"Configuration: {_configurationName}\n" +
            $"ML Model: {_mlModelName}\n" +
            $"FSM Version: {_fsmVersionName}\n" +
            $"Rounds: {_maxRounds}\n\n" +

            $"FSM Wins: {_fsmWins} ({fsmWinRate:P1})\n" +
            $"ML Wins: {_mlWins} ({mlWinRate:P1})\n" +
            $"Draws: {_draws} ({drawRate:P1})\n\n" +

            $"FSM First Clashes: {_fsmFirstClashes} ({fsmFirstClashRate:P1})\n" +
            $"ML First Clashes: {_mlFirstClashes} ({mlFirstClashRate:P1})\n" +
            $"Contested First Clashes: {_contestedFirstClashes} ({contestedFirstClashRate:P1})\n" +
            $"No First Clash: {_noFirstClashes} ({noFirstClashRate:P1})\n\n" +

            $"CSV: {_csvPath}\n" +
            "=========================================="
        );
    }

    private void PrepareCsv()
    {
        if (!_writeCsv)
            return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{_fileNamePrefix}_{_configurationName}_{timestamp}.csv";

        _csvPath = Path.Combine(Application.persistentDataPath, fileName);

        _writer = new StreamWriter(_csvPath, false, Encoding.UTF8);

        // Подсказка Excel: использовать ; как разделитель столбцов.
        _writer.WriteLine("sep=;");

        string header = string.Join(CsvSeparator, new string[]
        {
            "round",
            "configuration",
            "ml_model",
            "fsm_version",
            "result",
            "duration_seconds",

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
        });

        _writer.WriteLine(header);
        _writer.Flush();

        if (_logToConsole)
        {
            Debug.Log($"[CalibrationManager] CSV created: {_csvPath}");
        }
    }

    private void WriteRoundToCsv(int round, RoundResult result, float duration)
    {
        if (!_writeCsv || _writer == null)
            return;

        string firstClashTimeText = _firstClashRecorded
            ? _firstClashTime.ToString("F4", CsvCulture)
            : "";

        string line = string.Join(CsvSeparator, new string[]
        {
            round.ToString(CsvCulture),
            EscapeCsvCell(_configurationName),
            EscapeCsvCell(_mlModelName),
            EscapeCsvCell(_fsmVersionName),
            result.ToString(),
            duration.ToString("F4", CsvCulture),

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

        _writer.WriteLine(line);
        _writer.Flush();
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

    private void CloseCsv()
    {
        if (_writer == null)
            return;

        _writer.Flush();
        _writer.Close();
        _writer = null;
    }
}