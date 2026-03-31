using UnityEngine;

public class SwordPhysics : MonoBehaviour
{
    [Header("Sword Parts")]
    [SerializeField] private Transform tipPoint;      // Точка кончика меча
    [SerializeField] private Transform handlePoint;   // Точка рукояти меча
    [SerializeField] private float swordLength = 2f;  // Длина меча

    [Header("Physics Settings")]
    [SerializeField] private float knockbackDistance = 1.5f;
    [SerializeField] private float knockbackDuration = 0.15f;
    [SerializeField] private float rotationKnockback = 60f;

    [Header("Knockback Multipliers")]
    [SerializeField] private float strongKnockback = 1.0f;
    [SerializeField] private float weakKnockback = 0.3f;
    [SerializeField] private float noKnockback = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Rigidbody2D rb;
    private bool isKnockedBack = false;
    private float knockbackTimer = 0f;
    private Vector2 knockbackStartPosition;
    private Vector2 knockbackTargetPosition;
    private float knockbackStartRotation;
    private float knockbackTargetRotation;
    private float currentKnockbackDuration;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError($"{gameObject.name}: Rigidbody2D не найден!");
        }
    }

    void Update()
    {
        if (isKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;

            if (knockbackTimer > 0)
            {
                float t = 1f - (knockbackTimer / currentKnockbackDuration);
                t = Mathf.SmoothStep(0, 1, t);

                transform.position = Vector2.Lerp(knockbackStartPosition, knockbackTargetPosition, t);
                transform.rotation = Quaternion.Lerp(
                    Quaternion.Euler(0, 0, knockbackStartRotation),
                    Quaternion.Euler(0, 0, knockbackTargetRotation),
                    t);
            }
            else
            {
                isKnockedBack = false;
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }
        }
    }

    // ========== МЕТОД ДЛЯ ОПРЕДЕЛЕНИЯ ЧАСТИ МЕЧА ==========
    public CollisionPart GetCollisionPart(Vector2 worldPoint)
    {
        if (handlePoint == null || tipPoint == null)
        {
            Debug.LogWarning($"{gameObject.name}: handlePoint или tipPoint не назначены!");
            return CollisionPart.Blade;
        }

        Vector2 handlePos = handlePoint.position;
        Vector2 tipPos = tipPoint.position;

        float totalLength = Vector2.Distance(handlePos, tipPos);
        float distanceFromHandle = Vector2.Distance(handlePos, worldPoint);

        float t = distanceFromHandle / totalLength;
        t = Mathf.Clamp01(t);

        if (t < 0.2f)
            return CollisionPart.Handle;
        else if (t < 0.7f)
            return CollisionPart.Blade;
        else
            return CollisionPart.Tip;
    }

    public int GetCollisionPartInt(Vector2 worldPoint)
    {
        CollisionPart part = GetCollisionPart(worldPoint);

        switch (part)
        {
            case CollisionPart.Handle: return 0;
            case CollisionPart.Blade: return 1;
            case CollisionPart.Tip: return 2;
            default: return 1;
        }
    }

    public string GetCollisionPartString(Vector2 worldPoint)
    {
        return GetCollisionPart(worldPoint).ToString();
    }

    public void OnSwordCollision(SwordPhysics otherSword,
                                  Vector2 contactPoint,
                                  string myZone,
                                  string otherZone)
    {
        if (isKnockedBack) return;

        if (showDebugLogs)
        {
            Debug.Log($"⚔️ {gameObject.name}: {myZone} vs {otherSword.name}: {otherZone}");
        }

        Vector2 directionFromMe = (transform.position - otherSword.transform.position).normalized;
        Vector2 directionFromOther = -directionFromMe;

        float myMultiplier = noKnockback;
        float otherMultiplier = noKnockback;

        // ========== ЛОГИКА ПО ПРАВИЛАМ ==========

        if (myZone == "HandleZone" && otherZone == "HandleZone")
        {
            myMultiplier = noKnockback;
            otherMultiplier = noKnockback;
            if (showDebugLogs) Debug.Log($"   → Правило 4: Рукоять vs Рукоять → ничего");
        }
        else if (myZone == "HandleZone")
        {
            myMultiplier = strongKnockback;
            otherMultiplier = noKnockback;
            if (showDebugLogs) Debug.Log($"   → Правило 5: {gameObject.name} (рукоять) отлетает");
        }
        else if (otherZone == "HandleZone")
        {
            myMultiplier = noKnockback;
            otherMultiplier = strongKnockback;
            if (showDebugLogs) Debug.Log($"   → Правило 5: {otherSword.name} (рукоять) отлетает");
        }
        else if (myZone == "TipZone" && otherZone == "TipZone")
        {
            myMultiplier = strongKnockback;
            otherMultiplier = strongKnockback;
            if (showDebugLogs) Debug.Log($"   → Правило 1: Кончик vs Кончик → оба сильно");
        }
        else if (myZone == "BladeZone" && otherZone == "BladeZone")
        {
            myMultiplier = strongKnockback;
            otherMultiplier = strongKnockback;
            if (showDebugLogs) Debug.Log($"   → Правило 3: Лезвие vs Лезвие → оба сильно");
        }
        else if (myZone == "TipZone" && otherZone == "BladeZone")
        {
            myMultiplier = strongKnockback;
            otherMultiplier = weakKnockback;
            if (showDebugLogs) Debug.Log($"   → Правило 2: Кончик ({gameObject.name}) отлетает сильно, Лезвие ({otherSword.name}) слабо");
        }
        else if (myZone == "BladeZone" && otherZone == "TipZone")
        {
            myMultiplier = weakKnockback;
            otherMultiplier = strongKnockback;
            if (showDebugLogs) Debug.Log($"   → Правило 2: Лезвие ({gameObject.name}) отлетает слабо, Кончик ({otherSword.name}) сильно");
        }

        if (myMultiplier > 0)
        {
            ApplyKnockback(myMultiplier, directionFromMe);
        }

        if (otherMultiplier > 0)
        {
            otherSword.ApplyKnockback(otherMultiplier, directionFromOther);
        }

        if (showDebugLogs)
        {
            Debug.Log($"   → {gameObject.name}: множитель {myMultiplier:F1}");
            Debug.Log($"   → {otherSword.name}: множитель {otherMultiplier:F1}");
        }
    }

    public void ApplyKnockback(float multiplier, Vector2 direction)
    {
        if (multiplier <= 0.05f) return;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        knockbackStartPosition = transform.position;
        knockbackStartRotation = transform.eulerAngles.z;

        float distance = knockbackDistance * multiplier;
        knockbackTargetPosition = knockbackStartPosition + direction * distance;

        knockbackTargetPosition.x = Mathf.Clamp(knockbackTargetPosition.x, -9f, 9f);
        knockbackTargetPosition.y = Mathf.Clamp(knockbackTargetPosition.y, -4.5f, 4.5f);

        float rotationDir = direction.x > 0 ? 1 : -1;
        float rotationAmount = rotationKnockback * multiplier * rotationDir;
        knockbackTargetRotation = knockbackStartRotation + rotationAmount;

        isKnockedBack = true;
        knockbackTimer = knockbackDuration;
        currentKnockbackDuration = knockbackDuration;

        if (showDebugLogs)
        {
            Debug.Log($"   → {gameObject.name}: отлетает на {distance:F1} (множитель {multiplier:F1})");
        }
    }

    public bool IsKnockedBack()
    {
        return isKnockedBack;
    }

    void OnDrawGizmos()
    {
        if (handlePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(handlePoint.position, 0.1f);
        }

        if (tipPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(tipPoint.position, 0.1f);
        }

        if (handlePoint != null && tipPoint != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(handlePoint.position, tipPoint.position);
        }
    }
}

// ========== ENUM (ДОЛЖЕН БЫТЬ ВНЕ КЛАССА!) ==========
public enum CollisionPart
{
    Handle,
    Blade,
    Tip
}