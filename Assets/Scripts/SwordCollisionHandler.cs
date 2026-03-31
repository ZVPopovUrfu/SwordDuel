using UnityEngine;

public class SwordCollisionHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float collisionCooldown = 0.1f;

    private SwordPhysics swordPhysics;
    private float lastCollisionTime = 0f;
    private bool isProcessingCollision = false;

    public System.Action<string, string, Vector2, SwordPhysics> OnZoneCollision;

    void Start()
    {
        swordPhysics = GetComponentInParent<SwordPhysics>();

        if (swordPhysics == null)
        {
            Debug.LogError($"{gameObject.name}: SwordPhysics не найден!");
        }

        Debug.Log($"{gameObject.name}: инициализирован, тег = {gameObject.tag}, родитель = {transform.parent?.name}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isProcessingCollision) return;
        if (Time.time - lastCollisionTime < collisionCooldown) return;

        // Получаем SwordCollisionHandler зоны, с которой столкнулись
        SwordCollisionHandler otherZone = other.GetComponent<SwordCollisionHandler>();

        if (otherZone == null) return;

        // Получаем SwordPhysics у родителя другого объекта
        SwordPhysics otherPhysics = otherZone.swordPhysics;

        if (otherPhysics == null || otherPhysics == swordPhysics) return;

        // ПОЛУЧАЕМ ТЕГИ ЗОН (ВАЖНО! берем из компонента, а не из other.tag)
        string myZone = gameObject.tag;                    // Тег текущей зоны
        string otherZoneTag = otherZone.gameObject.tag;    // Тег зоны, с которой столкнулись

        // Проверяем, что это действительно зоны
        if (myZone != "HandleZone" && myZone != "BladeZone" && myZone != "TipZone")
        {
            Debug.LogWarning($"{gameObject.name}: неизвестный тег зоны '{myZone}'");
            return;
        }

        if (otherZoneTag != "HandleZone" && otherZoneTag != "BladeZone" && otherZoneTag != "TipZone")
        {
            Debug.LogWarning($"{otherZone.gameObject.name}: неизвестный тег зоны '{otherZoneTag}'");
            return;
        }

        Debug.Log($"🎯 {gameObject.name} ({myZone}) коснулся {otherZone.gameObject.name} ({otherZoneTag})");

        lastCollisionTime = Time.time;
        isProcessingCollision = true;

        // Получаем точку контакта
        Vector2 contactPoint = other.ClosestPoint(transform.position);

        // Обрабатываем столкновение (только один раз)
        if (swordPhysics != null && otherPhysics != null)
        {
            if (GetInstanceID() < other.gameObject.GetInstanceID())
            {
                swordPhysics.OnSwordCollision(otherPhysics, contactPoint, myZone, otherZoneTag);
                OnZoneCollision?.Invoke(myZone, otherZoneTag, contactPoint, otherPhysics);
            }
        }

        Invoke(nameof(ResetCollisionFlag), 0.05f);
    }

    void ResetCollisionFlag()
    {
        isProcessingCollision = false;
    }
}