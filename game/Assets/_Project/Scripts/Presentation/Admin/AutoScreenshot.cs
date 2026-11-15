using System;
using System.Collections;
using Shieldbreak.Presentation.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Admin
{
    /// <summary>
    /// Smoke test for builds: SHIELDBREAK.exe -shot out.png [-shotScene M0_Sandbox | -shotEntry battle] [-shotDelay 4]
    /// [-shotView x,y,z,pitch,yaw] loads the scene or scene catalog entry, waits, logs the frame rate of the wait, writes a
    /// screenshot (from the given point, if any) and quits.
    /// Does nothing without -shot.
    /// </summary>
    public class AutoScreenshot : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Arg("-shot") == null) return;
            // Without focus the player would pause and the shot would wait for someone to click the window
            Application.runInBackground = true;
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

        /// <summary>-shotView x,y,z,pitch,yaw: shoot from this point instead of the game's camera (degrees, yaw 0 = north).</summary>
        static bool TryView(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            var parts = Arg("-shotView")?.Split(',');
            if (parts == null || parts.Length != 5) return false;
            var v = new float[5];
            for (int i = 0; i < 5; i++)
                if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i])) return false;
            position = new Vector3(v[0], v[1], v[2]);
            rotation = Quaternion.Euler(v[3], v[4], 0f);
            return true;
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
            if (TryView(out var position, out var rotation) && Camera.main != null)
            {
                // A camera of its own on top: the game's cameras keep steering theirs
                var view = new GameObject("AutoScreenshotView").AddComponent<Camera>();
                view.CopyFrom(Camera.main);
                view.depth = Camera.main.depth + 10f;
                view.transform.SetPositionAndRotation(position, rotation);
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[autoshot] {SceneManager.GetActiveScene().name} -> {path}");
            Application.Quit();
        }
    }
}
