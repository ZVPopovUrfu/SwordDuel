using System.Collections.Generic;
using UnityEngine;

public class SwordCollisionHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float collisionCooldown = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool logCollisions = false;
    [SerializeField] private bool logWarnings = true;

    private SwordPhysics swordPhysics;

    private static readonly Dictionary<long, float> LastPairCollisionTime = new Dictionary<long, float>();

    public System.Action<string, string, Vector2, SwordPhysics> OnZoneCollision;

    private void Awake()
    {
        swordPhysics = GetComponentInParent<SwordPhysics>();

        if (swordPhysics == null && logWarnings)
        {
            Debug.LogWarning($"[SwordCollisionHandler] {gameObject.name}: SwordPhysics не найден в родителях.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryProcessCollision(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Нужен на случай, если после reset/спавна мечи уже пересекаются,
        // и OnTriggerEnter2D не сработал ожидаемо.
        TryProcessCollision(other);
    }

    private void TryProcessCollision(Collider2D other)
    {
        if (other == null)
            return;

        if (swordPhysics == null)
            return;

        SwordCollisionHandler otherZone = other.GetComponent<SwordCollisionHandler>();

        if (otherZone == null)
            return;

        SwordPhysics otherPhysics = otherZone.GetSwordPhysics();

        if (otherPhysics == null)
            return;

        if (otherPhysics == swordPhysics)
            return;

        string myZone = gameObject.tag;
        string otherZoneTag = otherZone.gameObject.tag;

        if (!IsValidZoneTag(myZone))
        {
            if (logWarnings)
                Debug.LogWarning($"[SwordCollisionHandler] {gameObject.name}: некорректный тег зоны: {myZone}");

            return;
        }

        if (!IsValidZoneTag(otherZoneTag))
        {
            if (logWarnings)
                Debug.LogWarning($"[SwordCollisionHandler] {otherZone.gameObject.name}: некорректный тег зоны: {otherZoneTag}");

            return;
        }

        long pairKey = MakePairKey(swordPhysics.GetInstanceID(), otherPhysics.GetInstanceID());

        if (LastPairCollisionTime.TryGetValue(pairKey, out float lastTime))
        {
            if (Time.time - lastTime < collisionCooldown)
                return;
        }

        LastPairCollisionTime[pairKey] = Time.time;

        Vector2 contactPoint = other.ClosestPoint(transform.position);

        if (logCollisions)
        {
            Debug.Log(
                $"[SwordCollisionHandler] {swordPhysics.name}:{myZone} vs " +
                $"{otherPhysics.name}:{otherZoneTag} at {contactPoint}"
            );
        }

        swordPhysics.OnSwordCollision(otherPhysics, contactPoint, myZone, otherZoneTag);
        OnZoneCollision?.Invoke(myZone, otherZoneTag, contactPoint, otherPhysics);
    }

    public SwordPhysics GetSwordPhysics()
    {
        if (swordPhysics == null)
            swordPhysics = GetComponentInParent<SwordPhysics>();

        return swordPhysics;
    }

    private bool IsValidZoneTag(string zoneTag)
    {
        return zoneTag == "HandleZone" ||
               zoneTag == "BladeZone" ||
               zoneTag == "TipZone";
    }

    private long MakePairKey(int a, int b)
    {
        int min = Mathf.Min(a, b);
        int max = Mathf.Max(a, b);

        return ((long)min << 32) ^ (uint)max;
    }
}