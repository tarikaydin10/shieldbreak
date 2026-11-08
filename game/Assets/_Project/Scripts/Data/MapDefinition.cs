using System;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// A battlefield as data (M4 task 8): size, hills, forest and swamp, and where each side may deploy. A scenario puts its
    /// armies on a map, so that later the Kriegszug can send any army to any map.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Map", fileName = "Map")]
    public class MapDefinition : ScriptableObject
    {
        [Serializable]
        public class Zone
        {
            public Vector2 min;
            public Vector2 max;
        }

        [Tooltip("Name in menus.")]
        public string displayName = "Karte";
        [Tooltip("Size in 1 m cells (square).")]
        [Min(16)] public int size = 200;
        [Tooltip("Round hills; several add up.")]
        public CompanyScenarioDefinition.Hill[] hills = Array.Empty<CompanyScenarioDefinition.Hill>();
        [Tooltip("Round areas of forest or swamp; later ones paint over earlier ones.")]
        public CompanyScenarioDefinition.Area[] areas = Array.Empty<CompanyScenarioDefinition.Area>();
        [Tooltip("Where the player's side may stand in the deployment (map coordinates in m).")]
        public Zone ownZone = new Zone { min = new Vector2(20f, 20f), max = new Vector2(180f, 70f) };
        [Tooltip("Where the enemy may stand.")]
        public Zone enemyZone = new Zone { min = new Vector2(20f, 130f), max = new Vector2(180f, 180f) };
    }
}
