using UnityEngine;

public class UserTestHitReporter : MonoBehaviour
{
    public enum Side
    {
        Player,
        Enemy
    }

    [Header("Setup")]
    [SerializeField] private UserTestRoundManager _manager;
    [SerializeField] private Side _side;

    [Header("Target")]
    [SerializeField] private string _targetCharacterTag;

    [Header("Hit Settings")]
    [SerializeField] private float _hitCooldown = 0.15f;

    private float _lastHitTime;

    private void Awake()
    {
        if (_manager == null)
            _manager = FindFirstObjectByType<UserTestRoundManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReportHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryReportHit(other);
    }

    private void TryReportHit(Collider2D other)
    {
        if (_manager == null)
            return;

        if (other == null)
            return;

        if (Time.time - _lastHitTime < _hitCooldown)
            return;

        if (!other.CompareTag(_targetCharacterTag))
            return;

        if (!IsBladeOrTipTouching(other))
            return;

        _lastHitTime = Time.time;

        if (_side == Side.Player)
            _manager.ReportPlayerHit();
        else
            _manager.ReportEnemyHit();
    }

    private bool IsBladeOrTipTouching(Collider2D characterCollider)
    {
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();

        foreach (Collider2D col in myColliders)
        {
            if (col == null)
                continue;

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
}
