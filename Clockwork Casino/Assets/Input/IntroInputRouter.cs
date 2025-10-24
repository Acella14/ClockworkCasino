using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class IntroInputRouter : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private IntroSequenceManager intro;

    [Header("Action Map / Action Names")]
    [SerializeField] private string actionMapName = "Intro";   // or "Player" if you prefer
    [SerializeField] private string continueActionName = "Continue";

    private PlayerInput _pi;
    private InputAction _continueAction;

    void Awake()
    {
        _pi = GetComponent<PlayerInput>();
        if (intro == null) intro = FindFirstObjectByType<IntroSequenceManager>();
    }

    void OnEnable()
    {
        if (_pi == null) _pi = GetComponent<PlayerInput>();

        // Disable everything so the intro scene is isolated
        foreach (var mapi in _pi.actions.actionMaps) mapi.Disable();

        // Enable only the map you want for this scene
        var map = _pi.actions.FindActionMap(actionMapName, throwIfNotFound: true);
        map.Enable();

        // Hook Continue
        _continueAction = map.FindAction(continueActionName, throwIfNotFound: true);
        _continueAction.performed += OnContinue;
        _continueAction.Enable();

        // (Optional) debug per-action:
        // _pi.onActionTriggered += ctx => Debug.Log($"[IntroInput] {ctx.action.actionMap.name}/{ctx.action.name}");
    }

    void OnDisable()
    {
        if (_continueAction != null)
        {
            _continueAction.performed -= OnContinue;
            _continueAction.Disable();
            _continueAction = null;
        }
        // if you added onActionTriggered debug, unsubscribe it here
    }

    private void OnContinue(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || intro == null) return;
        intro.OnContinueRequestedByKeyboard();
    }
}
