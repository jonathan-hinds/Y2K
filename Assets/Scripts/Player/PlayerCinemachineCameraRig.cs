using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace Race.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(CinemachineBrain))]
    public sealed class PlayerCinemachineCameraRig : MonoBehaviour
    {
        [SerializeField] private PlayerRig targetRig;
        [SerializeField] private PlayerInputReader targetInput;
        [SerializeField] private PlayerMotor targetMotor;
        [SerializeField] private CinemachineCamera gameplayCamera;
        [SerializeField] private string gameplayCameraName = "GameplayCamera";

        [Header("Orbit Pitch")]
        [Tooltip("Lowest pitch angle the player can orbit the camera down to.")]
        [SerializeField] private float minimumPitch = 4f;
        [Tooltip("Highest pitch angle the player can orbit the camera up to.")]
        [SerializeField] private float maximumPitch = 22f;
        [Tooltip("Default pitch angle used when the camera rig is created or reset.")]
        [SerializeField] private float defaultPitch = 10f;

        private void Reset()
        {
            targetRig = FindFirstObjectByType<PlayerRig>();
            targetInput = FindFirstObjectByType<PlayerInputReader>();
            targetMotor = FindFirstObjectByType<PlayerMotor>();
            gameplayCamera = GetComponentInChildren<CinemachineCamera>(true);
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureGameplayCamera();
            ConfigureRig(true);
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ResolveReferences();
            if (gameplayCamera == null)
            {
                gameplayCamera = GetComponentInChildren<CinemachineCamera>(true);
            }

            ConfigureRig(false);
        }

        public void SetTarget(PlayerRig rig, PlayerInputReader input, PlayerMotor motor)
        {
            targetRig = rig;
            targetInput = input;
            targetMotor = motor;
            ConfigureRig(true);
        }

        private void ResolveReferences()
        {
            if (targetRig == null)
            {
                targetRig = FindFirstObjectByType<PlayerRig>();
            }

            if (targetInput == null)
            {
                targetInput = FindFirstObjectByType<PlayerInputReader>();
            }

            if (targetMotor == null)
            {
                targetMotor = FindFirstObjectByType<PlayerMotor>();
            }
        }

        private void EnsureGameplayCamera()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = GetComponentInChildren<CinemachineCamera>(true);
            }

            if (gameplayCamera != null)
            {
                return;
            }

            GameObject cameraRoot = new GameObject(gameplayCameraName);
            cameraRoot.transform.SetParent(transform, false);
            gameplayCamera = cameraRoot.AddComponent<CinemachineCamera>();
        }

        private void ConfigureRig(bool createMissingComponents)
        {
            if (gameplayCamera == null)
            {
                return;
            }

            Transform cameraTarget = targetRig != null && targetRig.CameraTarget != null
                ? targetRig.CameraTarget
                : targetRig != null ? targetRig.transform : null;

            gameplayCamera.Follow = cameraTarget;
            gameplayCamera.LookAt = cameraTarget;
            gameplayCamera.BlendHint = CinemachineCore.BlendHints.InheritPosition;

            ConfigureLens(gameplayCamera);
            CinemachineOrbitalFollow orbitalFollow = GetOrAddComponent<CinemachineOrbitalFollow>(gameplayCamera.gameObject, createMissingComponents);
            if (orbitalFollow != null)
            {
                ConfigureOrbitalFollow(orbitalFollow);
            }

            CinemachineRotationComposer composer = GetOrAddComponent<CinemachineRotationComposer>(gameplayCamera.gameObject, createMissingComponents);
            if (composer != null)
            {
                ConfigureComposer(composer);
            }

            PlayerCameraOrbitInput orbitInput = GetOrAddComponent<PlayerCameraOrbitInput>(gameplayCamera.gameObject, createMissingComponents);
            if (orbitInput != null)
            {
                ConfigureOrbitInput(orbitInput);
            }

            PlayerCameraSpeedEffects speedEffects = GetOrAddComponent<PlayerCameraSpeedEffects>(gameplayCamera.gameObject, createMissingComponents);
            if (speedEffects != null)
            {
                ConfigureSpeedEffects(speedEffects);
                speedEffects.ApplyImmediate();
            }

            CinemachineDeoccluder deoccluder = GetOrAddComponent<CinemachineDeoccluder>(gameplayCamera.gameObject, createMissingComponents);
            if (deoccluder != null)
            {
                ConfigureDeoccluder(deoccluder);
            }

            CinemachineDecollider decollider = GetOrAddComponent<CinemachineDecollider>(gameplayCamera.gameObject, createMissingComponents);
            if (decollider != null)
            {
                ConfigureDecollider(decollider);
            }
        }

        private static void ConfigureLens(CinemachineCamera virtualCamera)
        {
            LensSettings lens = virtualCamera.Lens;
            if (lens.FieldOfView <= 0.01f)
            {
                lens = LensSettings.Default;
            }

            lens.FieldOfView = 60f;
            virtualCamera.Lens = lens;
        }

        private void ConfigureOrbitalFollow(CinemachineOrbitalFollow orbitalFollow)
        {
            orbitalFollow.TargetOffset = Vector3.zero;
            orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbitalFollow.Radius = 8.2f;
            orbitalFollow.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.AxisCenter;

            TrackerSettings trackerSettings = orbitalFollow.TrackerSettings;
            trackerSettings.BindingMode = BindingMode.WorldSpace;
            trackerSettings.PositionDamping = new Vector3(0.15f, 0.25f, 0.15f);
            trackerSettings.AngularDampingMode = AngularDampingMode.Euler;
            trackerSettings.RotationDamping = Vector3.zero;
            orbitalFollow.TrackerSettings = trackerSettings;

            InputAxis horizontalAxis = orbitalFollow.HorizontalAxis;
            horizontalAxis.Range = new Vector2(-180f, 180f);
            horizontalAxis.Wrap = true;
            horizontalAxis.Center = 0f;
            horizontalAxis.Value = 0f;
            horizontalAxis.Recentering.Enabled = false;
            orbitalFollow.HorizontalAxis = horizontalAxis;

            InputAxis verticalAxis = orbitalFollow.VerticalAxis;
            float clampedMinPitch = Mathf.Min(minimumPitch, maximumPitch);
            float clampedMaxPitch = Mathf.Max(minimumPitch, maximumPitch);
            float clampedDefaultPitch = Mathf.Clamp(defaultPitch, clampedMinPitch, clampedMaxPitch);
            verticalAxis.Range = new Vector2(clampedMinPitch, clampedMaxPitch);
            verticalAxis.Wrap = false;
            verticalAxis.Center = clampedDefaultPitch;
            verticalAxis.Value = Mathf.Clamp(verticalAxis.Value, clampedMinPitch, clampedMaxPitch);
            if (!Application.isPlaying)
            {
                verticalAxis.Value = clampedDefaultPitch;
            }
            verticalAxis.Recentering.Enabled = false;
            orbitalFollow.VerticalAxis = verticalAxis;

            InputAxis radialAxis = orbitalFollow.RadialAxis;
            radialAxis.Range = new Vector2(1f, 1f);
            radialAxis.Center = 1f;
            radialAxis.Value = 1f;
            radialAxis.Wrap = false;
            radialAxis.Recentering.Enabled = false;
            orbitalFollow.RadialAxis = radialAxis;
        }

        private static void ConfigureComposer(CinemachineRotationComposer composer)
        {
            composer.TargetOffset = Vector3.zero;
            composer.CenterOnActivate = false;
            composer.Damping = new Vector2(0.08f, 0.06f);
            composer.Lookahead = new LookaheadSettings
            {
                Enabled = false,
                Time = 0f,
                Smoothing = 0f,
                IgnoreY = true
            };

            ScreenComposerSettings composition = composer.Composition;
            composition.ScreenPosition = Vector2.zero;
            composition.DeadZone.Enabled = true;
            composition.DeadZone.Size = new Vector2(0.14f, 0.14f);
            composition.HardLimits.Enabled = true;
            composition.HardLimits.Size = new Vector2(0.65f, 0.6f);
            composition.HardLimits.Offset = Vector2.zero;
            composer.Composition = composition;
        }

        private void ConfigureOrbitInput(PlayerCameraOrbitInput orbitInput)
        {
            orbitInput.SetInputReader(targetInput);
        }

        private void ConfigureSpeedEffects(PlayerCameraSpeedEffects speedEffects)
        {
            speedEffects.SetTargetMotor(targetMotor);
        }

        private static void ConfigureDeoccluder(CinemachineDeoccluder deoccluder)
        {
            deoccluder.enabled = false;
            deoccluder.CollideAgainst = ~0;
            deoccluder.TransparentLayers = 0;
            deoccluder.IgnoreTag = string.Empty;
            deoccluder.MinimumDistanceFromTarget = 0.5f;
            deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                MinimumOcclusionTime = 0f,
                CameraRadius = 0.3f,
                UseFollowTarget = new CinemachineDeoccluder.ObstacleAvoidance.FollowTargetSettings
                {
                    Enabled = false,
                    YOffset = 0f
                },
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PreserveCameraDistance,
                MaximumEffort = 6,
                SmoothingTime = 0.05f,
                Damping = 0.3f,
                DampingWhenOccluded = 0.15f
            };
        }

        private static void ConfigureDecollider(CinemachineDecollider decollider)
        {
            decollider.enabled = false;
            decollider.CameraRadius = 0.3f;
            decollider.Decollision = new CinemachineDecollider.DecollisionSettings
            {
                Enabled = true,
                ObstacleLayers = ~0,
                UseFollowTarget = new CinemachineDecollider.DecollisionSettings.FollowTargetSettings
                {
                    Enabled = false,
                    YOffset = 0f
                },
                Damping = 0.2f,
                SmoothingTime = 0.05f
            };

            decollider.TerrainResolution = new CinemachineDecollider.TerrainSettings
            {
                Enabled = false,
                TerrainLayers = ~0,
                MaximumRaycast = 10f,
                Damping = 0.5f
            };
        }

        private static T GetOrAddComponent<T>(GameObject target, bool createIfMissing) where T : Component
        {
            if (!target.TryGetComponent(out T component))
            {
                if (!createIfMissing)
                {
                    return null;
                }

                component = target.AddComponent<T>();
            }

            return component;
        }
    }
}
