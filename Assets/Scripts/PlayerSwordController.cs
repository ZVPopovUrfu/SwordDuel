using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSwordController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 150f;

    [Header("Smooth Settings")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 12f;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;

    [Header("References")]          
    [SerializeField] private NewGameManager _gameManager;  

    // Компоненты
    private Rigidbody2D rb;
    private Vector2 movementInput;
    private float rotationInput;
    private Vector2 currentVelocity;

    // Input Actions
    private InputAction moveAction;
    private InputAction rotateAction;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;      // Физика не вращает меч
            rb.linearDamping = 2f;
            rb.angularDamping = 2f;
        }

        SetupInput();
    }

    void SetupInput()
    {
        // Движение WASD
        moveAction = new InputAction("Movement", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.Enable();

        // Вращение стрелками (влево/вправо)
        rotateAction = new InputAction("Rotate", InputActionType.Value);
        rotateAction.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/rightArrow")
            .With("Negative", "<Keyboard>/leftArrow");
        rotateAction.Enable();
    }

    void Update()
    {
        // Получаем ввод
        movementInput = moveAction.ReadValue<Vector2>();
        rotationInput = rotateAction.ReadValue<float>();
    }

    void FixedUpdate()
    {
        MoveSword();
        RotateSword();
    }

    void MoveSword()
    {
        if (rb == null) return;

        // Целевая скорость
        Vector2 targetVelocity = movementInput * moveSpeed;

        // Плавное ускорение/замедление
        if (movementInput.magnitude > 0.1f)
        {
            currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = currentVelocity;
    }

    void RotateSword()
    {
        if (rb == null || rotationInput == 0) return;

        // Вращение
        float rotationAmount = rotationInput * rotationSpeed * Time.fixedDeltaTime;
        transform.Rotate(0, 0, -rotationAmount);  // - для интуитивного управления
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        if (!IsBladeOrTipTouchingCharacter(other))
            return;

        _gameManager?.OnPlayerHit();
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