using UnityEngine;

public class SwordCombatContext : MonoBehaviour
{
    [Header("Self")]
    [SerializeField] private Transform _mySword;
    [SerializeField] private Transform _myCharacter;
    [SerializeField] private Transform _myTipPoint;
    [SerializeField] private Transform _myHandlePoint;
    [SerializeField] private Rigidbody2D _myRb;

    [Header("Opponent")]
    [SerializeField] private Transform _opponentSword;
    [SerializeField] private Transform _opponentCharacter;
    [SerializeField] private Transform _opponentTipPoint;
    [SerializeField] private Transform _opponentHandlePoint;
    [SerializeField] private Rigidbody2D _opponentRb;

    [Header("Targets")]
    [SerializeField] private Transform _opponentAttackPoint;
    [SerializeField] private Vector2 _fallbackAttackOffset = new Vector2(0f, 1.0f);

    [Header("Arena")]
    [SerializeField] private Transform _arenaCenter;
    [SerializeField] private float _arenaWidth = 20f;
    [SerializeField] private float _arenaHeight = 10f;
    [SerializeField] private float _arenaPadding = 0.5f;

    public Vector2 MySwordPos => _mySword != null ? (Vector2)_mySword.position : (Vector2)transform.position;
    public Vector2 MyCharacterPos => _myCharacter != null ? (Vector2)_myCharacter.position : Vector2.zero;
    public Vector2 MyTipPos => _myTipPoint != null ? (Vector2)_myTipPoint.position : MySwordPos;
    public Vector2 MyHandlePos => _myHandlePoint != null ? (Vector2)_myHandlePoint.position : MySwordPos;

    public Vector2 OpponentSwordPos => _opponentSword != null ? (Vector2)_opponentSword.position : Vector2.zero;
    public Vector2 OpponentCharacterPos => _opponentCharacter != null ? (Vector2)_opponentCharacter.position : Vector2.zero;
    public Vector2 OpponentTipPos => _opponentTipPoint != null ? (Vector2)_opponentTipPoint.position : OpponentSwordPos;
    public Vector2 OpponentHandlePos => _opponentHandlePoint != null ? (Vector2)_opponentHandlePoint.position : OpponentSwordPos;

    public Vector2 ArenaCenterPos => _arenaCenter != null ? (Vector2)_arenaCenter.position : Vector2.zero;

    public Vector2 MyVelocity => _myRb != null ? _myRb.linearVelocity : Vector2.zero;
    public Vector2 OpponentVelocity => _opponentRb != null ? _opponentRb.linearVelocity : Vector2.zero;

    public Vector2 MySwordDir
    {
        get
        {
            Vector2 dir = MyTipPos - MyHandlePos;
            if (dir.sqrMagnitude < 0.0001f) return Vector2.right;
            return dir.normalized;
        }
    }

    public Vector2 OpponentSwordDir
    {
        get
        {
            Vector2 dir = OpponentTipPos - OpponentHandlePos;
            if (dir.sqrMagnitude < 0.0001f) return Vector2.right;
            return dir.normalized;
        }
    }

