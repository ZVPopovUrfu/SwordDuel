using UnityEngine;

public class SwordClashReporter : MonoBehaviour
{
    [Header("Calibration")]
    [SerializeField] private CalibrationManager _calibrationManager;
    [SerializeField] private CalibrationManager.AgentSide _side;

    [Header("References")]
    [SerializeField] private SwordPhysics _swordPhysics;

    private void Awake()
    {
        if (_calibrationManager == null)
            _calibrationManager = FindObjectOfType<CalibrationManager>();

        if (_swordPhysics == null)
            _swordPhysics = GetComponent<SwordPhysics>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_calibrationManager == null || _swordPhysics == null)
            return;

        SwordClashReporter otherReporter = GetOtherReporter(collision);
        SwordPhysics otherPhysics = GetOtherSwordPhysics(collision);

        if (otherReporter == null || otherPhysics == null)
            return;

        if (otherReporter == this)
            return;

        if (collision.contactCount <= 0)
            return;

        ContactPoint2D contact = collision.GetContact(0);
        Vector2 contactPoint = contact.point;

        string myPart = _swordPhysics.GetCollisionPartString(contactPoint);
        string otherPart = otherPhysics.GetCollisionPartString(contactPoint);

        _calibrationManager.ReportSwordClash(
            _side,
            myPart,
            otherReporter.Side,
            otherPart
        );
    }

    private SwordClashReporter GetOtherReporter(Collision2D collision)
    {
        if (collision.rigidbody != null)
        {
            SwordClashReporter reporterFromRb =
                collision.rigidbody.GetComponent<SwordClashReporter>();

            if (reporterFromRb != null)
                return reporterFromRb;
        }

        if (collision.collider != null)
        {
            SwordClashReporter reporterFromCollider =
                collision.collider.GetComponentInParent<SwordClashReporter>();

            if (reporterFromCollider != null)
                return reporterFromCollider;
        }

        return null;
    }

    private SwordPhysics GetOtherSwordPhysics(Collision2D collision)
    {
        if (collision.rigidbody != null)
        {
            SwordPhysics physicsFromRb =
                collision.rigidbody.GetComponent<SwordPhysics>();

            if (physicsFromRb != null)
                return physicsFromRb;
        }

        if (collision.collider != null)
        {
            SwordPhysics physicsFromCollider =
                collision.collider.GetComponentInParent<SwordPhysics>();

            if (physicsFromCollider != null)
                return physicsFromCollider;
        }

        return null;
    }

    public CalibrationManager.AgentSide Side => _side;
}