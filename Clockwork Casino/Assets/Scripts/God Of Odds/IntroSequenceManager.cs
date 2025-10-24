using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;



[System.Serializable]
public class DialogueStep
{
    [TextArea(2, 6)] public string text;
    public float charsPerSecondOverride = 0f;

    [Header("Advance Control")]
    public bool requireUIButton = false;
    public bool showSpaceTooltip = true;
    public string buttonText = "Next";

    [Header("Button Hover Swap (optional)")]
    public bool useHoverSwap = false;
    public string hoverIdleText = "No.";
    public string hoverActiveText = "Absolutely.";

    [Header("Auto Advance")]
    public bool autoAdvance = false;
    public float autoAdvanceDelay = 1.0f;

    [Header("Jaw")]
    [Tooltip("If off, the jaw will NOT animate during this line.")]
    public bool animateJaw = true;

    [Header("Reaction")]
    [Tooltip("If true, play this state after the line finishes typing (auto) or on button click (manual).")]
    public bool playReaction = false;
    public string reactionStateName = "";
    public bool hideDialogueCanvasDuringReaction = true;
    public float waitTime = 0.0f;

    [Header("Extra Light (optional)")]
    [Tooltip("If true, fade the extra light when this step advances (manual) or right after the line (auto).")]
    public bool fadeExtraLightOnAdvance = false;
    [Tooltip("Target intensity for the extra light fade.")]
    public float extraLightTargetIntensity = 6f;
    [Tooltip("Seconds for the extra light fade on this step.")]
    public float extraLightFadeDuration = 0.6f;

    [Header("Scene Transition")]
    public bool loadMenuOnAdvance = false;
}




public class IntroSequenceManager : MonoBehaviour
{
    // --- Scene References ---
    [Header("Scene: Characters & Animation")]
    public JawController jaw;
    public Animator godAnimator;
    public SceneSwitcher sceneSwitcher;

    [Header("Scene: Lights")]
    [Tooltip("Main reveal light used at the beginning.")]
    public Light2D keyLight;
    [Tooltip("Optional second light you can fade in on specific steps.")]
    public Light2D extraLight;

    // --- Pre-Prompt & Typing ---
    [Header("UI: Pre-Prompt (shown BEFORE black hold)")]
    public GameObject prePromptGroup;
    [Tooltip("If true, show prePromptGroup first, then hide it, THEN do black hold + reveal.")]
    public bool showPrePrompt = true;

    [Header("Typing / Sequence")]
    [Tooltip("Hold on total black before revealing the key light.")]
    public float blackHoldSeconds = 0.8f;
    [Tooltip("Intensity to set/fade the key light to on reveal.")]
    public float revealLightIntensity = 12f;
    [Tooltip("Default characters per second if a step doesn't override.")]
    public float defaultCharsPerSecond = 45f;
    [Tooltip("If true, pressing advance while typing fills the rest of the line instantly.")]
    public bool allowSkipToFullLine = true;

    // --- Light Fades ---
    [Header("Light Fade Settings (Reveal)")]
    [Tooltip("If true, fades the key light instead of snapping to the reveal intensity.")]
    public bool fadeLight = true;
    [Tooltip("Seconds to fade the key light from black to reveal intensity.")]
    public float lightFadeDuration = 0.6f;
    [Tooltip("Curve for the key light fade (0..1 time → 0..1 intensity).")]
    public AnimationCurve lightFadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // --- Dialogue UI ---
    [Header("UI: Dialogue")]
    public CanvasGroup dialogueCanvas;
    public TextMeshProUGUI dialogueText;
    public Button nextButton;
    public TextMeshProUGUI nextButtonLabel;
    public HoverSwapLabel nextButtonHoverSwap;
    public GameObject continueIndicator;
    public GameObject spaceTooltip;

    [Header("UI: Dialogue Fade-In")]
    [Tooltip("Seconds to fade in the dialogue canvas at the start.")]
    public float dialogueFadeDuration = 0.6f;
    [Tooltip("Curve for the dialogue fade (0..1 time → 0..1 alpha).")]
    public AnimationCurve dialogueFadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // --- Script ---
    [Header("Dialogue Script")]
    public List<DialogueStep> steps = new List<DialogueStep>();

