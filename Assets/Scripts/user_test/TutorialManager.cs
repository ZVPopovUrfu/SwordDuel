using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    public enum TutorialStep
    {
        BasicControl = 0,
        SwordParts = 1,
        EnemyDefense = 2,
        EnemyAttack = 3,
        Completed = 4
    }

    [Serializable]
    public class TutorialStepConfig
    {
        [TextArea(2, 6)] public string instructionText;
        [TextArea(1, 4)] public string objectiveText;
        public GameObject[] enableObjects;
        public GameObject[] disableObjects;
        public Transform playerSwordSpawn;
        public Transform enemySwordSpawn;
        public Transform playerCharacterSpawn;
        public Transform enemyCharacterSpawn;
    }

    [Serializable]
    private class ClashObjective
    {
        public string playerPart;
        public string enemyPart;
        public string label;
        [NonSerialized] public bool completed;
    }

    [Header("Steps")]
    [SerializeField] private TutorialStepConfig[] steps = new TutorialStepConfig[4];
    [SerializeField] private TutorialStep startStep = TutorialStep.BasicControl;

    [Header("Objects To Reset")]
    [SerializeField] private Transform playerSword;
    [SerializeField] private Transform enemySword;
    [SerializeField] private Transform playerCharacter;
    [SerializeField] private Transform enemyCharacter;

    [Header("UI")]
    [SerializeField] private Text stepTitleText;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text objectiveText;
    [SerializeField] private Text progressText;
    [SerializeField] private GameObject stepCompletePanel;
    [SerializeField] private Text stepCompleteText;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";
    [SerializeField] private KeyCode continueKey = KeyCode.Return;
    [SerializeField] private KeyCode restartStepKey = KeyCode.R;

    [Header("Step 2: Sword Parts")]
    [SerializeField] private int requiredSwordPartObjectives = 5;

    [Header("Step 4: Enemy Attack")]
    [SerializeField] private int requiredSuccessfulBlocks = 3;

    [Header("Debug")]
    [SerializeField] private bool logEvents = true;
    [SerializeField] private bool allowDebugHotkeys = true;

    private TutorialStep _currentStep;
    private bool _stepCompleted;
    private int _successfulBlocks;

    private readonly List<ClashObjective> _clashObjectives = new List<ClashObjective>
    {
        new ClashObjective { playerPart = "Tip",    enemyPart = "Handle", label = "кончиком по рукоятке врага" },
        new ClashObjective { playerPart = "Tip",    enemyPart = "Blade",  label = "кончиком по лезвию врага" },
        new ClashObjective { playerPart = "Tip",    enemyPart = "Tip",    label = "кончиком по кончику врага" },
        new ClashObjective { playerPart = "Blade",  enemyPart = "Tip",    label = "лезвием по кончику врага" },
        new ClashObjective { playerPart = "Blade",  enemyPart = "Handle", label = "лезвием по рукоятке врага" },
        new ClashObjective { playerPart = "Handle", enemyPart = "Blade",  label = "рукояткой по лезвию врага" },
        new ClashObjective { playerPart = "Handle", enemyPart = "Tip",    label = "рукояткой по кончику врага" }
    };

    public TutorialStep CurrentStep => _currentStep;
    public bool StepCompleted => _stepCompleted;

    private void Start()
    {
        FillDefaultTextsIfEmpty();
        StartTutorialStep(startStep);
    }

    private void Update()
    {
        if (_stepCompleted && Input.GetKeyDown(continueKey))
        {
            GoToNextStep();
        }

        if (Input.GetKeyDown(restartStepKey))
        {
            RestartCurrentStep();
        }

        if (allowDebugHotkeys)
        {
            if (Input.GetKeyDown(KeyCode.F1)) StartTutorialStep(TutorialStep.BasicControl);
            if (Input.GetKeyDown(KeyCode.F2)) StartTutorialStep(TutorialStep.SwordParts);
            if (Input.GetKeyDown(KeyCode.F3)) StartTutorialStep(TutorialStep.EnemyDefense);
            if (Input.GetKeyDown(KeyCode.F4)) StartTutorialStep(TutorialStep.EnemyAttack);
            if (Input.GetKeyDown(KeyCode.F8)) CompleteCurrentStep();
        }
    }

    public void StartTutorialStep(TutorialStep step)
    {
        _currentStep = step;
        _stepCompleted = false;
        _successfulBlocks = 0;

        ResetClashObjectives();
        ApplyStepConfig(step);
        ResetSceneObjectsForStep(step);
        UpdateUI();
        SetStepCompletePanel(false, "");

        if (logEvents)
            Debug.Log($"[TutorialManager] Started step: {_currentStep}");
    }

    public void RestartCurrentStep()
    {
        StartTutorialStep(_currentStep);
    }

    public void GoToNextStep()
    {
        if (_currentStep == TutorialStep.Completed)
        {
            LoadMainMenu();
            return;
        }

        int next = (int)_currentStep + 1;

        if (next >= (int)TutorialStep.Completed)
        {
            _currentStep = TutorialStep.Completed;
            _stepCompleted = true;
            UpdateUI();
            SetStepCompletePanel(true, "Обучение завершено. Нажмите Enter, чтобы вернуться в меню.");
            return;
        }

        StartTutorialStep((TutorialStep)next);
    }

    public void CompleteCurrentStep()
    {
        if (_stepCompleted)
            return;

        _stepCompleted = true;
        UpdateUI();
        SetStepCompletePanel(true, "Отлично! Нажмите Enter, чтобы продолжить.");

        if (logEvents)
            Debug.Log($"[TutorialManager] Completed step: {_currentStep}");
    }

    // Step 1 and Step 3: call this when player hits enemy character with blade/tip.
    public void NotifyEnemyCharacterHit()
    {
        if (_currentStep == TutorialStep.BasicControl || _currentStep == TutorialStep.EnemyDefense)
        {
            CompleteCurrentStep();
        }
    }

    // Step 2: call this when player sword collides with enemy sword.
    // Expected part names: "Tip", "Blade", "Handle".
    public void NotifyClash(string playerPart, string enemyPart)
    {
        if (_currentStep != TutorialStep.SwordParts || _stepCompleted)
            return;

        bool changed = false;

        foreach (ClashObjective objective in _clashObjectives)
        {
            if (objective.completed)
                continue;

            if (PartEquals(objective.playerPart, playerPart) && PartEquals(objective.enemyPart, enemyPart))
            {
                objective.completed = true;
                changed = true;

                if (logEvents)
                    Debug.Log($"[TutorialManager] Clash objective completed: {objective.label}");

                break;
            }
        }

        if (changed)
            UpdateUI();

        if (GetCompletedClashObjectiveCount() >= requiredSwordPartObjectives)
            CompleteCurrentStep();
    }

    // Step 4: call this when player successfully blocks/deflects enemy sword.
    public void NotifyEnemyAttackBlocked()
    {
        if (_currentStep != TutorialStep.EnemyAttack || _stepCompleted)
            return;

        _successfulBlocks++;

        if (logEvents)
            Debug.Log($"[TutorialManager] Successful blocks: {_successfulBlocks}/{requiredSuccessfulBlocks}");

        UpdateUI();

        if (_successfulBlocks >= requiredSuccessfulBlocks)
            CompleteCurrentStep();
    }

    // Step 4: call this when enemy sword hits player character.
    public void NotifyPlayerCharacterHitByEnemy()
    {
        if (_currentStep != TutorialStep.EnemyAttack || _stepCompleted)
            return;

        if (logEvents)
            Debug.Log("[TutorialManager] Player was hit. Restarting enemy attack step.");

        RestartCurrentStep();
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("[TutorialManager] Main menu scene name is empty.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ApplyStepConfig(TutorialStep step)
    {
        int index = (int)step;

        if (steps == null || index < 0 || index >= steps.Length || steps[index] == null)
            return;

        TutorialStepConfig config = steps[index];

        SetObjectsActive(config.disableObjects, false);
        SetObjectsActive(config.enableObjects, true);
    }

    private void ResetSceneObjectsForStep(TutorialStep step)
    {
        int index = (int)step;

        if (steps == null || index < 0 || index >= steps.Length || steps[index] == null)
            return;

        TutorialStepConfig config = steps[index];

        ResetTransform(playerSword, config.playerSwordSpawn);
        ResetTransform(enemySword, config.enemySwordSpawn);
        ResetTransform(playerCharacter, config.playerCharacterSpawn);
        ResetTransform(enemyCharacter, config.enemyCharacterSpawn);
    }

    private void ResetTransform(Transform target, Transform spawn)
    {
        if (target == null || spawn == null)
            return;

        target.position = spawn.position;
        target.rotation = spawn.rotation;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    private void ResetClashObjectives()
    {
        foreach (ClashObjective objective in _clashObjectives)
            objective.completed = false;
    }

    private int GetCompletedClashObjectiveCount()
    {
        int count = 0;

        foreach (ClashObjective objective in _clashObjectives)
        {
            if (objective.completed)
                count++;
        }

        return count;
    }

    private void UpdateUI()
    {
        if (stepTitleText != null)
            stepTitleText.text = GetStepTitle(_currentStep);

        int index = (int)_currentStep;
        TutorialStepConfig config = steps != null && index >= 0 && index < steps.Length ? steps[index] : null;

        if (instructionText != null)
            instructionText.text = config != null ? config.instructionText : "";

        if (objectiveText != null)
            objectiveText.text = config != null ? config.objectiveText : "";

        if (progressText != null)
            progressText.text = GetProgressText();
    }

    private string GetProgressText()
    {
        switch (_currentStep)
        {
            case TutorialStep.SwordParts:
                return GetSwordPartsProgressText();

            case TutorialStep.EnemyAttack:
                return $"Успешные отбивания: {_successfulBlocks}/{requiredSuccessfulBlocks}";

            case TutorialStep.Completed:
                return "Обучение завершено.";

            default:
                return _stepCompleted ? "Этап выполнен." : "";
        }
    }

    private string GetSwordPartsProgressText()
    {
        string result = $"Выполнено: {GetCompletedClashObjectiveCount()}/{requiredSwordPartObjectives}\n";

        foreach (ClashObjective objective in _clashObjectives)
        {
            string mark = objective.completed ? "[x]" : "[ ]";
            result += $"{mark} {objective.label}\n";
        }

        return result;
    }

    private string GetStepTitle(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.BasicControl:
                return "Этап 1: управление и цель боя";
            case TutorialStep.SwordParts:
                return "Этап 2: части меча и столкновения";
            case TutorialStep.EnemyDefense:
                return "Этап 3: защита врага";
            case TutorialStep.EnemyAttack:
                return "Этап 4: атака врага";
            case TutorialStep.Completed:
                return "Обучение завершено";
            default:
                return "Обучение";
        }
    }

    private void SetStepCompletePanel(bool active, string text)
    {
        if (stepCompletePanel != null)
            stepCompletePanel.SetActive(active);

        if (stepCompleteText != null)
            stepCompleteText.text = text;
    }

    private bool PartEquals(string expected, string actual)
    {
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private void FillDefaultTextsIfEmpty()
    {
        if (steps == null || steps.Length < 4)
            Array.Resize(ref steps, 4);

        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == null)
                steps[i] = new TutorialStepConfig();
        }

        if (string.IsNullOrWhiteSpace(steps[0].instructionText))
        {
            steps[0].instructionText = "Перемещение: WASD.\nВращение меча: стрелки влево/вправо.\n\nЦель боя — достичь персонажа врага и атаковать его кончиком или лезвием меча.";
            steps[0].objectiveText = "Цель: атакуйте персонажа врага.";
        }

        if (string.IsNullOrWhiteSpace(steps[1].instructionText))
        {
            steps[1].instructionText = "Механика фехтования основана на грамотном попадании по разным частям меча врага.\n\nУ меча есть кончик, лезвие и рукоять. Разные столкновения дают разный результат.";
            steps[1].objectiveText = "Цель: выполните несколько разных столкновений с мечом врага.";
        }

        if (string.IsNullOrWhiteSpace(steps[2].instructionText))
        {
            steps[2].instructionText = "Вражеский меч может защищаться и не давать атаковать своего персонажа.";
            steps[2].objectiveText = "Цель: пройдите защиту врага и атакуйте персонажа.";
        }

        if (string.IsNullOrWhiteSpace(steps[3].instructionText))
        {
            steps[3].instructionText = "Вражеский меч может атаковать вашего персонажа.\n\nСтарайтесь отбивать его меч и не давать ему коснуться вашего персонажа.";
            steps[3].objectiveText = "Цель: успешно отбейте несколько атак врага.";
        }
    }
}
