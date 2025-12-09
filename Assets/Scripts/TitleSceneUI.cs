using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleF1.UI
{
    /// <summary>
    /// Simple title screen UI with an IMGUI button and a fade-to-black transition
    /// before loading the play scene. Works with mouse or touch; no extra UI prefabs required.
    /// </summary>
    public class TitleSceneUI : MonoBehaviour
    {
        [SerializeField]
        private string playSceneName = "Play.Scene";

        [SerializeField]
        private Rect buttonRect = new Rect(0f, 0f, 220f, 60f);

        [SerializeField]
        private string buttonText = "Play";

        [SerializeField]
        private float fadeDuration = 0.6f;

        private Texture2D fadeTexture;
        private bool isTransitioning;
        private float fadeAlpha;

        private void Awake()
        {
            fadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            fadeTexture.SetPixel(0, 0, Color.black);
            fadeTexture.Apply();
        }

        private void OnGUI()
        {
            Rect centered = new Rect(
                (Screen.width - buttonRect.width) * 0.5f,
                (Screen.height - buttonRect.height) * 0.5f,
                buttonRect.width,
                buttonRect.height);

            GUI.enabled = !isTransitioning;
            if (GUI.Button(centered, buttonText))
            {
                StartCoroutine(FadeAndLoad());
            }
            GUI.enabled = true;

            if (isTransitioning)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fadeTexture);
                GUI.color = prev;
            }
        }

        public void LoadPlayScene()
        {
            if (!string.IsNullOrEmpty(playSceneName))
            {
                SceneManager.LoadScene(playSceneName);
            }
        }

        private System.Collections.IEnumerator FadeAndLoad()
        {
            if (isTransitioning) yield break;
            isTransitioning = true;
            fadeAlpha = 0f;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            LoadPlayScene();
        }
    }
}

