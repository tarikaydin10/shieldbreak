using System;
using System.Collections;
using Shieldbreak.Presentation.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Admin
{
    /// <summary>
    /// Smoke test for builds: SHIELDBREAK.exe -shot out.png [-shotScene M0_Sandbox | -shotEntry battle] [-shotDelay 4]
    /// loads the scene or scene catalog entry, waits, logs the frame rate of the wait, writes a screenshot and quits.
    /// Does nothing without -shot.
    /// </summary>
    public class AutoScreenshot : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Arg("-shot") == null) return;
            var go = new GameObject("AutoScreenshot");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoScreenshot>();
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        IEnumerator Start()
        {
            string path = Arg("-shot");
            string scene = Arg("-shotScene");
            float delay = float.TryParse(Arg("-shotDelay"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 4f;

            string entry = Arg("-shotEntry");
            if (!string.IsNullOrEmpty(entry))
            {
                SceneLaunch.Start(SceneLaunch.Find(entry));
                yield return null;
            }
            else if (!string.IsNullOrEmpty(scene) && SceneManager.GetActiveScene().name != scene)
                yield return SceneManager.LoadSceneAsync(scene);
            int frames = 0;
            float worst = 0f, waited = 0f;
            while (waited < delay)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                // The first second is loading, not the game
                if (waited < 1f) continue;
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }
            Debug.Log($"[autoshot] {frames / Mathf.Max(waited - 1f, 0.01f):0} FPS on average, slowest frame {worst * 1000f:0} ms");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[autoshot] {SceneManager.GetActiveScene().name} -> {path}");
            Application.Quit();
        }
    }
}
