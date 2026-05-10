using UnityEngine;

public class HeadToHeadSwordClashReporter : MonoBehaviour
{
    [Header("Head-to-head")]
    [SerializeField] private HeadToHeadTestManager _manager;
    [SerializeField] private HeadToHeadTestManager.AgentKind _agentKind;

    [Header("References")]
    [SerializeField] private SwordPhysics _swordPhysics;

    public HeadToHeadTestManager.AgentKind AgentKind => _agentKind;

    private void Awake()
    {
        if (_manager == null)
            _manager = FindObjectOfType<HeadToHeadTestManager>();

        if (_swordPhysics == null)
            _swordPhysics = GetComponent<SwordPhysics>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_manager == null || _swordPhysics == null)
            return;

        HeadToHeadSwordClashReporter otherReporter = GetOtherReporter(collision);
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

        _manager.ReportSwordClash(
            _agentKind,
            myPart,
            otherReporter.AgentKind,
            otherPart
        );
    }

    private HeadToHeadSwordClashReporter GetOtherReporter(Collision2D collision)
    {
        if (collision.rigidbody != null)
        {
            HeadToHeadSwordClashReporter reporterFromRb =
                collision.rigidbody.GetComponent<HeadToHeadSwordClashReporter>();

            if (reporterFromRb != null)
                return reporterFromRb;
        }

        if (collision.collider != null)
        {
            HeadToHeadSwordClashReporter reporterFromCollider =
                collision.collider.GetComponentInParent<HeadToHeadSwordClashReporter>();

            if (reporterFromCollider != null)
                return reporterFromCollider;
        }

        return null;
    }

    private SwordPhysics GetOtherSwordPhysics(Collision2D collision)
    {
        if (collision.rigidbody != null)
        {
            SwordPhysics physicsFromRb = collision.rigidbody.GetComponent<SwordPhysics>();

            if (physicsFromRb != null)
                return physicsFromRb;
        }

        if (collision.collider != null)
        {
            SwordPhysics physicsFromCollider = collision.collider.GetComponentInParent<SwordPhysics>();

            if (physicsFromCollider != null)
                return physicsFromCollider;
        }

        return null;
    }
}
