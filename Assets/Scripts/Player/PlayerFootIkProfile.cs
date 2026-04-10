using System;
using System.Collections.Generic;
using UnityEngine;

namespace Race.Player
{
    [CreateAssetMenu(fileName = "PlayerFootIkProfile", menuName = "Race/Player/Foot IK Profile")]
    public sealed class PlayerFootIkProfile : ScriptableObject
    {
        [SerializeField] private List<FootIkClipSettings> clipSettings = new();

        public IReadOnlyList<FootIkClipSettings> ClipSettings => clipSettings;

        public bool TryGetSettings(AnimationClip clip, out FootIkClipSettings settings)
        {
            if (clip != null)
            {
                for (int i = 0; i < clipSettings.Count; i++)
                {
                    FootIkClipSettings candidate = clipSettings[i];
                    if (candidate != null && candidate.Clip == clip)
                    {
                        settings = candidate;
                        return true;
                    }
                }
            }

            settings = null;
            return false;
        }

#if UNITY_EDITOR
        public FootIkClipSettings GetOrCreateSettings(string sourceClipName)
        {
            for (int i = 0; i < clipSettings.Count; i++)
            {
                FootIkClipSettings candidate = clipSettings[i];
                if (candidate != null && string.Equals(candidate.SourceClipName, sourceClipName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            var created = new FootIkClipSettings();
            created.SetSourceClipName(sourceClipName);
            clipSettings.Add(created);
            return created;
        }
#endif
    }

    [Serializable]
    public sealed class FootIkClipSettings
    {
        [SerializeField] private string sourceClipName;
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(1)] private int frameCount = 1;
        [SerializeField] private FootPlantWindowCollection leftFoot = new();
        [SerializeField] private FootPlantWindowCollection rightFoot = new();

        public string SourceClipName => sourceClipName;
        public AnimationClip Clip => clip;
        public int FrameCount => Mathf.Max(1, frameCount);
        public FootPlantWindowCollection LeftFoot => leftFoot;
        public FootPlantWindowCollection RightFoot => rightFoot;

        public bool TryEvaluate(AvatarIKGoal goal, float normalizedTime, out FootPlantWindow window)
        {
            FootPlantWindowCollection collection = goal == AvatarIKGoal.LeftFoot ? leftFoot : rightFoot;
            return collection.TryGetWindow(FrameCount, normalizedTime, out window);
        }

#if UNITY_EDITOR
        public void SetSourceClipName(string value)
        {
            sourceClipName = value;
        }

        public void SetClip(AnimationClip value)
        {
            clip = value;
            frameCount = CalculateFrameCount(value);
        }

        public void SeedDefaultsIfEmpty()
        {
            if (!leftFoot.HasWindows && !rightFoot.HasWindows)
            {
                SeedDefaultWindows(sourceClipName, FrameCount);
            }
        }

        private void SeedDefaultWindows(string clipName, int resolvedFrameCount)
        {
            int maxFrame = Mathf.Max(0, resolvedFrameCount - 1);
            leftFoot.Clear();
            rightFoot.Clear();

            if (string.Equals(clipName, PlayerAnimationProfile.IdleSourceClipName, StringComparison.Ordinal))
            {
                leftFoot.AddWindow(new FootPlantWindow(0, maxFrame));
                rightFoot.AddWindow(new FootPlantWindow(0, maxFrame));
                return;
            }

            int midpoint = Mathf.Max(0, maxFrame / 2);
            leftFoot.AddWindow(new FootPlantWindow(0, midpoint));
            rightFoot.AddWindow(new FootPlantWindow(Mathf.Min(midpoint + 1, maxFrame), maxFrame));
        }

        private static int CalculateFrameCount(AnimationClip value)
        {
            if (value == null)
            {
                return 1;
            }

            return Mathf.Max(1, Mathf.RoundToInt(value.length * value.frameRate));
        }
#endif
    }

    [Serializable]
    public sealed class FootPlantWindowCollection
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private List<FootPlantWindow> windows = new();

        public bool Enabled => enabled;
        public bool HasWindows => windows.Count > 0;
        public IReadOnlyList<FootPlantWindow> Windows => windows;

        public bool TryGetWindow(int frameCount, float normalizedTime, out FootPlantWindow window)
        {
            if (!enabled || windows.Count == 0)
            {
                window = default;
                return false;
            }

            int frame = NormalizedTimeToFrame(frameCount, normalizedTime);
            for (int i = 0; i < windows.Count; i++)
            {
                FootPlantWindow candidate = windows[i];
                if (candidate.Contains(frame, frameCount))
                {
                    window = candidate;
                    return true;
                }
            }

            window = default;
            return false;
        }

#if UNITY_EDITOR
        public void Clear()
        {
            windows.Clear();
        }

        public void AddWindow(FootPlantWindow window)
        {
            windows.Add(window);
        }
#endif

        private static int NormalizedTimeToFrame(int frameCount, float normalizedTime)
        {
            int resolvedFrameCount = Mathf.Max(1, frameCount);
            float wrapped = normalizedTime - Mathf.Floor(normalizedTime);
            int frame = Mathf.FloorToInt(wrapped * resolvedFrameCount);
            return Mathf.Clamp(frame, 0, resolvedFrameCount - 1);
        }
    }

    [Serializable]
    public struct FootPlantWindow
    {
        [SerializeField, Min(0)] private int startFrame;
        [SerializeField, Min(0)] private int endFrame;
        [SerializeField, Range(0f, 1f)] private float positionWeight;
        [SerializeField, Range(0f, 1f)] private float rotationWeight;

        public int StartFrame => startFrame;
        public int EndFrame => endFrame;
        public float PositionWeight => positionWeight;
        public float RotationWeight => rotationWeight;

        public FootPlantWindow(int startFrame, int endFrame, float positionWeight = 1f, float rotationWeight = 1f)
        {
            this.startFrame = Mathf.Max(0, startFrame);
            this.endFrame = Mathf.Max(0, endFrame);
            this.positionWeight = Mathf.Clamp01(positionWeight);
            this.rotationWeight = Mathf.Clamp01(rotationWeight);
        }

        public bool Contains(int frame, int frameCount)
        {
            int maxFrame = Mathf.Max(0, frameCount - 1);
            int clampedStart = Mathf.Clamp(startFrame, 0, maxFrame);
            int clampedEnd = Mathf.Clamp(endFrame, 0, maxFrame);

            if (clampedStart <= clampedEnd)
            {
                return frame >= clampedStart && frame <= clampedEnd;
            }

            return frame >= clampedStart || frame <= clampedEnd;
        }
    }
}
