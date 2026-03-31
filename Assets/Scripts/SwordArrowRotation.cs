using UnityEngine;
using UnityEngine.InputSystem;

public class SwordArrowRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 180f; // Скорость вращения
    [SerializeField] private float moveSpeed = 5f; // Скорость движения WASD

    [Header("Smooth Settings")]
    [SerializeField] private float acceleration = 10f; // Плавное ускорение
    [SerializeField] private float deceleration = 15f; // Плавное замедление

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;

    [Header("Sword Physics")]
    [SerializeField] private SwordPhysics swordPhysics;
    [SerializeField] private SwordCollisionHandler collisionHandler;

    // Компоненты
    private Rigidbody2D rb;

    // Input Actions
    private InputAction moveAction;
    private InputAction rotateAction;

    // Переменные
    private Vector2 movementInput;
    private float rotationInput;
    private Vector2 currentVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = false; // ← ИЗМЕНИТЬ НА false (важно!)
            rb.linearDamping = 2f;      // Уменьшаем阻尼 для более отзывчивого управления
            rb.angularDamping = 2f;
        }

        SetupInput();

        if (swordPhysics == null)
            swordPhysics = GetComponent<SwordPhysics>();

        if (collisionHandler == null)
            collisionHandler = GetComponent<SwordCollisionHandler>();

        // Убеждаемся, что коллайдер настроен правильно
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false; // Должно быть false для физических столкновений
        }
    }

    void SetupInput()
    {
        // Используем PlayerInput если он есть
        if (playerInput != null)
        {
            // Получаем InputActionAsset из PlayerInput
            InputActionAsset asset = playerInput.actions;

            // Находим Action Map (обычно "Gameplay" или первый доступный)
            InputActionMap actionMap = null;

            // Пытаемся найти map с названием "Gameplay"
            actionMap = asset.FindActionMap("Gameplay");

            // Если не нашли, берем первый доступный
            if (actionMap == null && asset.actionMaps.Count > 0)
            {
                actionMap = asset.actionMaps[0];
            }

            if (actionMap != null)
            {
                // Получаем действия из Action Map
                moveAction = actionMap.FindAction("Movement");
                rotateAction = actionMap.FindAction("Rotate");
            }
        }

        // Если действия не найдены - создаем резервные
        if (moveAction == null)
            CreateMovementAction();

        if (rotateAction == null)
            CreateRotationAction();
    }

    void CreateMovementAction()
    {
        // Создаем действие для движения WASD
        moveAction = new InputAction("Movement", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.Enable();
    }

    void CreateRotationAction()
    {
        // Создаем действие для вращения стрелками
        rotateAction = new InputAction("Rotate", InputActionType.Value);
        rotateAction.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/rightArrow")
            .With("Negative", "<Keyboard>/leftArrow");
        rotateAction.Enable();
    }

    void Update()
    {
        // Получаем ввод движения (WASD)
        if (moveAction != null)
            movementInput = moveAction.ReadValue<Vector2>();

        // Получаем ввод вращения (Стрелки)
        if (rotateAction != null)
            rotationInput = rotateAction.ReadValue<float>();
    }

    void FixedUpdate()
    {
        // 1. Движение WASD
        MoveSword();

        // 2. Вращение стрелками через физику (ВАЖНО!)
        RotateSword();
    }

    void MoveSword()
    {
        if (rb == null) return;

        Vector2 moveForce = movementInput * moveSpeed;
        rb.linearVelocity = moveForce;
    }

    void RotateSword()
    {
        if (rb == null || rotationInput == 0) return;

        // Вращаем через физику
        float rotationAmount = -rotationInput * rotationSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation + rotationAmount);
    }

    void OnEnable()
    {
        moveAction?.Enable();
        rotateAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        rotateAction?.Disable();
    }
}