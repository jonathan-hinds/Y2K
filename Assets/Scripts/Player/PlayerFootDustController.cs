using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Player
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PlayerFootDustController : MonoBehaviour
    {
        [System.Serializable]
        private sealed class FootDustEmitter
        {
            [SerializeField] private string displayName;
            [SerializeField] private AvatarIKGoal goal;
            [SerializeField] private Transform emissionPoint;
            [SerializeField] private ParticleSystem particleSystem;

            public string DisplayName => displayName;
            public AvatarIKGoal Goal => goal;
            public Transform EmissionPoint => emissionPoint;
            public ParticleSystem ParticleSystem => particleSystem;

            public void SetDisplayName(string value)
            {
                displayName = value;
            }

            public void SetGoal(AvatarIKGoal value)
            {
                goal = value;
            }
        }

        [Header("References")]
        [SerializeField] private PlayerAnimator playerAnimator;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private Material fallbackDustMaterial;
        [SerializeField] private FootDustEmitter leftFoot = new();
        [SerializeField] private FootDustEmitter rightFoot = new();

        [Header("Emission")]
        [SerializeField] private bool emitDust = true;
        [SerializeField, Range(0f, 1f)] private float plantWeightThreshold = 0.3f;
        [SerializeField, Range(0f, 1.5f)] private float minMoveMagnitude = 0.08f;
        [SerializeField, Range(0f, 1.5f)] private float moveMagnitudeForMaxEmission = 1f;
        [SerializeField, Min(0f)] private float maxEmissionRate = 42f;
        [SerializeField, Min(0)] private int touchdownBurstCount = 14;
        [SerializeField, Min(0f)] private float burstCooldown = 0.08f;

        [Header("Color")]
        [SerializeField] private Color lightSmokeColor = new(0.72f, 0.72f, 0.72f, 0.48f);
        [SerializeField] private Color darkSmokeColor = new(0.46f, 0.46f, 0.46f, 0.68f);
        [SerializeField] private Color trailColor = new(0.63f, 0.63f, 0.63f, 0.42f);

        [Header("Debug")]
        [SerializeField] private bool showEmitterGizmos = true;
        [SerializeField] private Color leftGizmoColor = new(0.94f, 0.76f, 0.33f, 0.9f);
        [SerializeField] private Color rightGizmoColor = new(0.88f, 0.47f, 0.29f, 0.9f);
        [SerializeField, Min(0.01f)] private float gizmoRadius = 0.08f;

        private readonly List<AnimatorClipInfo> currentClipInfos = new();
        private readonly List<AnimatorClipInfo> nextClipInfos = new();

        private bool leftWasActive;
        private bool rightWasActive;
        private float leftLastBurstTime = float.NegativeInfinity;
        private float rightLastBurstTime = float.NegativeInfinity;

        private void Awake()
        {
            ResolveReferences();
            ApplyVisualSettings();
        }

        private void Reset()
        {
            leftFoot.SetDisplayName("Left Foot");
            leftFoot.SetGoal(AvatarIKGoal.LeftFoot);
            rightFoot.SetDisplayName("Right Foot");
            rightFoot.SetGoal(AvatarIKGoal.RightFoot);
            ResolveReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            leftFoot.SetDisplayName("Left Foot");
            leftFoot.SetGoal(AvatarIKGoal.LeftFoot);
            rightFoot.SetDisplayName("Right Foot");
            rightFoot.SetGoal(AvatarIKGoal.RightFoot);
            ResolveReferences();
            ApplyVisualSettings();
        }
#endif

        private void LateUpdate()
        {
            ResolveReferences();
            if (!ShouldEvaluateDust())
            {
                UpdateEmitter(leftFoot, 0f, ref leftWasActive, ref leftLastBurstTime);
                UpdateEmitter(rightFoot, 0f, ref rightWasActive, ref rightLastBurstTime);
                return;
            }

            float moveMagnitude = ResolveMoveMagnitude();
            bool canEmit = CanEmitDust() && moveMagnitude >= minMoveMagnitude;
            float moveFactor = Mathf.InverseLerp(minMoveMagnitude, Mathf.Max(minMoveMagnitude, moveMagnitudeForMaxEmission), moveMagnitude);

            UpdateEmitter(
                leftFoot,
                canEmit ? PlayerFootPlantWeightEvaluator.Evaluate(playerAnimator, leftFoot.Goal, currentClipInfos, nextClipInfos) * moveFactor : 0f,
                ref leftWasActive,
                ref leftLastBurstTime);
            UpdateEmitter(
                rightFoot,
                canEmit ? PlayerFootPlantWeightEvaluator.Evaluate(playerAnimator, rightFoot.Goal, currentClipInfos, nextClipInfos) * moveFactor : 0f,
                ref rightWasActive,
                ref rightLastBurstTime);
        }

        private bool ShouldEvaluateDust()
        {
            if (!emitDust || playerAnimator == null || playerAnimator.Animator == null || playerAnimator.AnimationProfile == null)
            {
                return false;
            }

            if (playerAnimator.AnimationProfile.FootIkProfile == null)
            {
                return false;
            }

            return leftFoot.ParticleSystem != null && rightFoot.ParticleSystem != null;
        }

        private void ResolveReferences()
        {
            if (playerAnimator == null)
            {
                playerAnimator = GetComponent<PlayerAnimator>();
            }

            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }
        }

        private float ResolveMoveMagnitude()
        {
            return Mathf.Clamp(playerAnimator.CurrentState.MoveMagnitude, 0f, 1.5f);
        }

        private bool ResolveGrounded()
        {
            if (playerMotor != null && playerMotor.enabled)
            {
                return playerMotor.HasStableGroundContact;
            }

            return playerAnimator.CurrentState.IsGrounded;
        }

        private bool CanEmitDust()
        {
            if (!ResolveGrounded())
            {
                return false;
            }

            if (playerMotor == null || !playerMotor.enabled)
            {
                return true;
            }

            if (playerMotor.IsGrinding || playerMotor.IsWallRiding)
            {
                return false;
            }

            if (!playerMotor.IsGrounded)
            {
                return false;
            }

            return playerMotor.CurrentJumpPhase == PlayerMotor.JumpPhase.Grounded
                || playerMotor.CurrentJumpPhase == PlayerMotor.JumpPhase.Landing;
        }

        private void UpdateEmitter(FootDustEmitter emitter, float emissionStrength, ref bool wasActive, ref float lastBurstTime)
        {
            ParticleSystem system = emitter.ParticleSystem;
            if (system == null)
            {
                wasActive = false;
                return;
            }

            bool isActive = emissionStrength >= plantWeightThreshold;
            var emission = system.emission;
            emission.rateOverTimeMultiplier = isActive ? maxEmissionRate * Mathf.Clamp01(emissionStrength) : 0f;

            if (isActive)
            {
                if (!system.isPlaying)
                {
                    system.Play(true);
                }

                if (!wasActive && touchdownBurstCount > 0 && (Time.time - lastBurstTime) >= burstCooldown)
                {
                    system.Emit(touchdownBurstCount);
                    lastBurstTime = Time.time;
                }
            }

            wasActive = isActive;
        }

        private void OnDisable()
        {
            StopEmitter(leftFoot.ParticleSystem);
            StopEmitter(rightFoot.ParticleSystem);
            leftWasActive = false;
            rightWasActive = false;
        }

        private static void StopEmitter(ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }

            var emission = system.emission;
            emission.rateOverTimeMultiplier = 0f;
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showEmitterGizmos)
            {
                return;
            }

            DrawEmitterGizmo(leftFoot, leftGizmoColor);
            DrawEmitterGizmo(rightFoot, rightGizmoColor);
        }

        private void DrawEmitterGizmo(FootDustEmitter emitter, Color color)
        {
            Transform point = emitter.EmissionPoint;
            if (point == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawSphere(point.position, gizmoRadius);
            Gizmos.DrawLine(point.position, point.position + point.up * gizmoRadius * 2f);

#if UNITY_EDITOR
            Handles.color = color;
            Handles.Label(point.position + Vector3.up * gizmoRadius * 1.5f, emitter.DisplayName);
#endif
        }

        private void ApplyVisualSettings()
        {
            ApplyVisualSettings(leftFoot.ParticleSystem);
            ApplyVisualSettings(rightFoot.ParticleSystem);
        }

        private void ApplyVisualSettings(ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }

            ApplyRendererMaterial(system);

            var main = system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(lightSmokeColor, darkSmokeColor);

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(lightSmokeColor, 0f),
                    new GradientColorKey(Color.Lerp(lightSmokeColor, darkSmokeColor, 0.55f), 0.5f),
                    new GradientColorKey(darkSmokeColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(Mathf.Max(lightSmokeColor.a, darkSmokeColor.a), 0.08f),
                    new GradientAlphaKey(trailColor.a, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var trails = system.trails;
            if (trails.enabled)
            {
                Gradient trailGradient = new();
                trailGradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(trailColor, 0f),
                        new GradientColorKey(darkSmokeColor, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(trailColor.a, 0f),
                        new GradientAlphaKey(0f, 1f)
                    });
                trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(trailGradient);
            }
        }

        private void ApplyRendererMaterial(ParticleSystem system)
        {
            if (fallbackDustMaterial == null)
            {
                return;
            }

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null)
            {
                return;
            }

            Material[] currentMaterials = particleRenderer.sharedMaterials;
            int materialCount = Mathf.Max(1, currentMaterials != null ? currentMaterials.Length : 0);
            Material[] replacementMaterials = new Material[materialCount];
            for (int index = 0; index < materialCount; index++)
            {
                replacementMaterials[index] = fallbackDustMaterial;
            }

            particleRenderer.sharedMaterials = replacementMaterials;
        }
    }
}