    // --- Runtime ---
    int _stepIndex = -1;
    bool _isTyping = false;
    bool _lineCompleted = false;
    bool _requestedAdvance = false;
    bool _allowKeyboardAdvance = true;
    Coroutine _typingCo;
    Dictionary<string, float> _clipLenCache;


    void Awake()
    {
        if (prePromptGroup) prePromptGroup.SetActive(showPrePrompt);

        if (continueIndicator) continueIndicator.SetActive(false);
        if (spaceTooltip) spaceTooltip.SetActive(false);

        if (dialogueCanvas)
        {
            dialogueCanvas.alpha = 0f;
            dialogueCanvas.interactable = false;
            dialogueCanvas.blocksRaycasts = false;
        }

        if (nextButton && nextButtonLabel == null)
            nextButtonLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (nextButton)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnContinueRequestedByButton);
            nextButton.gameObject.SetActive(false);
            nextButton.interactable = false;
        }
    }

    void Start()
    {
        StartCoroutine(RunSequence());
    }


    IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration, AnimationCurve curve = null)
    {
        if (cg == null || duration <= 0f)
        {
            if (cg) cg.alpha = to;
            yield break;
        }

        float t = 0f;
        cg.alpha = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = (curve != null) ? curve.Evaluate(k) : k;
            cg.alpha = Mathf.LerpUnclamped(from, to, eased);
            yield return null;
        }

        cg.alpha = to;
    }

    IEnumerator FadeLight(Light2D light, float from, float to, float duration, AnimationCurve curve)
    {
        if (light == null || duration <= 0f)
        {
            if (light) light.intensity = to;
            yield break;
        }

        float t = 0f;
        light.intensity = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = (curve != null) ? curve.Evaluate(k) : k;
            light.intensity = Mathf.LerpUnclamped(from, to, eased);
            yield return null;
        }

        light.intensity = to;
    }

    IEnumerator RunSequence()
    {
        // Start conditions
        if (jaw) jaw.talking = false;
        if (dialogueText) dialogueText.text = "";

        // Dialogue canvas fade in
        if (dialogueCanvas)
        {
            dialogueCanvas.alpha = 0f;
            yield return StartCoroutine(FadeCanvas(dialogueCanvas, 0f, 1f, dialogueFadeDuration, dialogueFadeCurve));
            dialogueCanvas.interactable = true;
            dialogueCanvas.blocksRaycasts = true;
        }

        // Pre-prompt (before lights)
        if (showPrePrompt && prePromptGroup)
        {
            _requestedAdvance = false;
            yield return new WaitUntil(() => _requestedAdvance);
            prePromptGroup.SetActive(false);
            _requestedAdvance = false;
        }

        // Black hold
        if (keyLight) keyLight.intensity = 0f;
        yield return new WaitForSeconds(blackHoldSeconds);

        // Reveal main light
        if (keyLight)
        {
            if (fadeLight)
                yield return StartCoroutine(FadeLight(keyLight, 0f, revealLightIntensity, lightFadeDuration, lightFadeCurve));
            else
                keyLight.intensity = revealLightIntensity;
        }

        // Dialogue loop
        for (_stepIndex = 0; _stepIndex < steps.Count; _stepIndex++)
        {
            var step = steps[_stepIndex];

            _requestedAdvance = false;
            _allowKeyboardAdvance = !step.requireUIButton;

            if (nextButton)
            {
                nextButton.gameObject.SetActive(false);
                nextButton.interactable = false;
            }
            if (spaceTooltip) spaceTooltip.SetActive(false);

            if (_typingCo != null) StopCoroutine(_typingCo);
            _typingCo = StartCoroutine(TypeLine(step));
            yield return new WaitUntil(() => _lineCompleted);

            bool willAuto = step.autoAdvance;

            if (continueIndicator) continueIndicator.SetActive(!willAuto);
            if (spaceTooltip) spaceTooltip.SetActive(!willAuto && !step.requireUIButton && step.showSpaceTooltip);

            if (nextButton)
            {
                bool useButton = step.requireUIButton && !willAuto;
                if (useButton)
                {
                    yield return new WaitForSeconds(1.0f);
                    if (nextButtonHoverSwap != null && step.useHoverSwap)
                    {
                        nextButtonHoverSwap.enabledForThisStep = true;
                        nextButtonHoverSwap.idleText = string.IsNullOrEmpty(step.hoverIdleText) ? "No." : step.hoverIdleText;
                        nextButtonHoverSwap.hoverText = string.IsNullOrEmpty(step.hoverActiveText) ? "Absolutely." : step.hoverActiveText;
                        nextButtonHoverSwap.ApplyIdle();
                    }
                    else if (nextButtonHoverSwap != null)
                    {
                        nextButtonHoverSwap.enabledForThisStep = false;
                    }
                    if (nextButtonLabel && !step.useHoverSwap)
                        nextButtonLabel.text = string.IsNullOrEmpty(step.buttonText) ? "Next" : step.buttonText;

                    nextButton.gameObject.SetActive(true);
                    nextButton.interactable = true;
                }
                else
                {
                    if (nextButtonHoverSwap != null) nextButtonHoverSwap.enabledForThisStep = false;
                    nextButton.gameObject.SetActive(false);
                    nextButton.interactable = false;
                }
            }

            _requestedAdvance = false;

            if (willAuto)
            {
                if (step.playReaction || step.fadeExtraLightOnAdvance)
                    yield return StartCoroutine(CoPlayPostEffects(step, setAdvanceFlag: false, loadMenuAfter: false));

                // wait auto delay (skippable)
                float t = 0f, wait = Mathf.Max(0f, step.autoAdvanceDelay);
                while (t < wait && !_requestedAdvance) { t += Time.deltaTime; yield return null; }

                if (continueIndicator) continueIndicator.SetActive(false);
                if (spaceTooltip) spaceTooltip.SetActive(false);

                // If this step should jump to menu, do it now
                if (step.loadMenuOnAdvance)
                {
                    TryLoadMenu();
                    yield break; // stop the sequence here
                }
            }
            else
            {
                yield return new WaitUntil(() => _requestedAdvance);
                if (continueIndicator) continueIndicator.SetActive(false);
                if (spaceTooltip) spaceTooltip.SetActive(false);

                // If this step was flagged to load menu, jump now.
                if (step.loadMenuOnAdvance)
                {
                    TryLoadMenu();
                    yield break;
                }
            }
        }

        if (jaw) jaw.talking = false;
    }

    IEnumerator TypeLine(DialogueStep step)
    {
        _isTyping = true;
        _lineCompleted = false;

        bool doJaw = (step == null) ? true : step.animateJaw;
        if (jaw) jaw.talking = doJaw;

        string full = step?.text ?? "";
        if (dialogueText) dialogueText.text = "";

        float cps = (step != null && step.charsPerSecondOverride > 0f)
            ? step.charsPerSecondOverride : defaultCharsPerSecond;
        cps = Mathf.Max(1f, cps);
        float delay = 1f / cps;

        _requestedAdvance = false;

        for (int i = 0; i < full.Length; i++)
        {
            if (_requestedAdvance && allowSkipToFullLine)
            {
                if (dialogueText) dialogueText.text = full;
                break;
            }

            if (dialogueText) dialogueText.text = full.Substring(0, i + 1);
            yield return new WaitForSeconds(delay);
        }

        _isTyping = false;
        _lineCompleted = true;

        if (jaw) jaw.talking = false;
        _requestedAdvance = false;
    }


    public void OnContinueRequestedByButton()
    {
        if (_isTyping) { _requestedAdvance = true; return; }

        if (_stepIndex >= 0 && _stepIndex < steps.Count)
        {
            var step = steps[_stepIndex];
            bool wantsFX = step.playReaction || step.fadeExtraLightOnAdvance;

            if (wantsFX)
            {
                if (nextButton) nextButton.interactable = false;
                StartCoroutine(CoPlayPostEffects(
                    step,
                    setAdvanceFlag: !step.loadMenuOnAdvance,
                    loadMenuAfter: step.loadMenuOnAdvance
                ));
                return;
            }

            if (step.loadMenuOnAdvance)
            {
                TryLoadMenu();
                return;
            }
        }

        _requestedAdvance = true;
    }

    public void OnContinueRequestedByKeyboard()
    {
        if (!_allowKeyboardAdvance) return;

        if (_isTyping) { _requestedAdvance = true; return; }

        if (_stepIndex >= 0 && _stepIndex < steps.Count)
        {
            var step = steps[_stepIndex];
            bool wantsFX = step.playReaction || step.fadeExtraLightOnAdvance;

            if (wantsFX)
            {
                StartCoroutine(CoPlayPostEffects(
                    step,
                    setAdvanceFlag: !step.loadMenuOnAdvance,
                    loadMenuAfter: step.loadMenuOnAdvance
                ));
                return;
            }

            if (step.loadMenuOnAdvance)
            {
                TryLoadMenu();
                return;
            }
        }

        _requestedAdvance = true;
    }


    float GetClipLength(string name)
    {
        if (string.IsNullOrEmpty(name) || godAnimator == null) return 0f;

        if (_clipLenCache == null)
        {
            _clipLenCache = new Dictionary<string, float>();
            var rc = godAnimator.runtimeAnimatorController;
            if (rc != null)
            {
                foreach (var clip in rc.animationClips)
                    if (!_clipLenCache.ContainsKey(clip.name))
                        _clipLenCache[clip.name] = clip.length;
            }
        }

        return _clipLenCache.TryGetValue(name, out var len) ? len : 0f;
    }

    int ReactionLayer => 0;

    int ResolveStateHash(string stateName, Animator anim)
    {
        int full = Animator.StringToHash($"Base Layer.{stateName}");
        if (anim.HasState(ReactionLayer, full)) return full;

        int shortHash = Animator.StringToHash(stateName);
        if (anim.HasState(ReactionLayer, shortHash)) return shortHash;

        return 0;
    }

    void SetDialogueCanvasVisible(bool visible)
    {
        if (!dialogueCanvas) return;
        dialogueCanvas.alpha = visible ? 1f : 0f;
        dialogueCanvas.interactable = visible;
        dialogueCanvas.blocksRaycasts = visible;
    }

    IEnumerator CoPlayPostEffects(DialogueStep step, bool setAdvanceFlag, bool loadMenuAfter)
    {
        yield return new WaitForSeconds(step.waitTime);

        bool hidCanvas = false;
        if (step.hideDialogueCanvasDuringReaction)
        {
            SetDialogueCanvasVisible(false);
            if (continueIndicator) continueIndicator.SetActive(false);
            if (spaceTooltip) spaceTooltip.SetActive(false);
            if (nextButton) { nextButton.interactable = false; nextButton.gameObject.SetActive(false); }
            hidCanvas = true;
        }
        else if (nextButton) nextButton.interactable = false;

        Coroutine extraLightCo = null;
        if (step.fadeExtraLightOnAdvance && extraLight != null)
        {
            extraLightCo = StartCoroutine(FadeLight(
                extraLight,
                extraLight.intensity,
                step.extraLightTargetIntensity,
                Mathf.Max(0f, step.extraLightFadeDuration),
                lightFadeCurve
            ));
        }

        if (godAnimator != null && step.playReaction && !string.IsNullOrEmpty(step.reactionStateName))
        {
            godAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            godAnimator.updateMode = AnimatorUpdateMode.Normal;
            godAnimator.enabled = true;
            godAnimator.Rebind();
            godAnimator.Update(0f);

            int stateHash = ResolveStateHash(step.reactionStateName, godAnimator);
            if (stateHash != 0)
            {
                godAnimator.Play(stateHash, ReactionLayer, 0f);
                float clipLen = GetClipLength(step.reactionStateName);
                if (clipLen <= 0f) clipLen = 0.25f;
                float maxWait = clipLen + 0.1f, elapsed = 0f;
                while (elapsed < maxWait)
                {
                    var st = godAnimator.GetCurrentAnimatorStateInfo(ReactionLayer);
                    bool onRightState =
                        (st.fullPathHash == stateHash) ||
                        (st.shortNameHash == Animator.StringToHash(step.reactionStateName));
                    if (onRightState && !st.loop && st.normalizedTime >= 0.98f) break;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                godAnimator.enabled = false;
            }
            else
            {
                Debug.LogError($"[Intro] Animator.HasState failed for '{step.reactionStateName}'.");
            }
        }

        if (extraLightCo != null) yield return extraLightCo;

        if (hidCanvas) SetDialogueCanvasVisible(true);

        if (loadMenuAfter)
        {
            TryLoadMenu();
            yield break;
        }

        if (setAdvanceFlag) _requestedAdvance = true;
    }

    void TryLoadMenu()
    {
        if (sceneSwitcher == null)
            sceneSwitcher = FindFirstObjectByType<SceneSwitcher>();

        if (sceneSwitcher != null)
            sceneSwitcher.LoadMenu();
        else
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

}
