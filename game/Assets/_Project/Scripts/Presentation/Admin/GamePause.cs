using System;
using UnityEngine;

namespace Shieldbreak.Presentation.Admin
{
    /// <summary>
    /// Singleplayer pause (freeze). Scaled time stops; anything meant to stay usable while frozen (debug cameras,
    /// menus) runs on unscaled time. From M0 task 5 the SimRunner runs zero ticks while paused, so the sim itself
    /// is untouched. Multiplayer (M6) will not allow pausing.
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }
        public static event Action<bool> Changed;

        public static void Set(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Changed?.Invoke(paused);
        }

        public static void Toggle() => Set(!IsPaused);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            Changed = null;
            Time.timeScale = 1f;
        }
    }
}
