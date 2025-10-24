using UnityEngine;

[DisallowMultipleComponent]
public class JawController : MonoBehaviour
{
    [Header("Test / Control")]
    public bool talking = true;
    [Tooltip("Global intensity multiplier for how 'talky' the jaw is.")]
    [Range(0f, 2f)] public float intensity = 1f;

    [Header("Open/Close")]
    [Tooltip("Mouth closed Y-offset (local/anchored). 0 is the current position.")]
    public float closedOffsetY = 0f;
    [Tooltip("Max additional downward offset when fully open (negative opens down in UI).")]
    public float openOffsetY = -30f;
    [Tooltip("How fast the mouth cycles (Perlin time scale).")]
    [Range(0.1f, 8f)] public float talkSpeed = 2f;
    [Tooltip("Randomness in amplitude of each cycle.")]
    [Range(0f, 1f)] public float amplitudeJitter = 0.25f;
    [Tooltip("Smooth time for vertical motion (seconds).")]
    [Range(0.01f, 0.5f)] public float moveSmoothTime = 0.08f;

    [Header("Tilt (optional, subtle)")]
    public bool enableTilt = true;
    [Tooltip("Max absolute tilt angle in degrees.")]
    [Range(0f, 20f)] public float maxTilt = 6f;
    [Tooltip("Chance a tilt happens when reaching bottom (0..1).")]
    [Range(0f, 1f)] public float tiltChanceAtBottom = 0.35f;
    [Tooltip("How quickly tilt eases toward its target.")]
    [Range(0.01f, 0.5f)] public float tiltSmoothTime = 0.12f;
    [Tooltip("Cooldown to avoid constant tilting (seconds).")]
    [Range(0f, 1f)] public float tiltCooldown = 0.25f;

    [Header("General")]
    [Tooltip("Seed so multiple characters can have different patterns.")]
    public int noiseSeed = 12345;
    [Tooltip("If true, uses rotation hinge instead of (or in addition to) vertical motion.")]
    public bool alsoRotateAtOpen = false;
    [Tooltip("Extra rotation at full open (degrees).")]
    [Range(0f, 25f)] public float openHingeExtraDegrees = 8f;

    // Private state
    RectTransform rect;
    Vector3 baseLocalPos;
    bool _prevTalking = false;
    float yVel;
    float tiltVel;
    float targetTilt;
    float lastTiltTime;

    // Small variance per-instance
    float ampScale;
    float timeOffset;

    // Tilt streak control
    int lastTiltSign = 0;
    int sameDirCount = 0;
    [SerializeField] int maxSameDirInRow = 2;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        baseLocalPos = transform.localPosition;

