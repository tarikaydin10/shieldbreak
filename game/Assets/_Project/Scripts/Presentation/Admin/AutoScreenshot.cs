using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Admin
{
    /// <summary>
    /// Smoke test for builds: SHIELDBREAK.exe -shot out.png [-shotScene M0_Sandbox] [-shotDelay 4] loads the scene,
    /// waits, writes a screenshot and quits. Does nothing without -shot.
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

            if (!string.IsNullOrEmpty(scene) && SceneManager.GetActiveScene().name != scene)
                yield return SceneManager.LoadSceneAsync(scene);
            yield return new WaitForSecondsRealtime(delay);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[autoshot] {scene ?? SceneManager.GetActiveScene().name} -> {path}");
            Application.Quit();
        }
    }
}
