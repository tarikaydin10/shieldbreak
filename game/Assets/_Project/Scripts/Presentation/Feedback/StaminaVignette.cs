using Shieldbreak.Sim.Combat.Grammar;
using UnityEngine;

namespace Shieldbreak.Presentation.Feedback
{
    /// <summary>
    /// No combat HUD (game feel spec): low stamina darkens the screen edges with a pulse that quickens as the tank
    /// empties; taking damage flashes the edges red. Only for the local hero.
    /// </summary>
    public class StaminaVignette : MonoBehaviour, Admin.IAdminStatus
    {
        public SimRunner runner;
        [Tooltip("Below this share of max stamina the vignette starts.")]
        [Range(0f, 1f)] public float threshold = 0.4f;
        [Range(0f, 1f)] public float maxAlpha = 0.65f;
        [Tooltip("Pulse rate (Hz) at the threshold and at empty.")]
        public Vector2 pulseHz = new Vector2(0.8f, 2.4f);
        public Color exhausted = new Color(0.04f, 0.02f, 0.02f);
        public Color hurt = new Color(0.55f, 0.02f, 0.02f);
        [Min(0.05f)] public float hurtSeconds = 0.35f;

        Texture2D ring;
        float hurtFlash, phase, lastStamina = 1f;

        public string AdminStatusLabel => "Vignette";
        public string AdminStatus => runner == null || runner.PlayerAgent < 0 ? "aus (kein Held)" : $"Ausdauer {lastStamina:P0}";

        void OnDestroy()
        {
            if (ring != null) Destroy(ring);
        }

        static Texture2D BuildRing()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, r))));
            }
            tex.Apply();
            return tex;
        }

        void Update()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || !world.HasCombat || runner.PlayerAgent < 0) return;
            int hero = runner.PlayerAgent;
            foreach (var e in runner.FrameEvents)
                if (e.Target == hero && (e.Type == CombatEventType.Hit && e.Damage > 0f || e.Type == CombatEventType.GuardBreak)) hurtFlash = 1f;
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.unscaledDeltaTime / hurtSeconds);
            var c = world.Combat[hero];
            lastStamina = Mathf.Clamp01(c.Stamina / world.CombatSetup.Tuning.StaminaMax);
            float low = c.State == CombatState.Dead ? 0f : Mathf.Clamp01(1f - lastStamina / Mathf.Max(0.01f, threshold));
            phase += Time.unscaledDeltaTime * Mathf.Lerp(pulseHz.x, pulseHz.y, low) * 2f * Mathf.PI;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || runner == null || runner.World == null || runner.PlayerAgent < 0) return;
            if (ring == null) ring = BuildRing();
            GUI.depth = 10;
            float low = Mathf.Clamp01(1f - lastStamina / Mathf.Max(0.01f, threshold));
            float exhaustedAlpha = low <= 0f ? 0f : maxAlpha * low * (0.75f + 0.25f * Mathf.Sin(phase));
            var rect = new Rect(0, 0, Screen.width, Screen.height);
            if (exhaustedAlpha > 0.01f)
            {
                GUI.color = new Color(exhausted.r, exhausted.g, exhausted.b, exhaustedAlpha);
                GUI.DrawTexture(rect, ring);
            }
            if (hurtFlash > 0.01f)
            {
                GUI.color = new Color(hurt.r, hurt.g, hurt.b, hurtFlash * 0.6f);
                GUI.DrawTexture(rect, ring);
            }
            GUI.color = Color.white;
        }
    }
}