        var rnd = new System.Random(noiseSeed ^ GetInstanceID());
        ampScale = 1f + ((float)rnd.NextDouble() * 2f - 1f) * amplitudeJitter;
        timeOffset = (float)rnd.NextDouble() * 1000f;
    }

    void OnEnable()
    {
        baseLocalPos = transform.localPosition;
    }

    void Update()
    {
        // Compute normalized open amount (0..1) using Perlin for smooth random talk
        float t = talking ? Time.time : 0f;
        float open01;
        if (talking)
        {
            float noise = Mathf.PerlinNoise(timeOffset, Time.time * talkSpeed);
            open01 = QuinticEaseInOut(noise) * intensity * ampScale;
        }
        else
        {
            open01 = 0f;
        }
        open01 = Mathf.Clamp01(open01);

        // Vertical movement
        float targetY = Mathf.Lerp(closedOffsetY, openOffsetY, open01);

        if (rect != null)
        {
            Vector2 ap = rect.anchoredPosition;
            float smoothedY = Mathf.SmoothDamp(ap.y, targetY, ref yVel, moveSmoothTime);
            ap.y = smoothedY;
            rect.anchoredPosition = ap;
        }
        else
        {
            Vector3 lp = transform.localPosition;
            float smoothedY = Mathf.SmoothDamp(lp.y - baseLocalPos.y, targetY, ref yVel, moveSmoothTime);
            lp.y = baseLocalPos.y + smoothedY;
            transform.localPosition = lp;
        }

        float hingeExtra = (alsoRotateAtOpen ? -open01 * openHingeExtraDegrees : 0f);

        // Occasional subtle tilt at bottom
        if (enableTilt)
        {
            TryTriggerTiltAtBottom(open01);
            float currentZ = GetLocalEulerZ();
            float desiredZ = Mathf.SmoothDampAngle(currentZ, targetTilt + hingeExtra, ref tiltVel, tiltSmoothTime);
            SetLocalEulerZ(desiredZ);
        }
        else
        {
            // Still apply hingeExtra if enabled
            if (alsoRotateAtOpen)
                SetLocalEulerZ(Mathf.SmoothDampAngle(GetLocalEulerZ(), hingeExtra, ref tiltVel, tiltSmoothTime));
        }

        if (!_prevTalking && talking)
        {
            // just started talking
        }
        else if (_prevTalking && !talking)
        {
            // just stopped talking
            yVel = 0f;
            tiltVel = 0f;
            targetTilt = 0f;

            // Also force target to closed this frame
            if (rect != null)
            {
                var ap = rect.anchoredPosition;
                ap.y = Mathf.Lerp(ap.y, closedOffsetY, 1f);
                rect.anchoredPosition = ap;
            }
            else
            {
                var lp = transform.localPosition;
                lp.y = baseLocalPos.y + closedOffsetY;
                transform.localPosition = lp;
            }
            SetLocalEulerZ(0f);
        }
        _prevTalking = talking;
    }

    void TryTriggerTiltAtBottom(float open01)
    {
        const float bottomThreshold = 0.3f;

        if (open01 >= bottomThreshold && (Time.time - lastTiltTime) >= tiltCooldown)
        {
            if (Random.value < tiltChanceAtBottom)
            {
                int sign;
                if (lastTiltSign != 0 && sameDirCount >= maxSameDirInRow)
                {
                    sign = -lastTiltSign;
                }
                else
                {
                    sign = (Random.value < 0.5f) ? -1 : +1;
                }

                float mag = Random.Range(maxTilt * 0.2f, maxTilt);
                targetTilt = sign * mag;

                if (sign == lastTiltSign)
                {
                    sameDirCount++;
                }
                else
                {
                    lastTiltSign = sign;
                    sameDirCount = 1;
                }

                lastTiltTime = Time.time;
            }
        }

        // Drift back toward neutral when mostly closed
        if (open01 < 0.3f)
        {
            targetTilt = Mathf.MoveTowards(targetTilt, 0f, Time.deltaTime * (maxTilt / Mathf.Max(0.001f, tiltSmoothTime)));
        }
    }

    public void SnapClosed()
    {
        yVel = 0f;
        tiltVel = 0f;
        targetTilt = 0f;

        if (rect != null)
        {
            var ap = rect.anchoredPosition;
            ap.y = closedOffsetY;
            rect.anchoredPosition = ap;
        }
        else
        {
            var lp = transform.localPosition;
            lp.y = baseLocalPos.y + closedOffsetY;
            transform.localPosition = lp;
        }

        SetLocalEulerZ(0f);
    }


    float GetLocalEulerZ()
    {
        float z = transform.localEulerAngles.z;
        if (z > 180f) z -= 360f;
        return z;
    }

    void SetLocalEulerZ(float z)
    {
        Vector3 e = transform.localEulerAngles;
        e.z = z;
        transform.localEulerAngles = e;
    }

    static float QuinticEaseInOut(float x)
    {
        return x * x * x * (x * (6f * x - 15f) + 10f);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (moveSmoothTime < 0.01f) moveSmoothTime = 0.01f;
        if (tiltSmoothTime < 0.01f) tiltSmoothTime = 0.01f;
    }
#endif
}
