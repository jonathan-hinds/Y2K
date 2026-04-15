using System;
using System.Collections.Generic;
using UnityEngine;

namespace Race.Scoring
{
    [Serializable]
    public struct TrickScoreDefinition
    {
        [SerializeField] private string trickId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int pointsPerAward;
        [SerializeField, Min(0.01f)] private float awardIntervalSeconds;
        [SerializeField] private Vector3 popupLocalOffset;

        public string TrickId => trickId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? trickId : displayName;
        public int PointsPerAward => Mathf.Max(1, pointsPerAward);
        public float AwardIntervalSeconds => Mathf.Max(0.01f, awardIntervalSeconds);
        public Vector3 PopupLocalOffset => popupLocalOffset;
    }

    [CreateAssetMenu(fileName = "TrickScoringSettings", menuName = "Race/Scoring/Trick Scoring Settings")]
    public sealed class TrickScoringSettings : ScriptableObject
    {
        [SerializeField] private List<TrickScoreDefinition> trickDefinitions = new List<TrickScoreDefinition>();

        public IReadOnlyList<TrickScoreDefinition> TrickDefinitions => trickDefinitions;

        public bool TryGetDefinition(string trickId, out TrickScoreDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(trickId))
            {
                for (int index = 0; index < trickDefinitions.Count; index++)
                {
                    TrickScoreDefinition candidate = trickDefinitions[index];
                    if (string.Equals(candidate.TrickId, trickId, StringComparison.OrdinalIgnoreCase))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }
    }
}
