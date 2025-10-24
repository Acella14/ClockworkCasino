using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("Fade (CanvasGroup)")]
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.35f;

    public void LoadGame()
    {
        Time.timeScale = 1f;
        StopAllCoroutines();
        if (fadeCanvas && fadeSeconds > 0f) StartCoroutine(CoFadeThenLoad(gameSceneName));
        else SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        StopAllCoroutines();
        if (fadeCanvas && fadeSeconds > 0f) StartCoroutine(CoFadeThenLoad(menuSceneName));
        else SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }

    private IEnumerator CoFadeThenLoad(string sceneName)
    {
        var go = fadeCanvas.gameObject;
        go.SetActive(true);
        fadeCanvas.ignoreParentGroups = true;
        fadeCanvas.blocksRaycasts = true;
        fadeCanvas.interactable = false;
        fadeCanvas.alpha = 0f;

        var anim = go.GetComponent<Animator>();
        if (anim) anim.enabled = false;

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }
        fadeCanvas.alpha = 1f;

        yield return new WaitForEndOfFrame();

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
