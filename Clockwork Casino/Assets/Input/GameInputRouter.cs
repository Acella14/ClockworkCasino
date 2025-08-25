using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class GameInputRouter : MonoBehaviour
{
    [SerializeField] private ClockworkCasino.Core.GameManager _gm;
    private PlayerInput _pi;

    void Awake()
    {
        _pi = GetComponent<PlayerInput>();

        // Disable all maps first
        foreach (var map in _pi.actions.actionMaps)
            map.Disable();

        // Enable only the default gameplay map ()
        var playerMap = _pi.actions.FindActionMap("Player", throwIfNotFound: true);
        playerMap.Enable();
    }

    public void OnBorrow(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || _gm == null) return;

        if (_gm.State == ClockworkCasino.Core.GameState.RoundActive || 
            _gm.State == ClockworkCasino.Core.GameState.InterRound)
        {
            _gm.TryBorrow();
        }
    }
}