    public Vector2 OpponentAttackTargetPos
    {
        get
        {
            if (_opponentAttackPoint != null)
                return _opponentAttackPoint.position;

            if (_opponentCharacter != null)
            {
                Collider2D col = _opponentCharacter.GetComponentInChildren<Collider2D>();
                if (col != null)
                {
                    Bounds b = col.bounds;
                    return new Vector2(b.center.x, b.center.y + b.size.y * 0.15f);
                }

                SpriteRenderer sr = _opponentCharacter.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    Bounds b = sr.bounds;
                    return new Vector2(b.center.x, b.center.y + b.size.y * 0.15f);
                }

                return (Vector2)_opponentCharacter.position + _fallbackAttackOffset;
            }

            return Vector2.zero;
        }
    }

    public float MyThreatDistance =>
        Vector2.Distance(MyTipPos, OpponentAttackTargetPos);

    public float OpponentThreatDistance =>
        Vector2.Distance(OpponentTipPos, MyCharacterPos);

    // > 0: я ближе к цели, чем противник к моему персонажу.
    // < 0: противник опаснее.
    public float ThreatAdvantage =>
        OpponentThreatDistance - MyThreatDistance;

    public Vector2 OpponentTipToMyCharacter =>
        MyCharacterPos - OpponentTipPos;

    public Vector2 MyTipToOpponentCharacter =>
        OpponentAttackTargetPos - MyTipPos;

    public float OpponentAimAtMyCharacterScore
    {
        get
        {
            Vector2 toMyChar = MyCharacterPos - OpponentHandlePos;
            if (toMyChar.sqrMagnitude < 0.0001f) return 0f;
            return Vector2.Dot(OpponentSwordDir, toMyChar.normalized);
        }
    }

    public float MyAimAtOpponentScore
    {
        get
        {
            Vector2 toOpponent = OpponentAttackTargetPos - MyHandlePos;
            if (toOpponent.sqrMagnitude < 0.0001f) return 0f;
            return Vector2.Dot(MySwordDir, toOpponent.normalized);
        }
    }

    public Vector2 DistanceToArenaEdges01()
    {
        Vector2 center = ArenaCenterPos;

        float halfW = _arenaWidth * 0.5f - _arenaPadding;
        float halfH = _arenaHeight * 0.5f - _arenaPadding;

        float left = center.x - halfW;
        float right = center.x + halfW;
        float bottom = center.y - halfH;
        float top = center.y + halfH;

        float distLeft = Mathf.InverseLerp(0f, halfW * 2f, MySwordPos.x - left);
        float distRight = Mathf.InverseLerp(0f, halfW * 2f, right - MySwordPos.x);
        float distBottom = Mathf.InverseLerp(0f, halfH * 2f, MySwordPos.y - bottom);
        float distTop = Mathf.InverseLerp(0f, halfH * 2f, top - MySwordPos.y);

        return new Vector2(
            Mathf.Min(distLeft, distRight),
            Mathf.Min(distBottom, distTop)
        );
    }

    public Vector2 NormalizeArenaPosition(Vector2 worldPos)
    {
        Vector2 center = ArenaCenterPos;

        float halfW = Mathf.Max(0.01f, _arenaWidth * 0.5f);
        float halfH = Mathf.Max(0.01f, _arenaHeight * 0.5f);

        Vector2 local = worldPos - center;

        return new Vector2(
            Mathf.Clamp(local.x / halfW, -1f, 1f),
            Mathf.Clamp(local.y / halfH, -1f, 1f)
        );
    }

    public Vector2 ClampPointToArena(Vector2 worldPoint)
    {
        Vector2 center = ArenaCenterPos;

        float halfW = _arenaWidth * 0.5f - _arenaPadding;
        float halfH = _arenaHeight * 0.5f - _arenaPadding;

        worldPoint.x = Mathf.Clamp(worldPoint.x, center.x - halfW, center.x + halfW);
        worldPoint.y = Mathf.Clamp(worldPoint.y, center.y - halfH, center.y + halfH);

        return worldPoint;
    }

    public bool IsNearArenaEdge(Vector2 pos, float margin = 0.6f)
    {
        Vector2 center = ArenaCenterPos;

        float halfW = _arenaWidth * 0.5f - _arenaPadding;
        float halfH = _arenaHeight * 0.5f - _arenaPadding;

        float left = center.x - halfW;
        float right = center.x + halfW;
        float bottom = center.y - halfH;
        float top = center.y + halfH;

        return
            pos.x < left + margin ||
            pos.x > right - margin ||
            pos.y < bottom + margin ||
            pos.y > top - margin;
    }

    public void AddObservationsTo(Unity.MLAgents.Sensors.VectorSensor sensor)
    {
        // Все значения нормализуются относительно арены,
        // чтобы ML не зависел от абсолютных координат сцены.

        sensor.AddObservation(NormalizeArenaPosition(MySwordPos));
        sensor.AddObservation(NormalizeArenaPosition(MyTipPos));
        sensor.AddObservation(NormalizeArenaPosition(MyCharacterPos));

        sensor.AddObservation(NormalizeArenaPosition(OpponentSwordPos));
        sensor.AddObservation(NormalizeArenaPosition(OpponentTipPos));
        sensor.AddObservation(NormalizeArenaPosition(OpponentCharacterPos));
        sensor.AddObservation(NormalizeArenaPosition(OpponentAttackTargetPos));

        sensor.AddObservation(MySwordDir);
        sensor.AddObservation(OpponentSwordDir);

        sensor.AddObservation(MyVelocity / 5f);
        sensor.AddObservation(OpponentVelocity / 5f);

        sensor.AddObservation(Mathf.Clamp(MyThreatDistance / 20f, 0f, 1f));
        sensor.AddObservation(Mathf.Clamp(OpponentThreatDistance / 20f, 0f, 1f));
        sensor.AddObservation(Mathf.Clamp(ThreatAdvantage / 20f, -1f, 1f));

        sensor.AddObservation(Mathf.Clamp(OpponentAimAtMyCharacterScore, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(MyAimAtOpponentScore, -1f, 1f));

        sensor.AddObservation(DistanceToArenaEdges01());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(OpponentAttackTargetPos, 0.12f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(MyTipPos, OpponentAttackTargetPos);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(OpponentTipPos, MyCharacterPos);
    }
}