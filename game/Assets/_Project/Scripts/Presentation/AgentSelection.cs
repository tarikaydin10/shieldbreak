using System;
using System.Collections.Generic;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Which agents the local player has selected. Pure UI state: never part of the sim or its hash.
    /// </summary>
    public static class AgentSelection
    {
        static bool[] selected = Array.Empty<bool>();
        public static int Count { get; private set; }
        public static event Action Changed;

        public static bool IsSelected(int id) => id < selected.Length && selected[id];

        public static void Clear()
        {
            Array.Clear(selected, 0, selected.Length);
            Count = 0;
            Changed?.Invoke();
        }

        public static void Set(IEnumerable<int> ids, bool additive)
        {
            if (!additive) Array.Clear(selected, 0, selected.Length);
            foreach (int id in ids)
            {
                if (id >= selected.Length) Array.Resize(ref selected, Math.Max(id + 1, selected.Length * 2));
                selected[id] = true;
            }
            int count = 0;
            foreach (bool s in selected) if (s) count++;
            Count = count;
            Changed?.Invoke();
        }

        /// <summary>Selected ids in ascending order.</summary>
        public static void CopyTo(List<int> ids)
        {
            ids.Clear();
            for (int i = 0; i < selected.Length; i++) if (selected[i]) ids.Add(i);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            selected = Array.Empty<bool>();
            Count = 0;
            Changed = null;
        }
    }
}
