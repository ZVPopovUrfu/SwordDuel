using UnityEngine;
using static SwordActionTypes;

public class SwordActionExecutor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _rotationSpeed = 150f;
    [SerializeField] private float _acceleration = 20f;
    [SerializeField] private float _deceleration = 8f;

    [Header("Arena Bounds")]
    [SerializeField] private Transform _arenaCenter;
    [SerializeField] private float _arenaWidth = 20f;
    [SerializeField] private float _arenaHeight = 10f;
    [SerializeField] private float _arenaPadding = 0.5f;
    [SerializeField] private bool _clampToArena = true;

    [Header("References")]
    [SerializeField] private Rigidbody2D _rb;

    private SwordAction _currentAction;
    private Vector2 _currentVelocity;

    private void Awake()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.linearDamping = 2f;
            _rb.angularDamping = 2f;
        }
    }

    public void SetAction(SwordAction action)
    {
        _currentAction = action;
    }

    private void FixedUpdate()
    {
        ExecuteCurrentAction();

        if (_clampToArena)
            ClampToArena();
    }

    private void ExecuteCurrentAction()
    {
        Vector2 moveDir = DecodeMove(_currentAction.Move);
        float rotationInput = DecodeRotation(_currentAction.Rotate);

        ApplyMove(moveDir);
        ApplyRotation(rotationInput);
    }

    private Vector2 DecodeMove(SwordMoveAction action)
    {
        switch (action)
        {
            case SwordMoveAction.Up:
                return Vector2.up;

            case SwordMoveAction.Down:
                return Vector2.down;

            case SwordMoveAction.Left:
                return Vector2.left;

            case SwordMoveAction.Right:
                return Vector2.right;

            case SwordMoveAction.UpLeft:
                return new Vector2(-1f, 1f).normalized;

            case SwordMoveAction.UpRight:
                return new Vector2(1f, 1f).normalized;

            case SwordMoveAction.DownLeft:
                return new Vector2(-1f, -1f).normalized;

            case SwordMoveAction.DownRight:
                return new Vector2(1f, -1f).normalized;

            default:
                return Vector2.zero;
        }
    }

    private float DecodeRotation(SwordRotateAction action)
    {
        switch (action)
        {
            case SwordRotateAction.CounterClockwise:
                return 1f;

            case SwordRotateAction.Clockwise:
                return -1f;

            default:
                return 0f;
        }
    }

    private void ApplyMove(Vector2 moveDir)
    {
        if (_rb == null) return;

        Vector2 targetVelocity = moveDir * _moveSpeed;

        if (moveDir.magnitude > 0.1f)
        {
            _currentVelocity = Vector2.Lerp(
                _currentVelocity,
                targetVelocity,
                _acceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            _currentVelocity = Vector2.Lerp(
                _currentVelocity,
                Vector2.zero,
                _deceleration * Time.fixedDeltaTime
            );
        }

        _rb.linearVelocity = _currentVelocity;
    }

    private void ApplyRotation(float rotationInput)
    {
        if (Mathf.Abs(rotationInput) < 0.01f)
            return;

        float rotationAmount = rotationInput * _rotationSpeed * Time.fixedDeltaTime;
        transform.Rotate(0f, 0f, rotationAmount);
    }

    private void ClampToArena()
    {
        Vector2 center = _arenaCenter != null ? (Vector2)_arenaCenter.position : Vector2.zero;

        float halfW = _arenaWidth * 0.5f - _arenaPadding;
        float halfH = _arenaHeight * 0.5f - _arenaPadding;

        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, center.x - halfW, center.x + halfW);
        pos.y = Mathf.Clamp(pos.y, center.y - halfH, center.y + halfH);

        transform.position = pos;
    }

    public void ResetExecutor()
    {
        _currentAction = new SwordAction(SwordMoveAction.None, SwordRotateAction.None);
        _currentVelocity = Vector2.zero;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
    }
}