using UnityEngine;
using UnityEngine.InputSystem;
using ClockworkCasino.Core;

[RequireComponent(typeof(PlayerInput))]
public class GameInputRouter : MonoBehaviour
{
    [SerializeField] private GameManager _gm;

    private PlayerInput _pi;
    private InputAction _borrow;

    private System.Action<InputAction.CallbackContext> _debugHandler;

    void Awake()
    {
        _pi = GetComponent<PlayerInput>();
        if (_gm == null) _gm = FindFirstObjectByType<GameManager>();
    }

    void OnEnable()
    {
        if (_pi == null) _pi = GetComponent<PlayerInput>();

        foreach (var map in _pi.actions.actionMaps) map.Disable();

        var playerMap = _pi.actions.FindActionMap("Player", throwIfNotFound: true);
        playerMap.Enable();

        _borrow = playerMap.FindAction("Borrow", throwIfNotFound: true);
        _borrow.performed += OnBorrow;
        _borrow.Enable();

        _debugHandler = ctx =>
        {
            if (ctx.performed)
                Debug.Log($"[Input] {ctx.action.actionMap.name}/{ctx.action.name} via {ctx.control?.displayName}");
        };
        _pi.onActionTriggered += _debugHandler;
    }

    void OnDisable()
    {
        if (_borrow != null)
        {
            _borrow.performed -= OnBorrow;
            _borrow.Disable();
            _borrow = null;
        }

        if (_pi != null && _debugHandler != null)
        {
            _pi.onActionTriggered -= _debugHandler;
            _debugHandler = null;
        }
    }

    private void OnBorrow(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || _gm == null) return;

        Debug.Log($"[Input] Borrow handler → gm.State={_gm.State}, debt={_gm.DebtS}, score={_gm.ScoreS}");

        if (_gm.State != GameState.Ended)
            _gm.TryBorrow();
    }
}
