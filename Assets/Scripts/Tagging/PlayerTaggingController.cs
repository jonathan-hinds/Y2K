using System.Collections.Generic;
using Race.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Race.Tagging
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerTaggingController : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> sprayVisualActive = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector3> sprayTargetPoint = new(writePerm: NetworkVariableWritePermission.Owner);
        private readonly List<Renderer> debugCandidateRenderers = new();
        private readonly List<Vector3> debugHitPoints = new();
        private readonly List<GraffitiSurfaceHitSample> debugProjectionSamples = new();

        [Header("References")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerRig playerRig;
        [SerializeField] private MousePlaneAimer aimer;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform sprayOrigin;
        [SerializeField] private ParticleSystem sprayParticles;

        [Header("Tagging")]
        [SerializeField] private LayerMask tagSurfaceMask = ~0;
        [SerializeField, Min(0.5f)] private float targetingRayDistance = 12f;
        [SerializeField, Min(0.5f)] private float projectionDistance = 3.4f;
        [SerializeField, Min(0.1f)] private float tagDuration = 5f;
        [SerializeField, Min(0.05f)] private float cancelDuration = 0.5f;

        [Header("Projection")]
        [SerializeField, Min(0.05f)] private float projectionAcquireRadius = 0.28f;
        [FormerlySerializedAs("projectionHeight")]
        [SerializeField, Min(0.1f)] private float tagSize = 1.45f;
        [SerializeField, Min(0.1f)] private float projectionAspect = 1f;
        [SerializeField, Min(0.05f)] private float projectionDepth = 0.65f;
        [SerializeField] private float projectionForwardOffset;

        [Header("Debug")]
        [SerializeField] private bool showProjectionDebug = true;
        [SerializeField, Range(1, 32)] private int debugMaxHitMarkers = 12;

        private struct TagTarget
        {
            public string SceneName;
            public Vector3 Point;
            public Vector3 SurfacePoint;
            public Vector3 SprayOrigin;
            public Vector3 Direction;
            public Vector3 Up;
            public float Size;
        }

        private bool isLocallyTagging;
        private bool awaitInteractRelease;
        private double localTagStartTime;
        private Vector3 currentTargetPoint;
        private GraffitiTagInstance activeLocalTagInstance;
        private GraffitiTagInstance activeServerTagInstance;
        private GraffitiProjectionDebugView projectionDebugView;
        private float MaxTagDistance => Mathf.Max(0.5f, projectionDistance);

        public bool IsTagging => isLocallyTagging;
        public float TagProgressNormalized => !isLocallyTagging
            ? 0f
            : Mathf.Clamp01((float)((Time.timeAsDouble - localTagStartTime) / Mathf.Max(0.01f, tagDuration)));

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            UpdateSprayVisuals();
        }

        private void OnDisable()
        {
            StopLocalTagging(keepWorldTag: true, requireRelease: false);
            SetSprayVisualState(false, Vector3.zero);
            UpdateSprayVisuals();
            projectionDebugView?.Dispose();
            projectionDebugView = null;
        }

        public override void OnNetworkDespawn()
        {
            StopLocalTagging(keepWorldTag: true, requireRelease: false);
            SetSprayVisualState(false, Vector3.zero);
        }

        private void Update()
        {
            UpdateSprayVisuals();
            UpdateProjectionDebug();

            if (!ShouldProcessLocalInput() || inputReader == null)
            {
                return;
            }

            if (isLocallyTagging)
            {
                if (inputReader.InputBlocked || !inputReader.InteractHeld)
                {
                    CancelActiveTag();
                    return;
                }

                if (TagProgressNormalized >= 0.999f)
                {
                    CompleteActiveTag();
                }

                return;
            }

            if (awaitInteractRelease)
            {
                if (!inputReader.InteractHeld)
                {
                    awaitInteractRelease = false;
                }

                return;
            }

            if (inputReader.InputBlocked || !inputReader.InteractHeld || !CanStartTagging())
            {
                return;
            }

            if (!TryResolveTagTarget(out TagTarget target))
            {
                return;
            }

            BeginLocalTag(target);
        }

        private void ResolveReferences()
        {
            if (inputReader == null)
            {
                inputReader = GetComponent<PlayerInputReader>();
            }

            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }

            if (playerRig == null)
            {
                playerRig = GetComponent<PlayerRig>();
            }

            if (aimer == null)
            {
                aimer = GetComponent<MousePlaneAimer>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (sprayOrigin == null)
            {
                Transform visualRoot = playerRig != null ? playerRig.VisualRoot : null;
                if (visualRoot != null)
                {
                    Transform candidate = visualRoot.Find("TagSprayPoint");
                    if (candidate != null)
                    {
                        sprayOrigin = candidate;
                    }
                }
            }

            if (sprayParticles == null && sprayOrigin != null)
            {
                sprayParticles = sprayOrigin.GetComponentInChildren<ParticleSystem>(true);
            }

            if (sprayOrigin == null && playerRig != null && playerRig.CameraTarget != null)
            {
                sprayOrigin = playerRig.CameraTarget;
            }

            ConfigureSprayParticles();
        }

        private bool CanStartTagging()
        {
            return playerMotor != null
                && playerMotor.CanBeginGroundedInteraction
                && !playerMotor.GameplayMovementLocked;
        }

        private bool TryResolveTagTarget(out TagTarget target)
        {
            target = default;
            if (!TryResolveProjection(out GraffitiProjectionVolume volume))
            {
                return false;
            }

            Vector3 origin = sprayOrigin != null ? sprayOrigin.position : transform.position + Vector3.up;
            if (GraffitiProjectionUtility.CollectSurfaceHitSamples(
                    volume,
                    origin,
                    transform.root,
                    tagSurfaceMask,
                    6,
                    6,
                    debugProjectionSamples,
                    debugCandidateRenderers,
                    null) <= 0)
            {
                return false;
            }

            target = new TagTarget
            {
                SceneName = volume.SceneName,
                Point = volume.Center,
                SurfacePoint = volume.SurfacePoint,
                SprayOrigin = origin,
                Direction = volume.Direction,
                Up = volume.Up,
                Size = tagSize
            };
            return true;
        }

        private bool TryResolveProjection(out GraffitiProjectionVolume volume)
        {
            volume = default;
            if (!TryBuildAimRay(out Ray aimRay))
            {
                return false;
            }

            Vector3 origin = sprayOrigin != null ? sprayOrigin.position : transform.position + Vector3.up;
            return GraffitiProjectionUtility.TryResolveProjection(
                aimRay,
                origin,
                transform.root,
                tagSurfaceMask,
                targetingRayDistance,
                MaxTagDistance,
                projectionAcquireRadius,
                tagSize,
                projectionAspect,
                projectionDepth,
                projectionForwardOffset,
                GetPreferredUp(),
                out volume);
        }

        private bool TryBuildAimRay(out Ray aimRay)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                aimRay = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                return true;
            }

            Vector3 origin = sprayOrigin != null ? sprayOrigin.position : transform.position + Vector3.up;
            Vector3 forward = aimer != null ? aimer.AimForward : transform.forward;
            aimRay = new Ray(origin, forward);
            return true;
        }

        private Vector3 GetPreferredUp()
        {
            if (targetCamera != null)
            {
                return targetCamera.transform.up;
            }

            if (playerRig != null && playerRig.CameraTarget != null)
            {
                return playerRig.CameraTarget.up;
            }

            return transform.up;
        }

        private void UpdateProjectionDebug()
        {
            if (!showProjectionDebug || !ShouldProcessLocalInput() || isLocallyTagging)
            {
                projectionDebugView?.Update(false, Vector3.zero, default, false, null);
                return;
            }

            EnsureProjectionDebugView();
            if (projectionDebugView == null || !TryBuildAimRay(out Ray aimRay))
            {
                return;
            }

            Vector3 origin = sprayOrigin != null ? sprayOrigin.position : transform.position + Vector3.up;
            if (TryResolveProjection(out GraffitiProjectionVolume resolvedVolume))
            {
                int candidateCount = GraffitiProjectionUtility.CollectSurfaceHitSamples(
                    resolvedVolume,
                    origin,
                    transform.root,
                    tagSurfaceMask,
                    6,
                    6,
                    debugProjectionSamples,
                    debugCandidateRenderers,
                    debugHitPoints);
                projectionDebugView.Update(true, origin, resolvedVolume, candidateCount > 0, debugHitPoints);
                return;
            }

            GraffitiProjectionVolume previewVolume = GraffitiProjectionUtility.BuildPreviewVolume(
                string.Empty,
                origin,
                aimRay.direction,
                MaxTagDistance,
                tagSize,
                projectionAspect,
                projectionDepth,
                projectionForwardOffset,
                GetPreferredUp());
            debugHitPoints.Clear();
            projectionDebugView.Update(true, origin, previewVolume, false, debugHitPoints);
        }

        private void EnsureProjectionDebugView()
        {
            if (projectionDebugView == null)
            {
                projectionDebugView = new GraffitiProjectionDebugView($"{name}_TagProjectionDebug", debugMaxHitMarkers);
            }
        }

        private void BeginLocalTag(TagTarget target)
        {
            isLocallyTagging = true;
            localTagStartTime = Time.timeAsDouble;
            currentTargetPoint = target.SurfacePoint;
            ApplyTaggingLocks(true);
            SetSprayVisualState(true, target.SurfacePoint);

            if (ShouldSimulateOffline())
            {
                BeginOfflineTag(target);
                return;
            }

            RequestBeginTagServerRpc(target.SceneName, target.Point, target.SurfacePoint, target.SprayOrigin, target.Direction, target.Up, target.Size);
        }

        private void BeginOfflineTag(TagTarget target)
        {
            GameObject prefab = Resources.Load<GameObject>(GraffitiTagInstance.ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"Unable to load graffiti tag prefab from Resources at '{GraffitiTagInstance.ResourcePath}'.", this);
                return;
            }

            GameObject instance = Instantiate(prefab);
            activeLocalTagInstance = instance.GetComponent<GraffitiTagInstance>();
            if (activeLocalTagInstance == null)
            {
                Destroy(instance);
                return;
            }

            activeLocalTagInstance.ConfigureTarget(target.SceneName, target.Point, target.SurfacePoint, target.SprayOrigin, target.Direction, target.Up, target.Size);
            activeLocalTagInstance.BeginLocalReveal(tagDuration);
        }

        private void CancelActiveTag()
        {
            if (!isLocallyTagging)
            {
                return;
            }

            if (ShouldSimulateOffline())
            {
                if (activeLocalTagInstance != null)
                {
                    activeLocalTagInstance.BeginLocalErase(cancelDuration);
                    activeLocalTagInstance = null;
                }
            }
            else
            {
                RequestCancelTagServerRpc();
            }

            StopLocalTagging(keepWorldTag: true, requireRelease: true);
        }

        private void CompleteActiveTag()
        {
            if (!isLocallyTagging)
            {
                return;
            }

            if (ShouldSimulateOffline())
            {
                if (activeLocalTagInstance != null)
                {
                    activeLocalTagInstance.CompleteLocal();
                    activeLocalTagInstance = null;
                }
            }
            else
            {
                RequestCompleteTagServerRpc();
            }

            StopLocalTagging(keepWorldTag: true, requireRelease: true);
        }

        private void StopLocalTagging(bool keepWorldTag, bool requireRelease)
        {
            if (!keepWorldTag && activeLocalTagInstance != null)
            {
                Destroy(activeLocalTagInstance.gameObject);
                activeLocalTagInstance = null;
            }

            isLocallyTagging = false;
            currentTargetPoint = Vector3.zero;
            awaitInteractRelease = requireRelease;
            ApplyTaggingLocks(false);
            SetSprayVisualState(false, Vector3.zero);
        }

        private void ApplyTaggingLocks(bool locked)
        {
            if (inputReader != null)
            {
                inputReader.GameplayMovementBlocked = locked;
                inputReader.GameplayLookBlocked = locked;
            }

            if (playerMotor != null)
            {
                playerMotor.SetGameplayMovementLocked(locked);
            }
        }

        private void SetSprayVisualState(bool active, Vector3 targetPoint)
        {
            if (ShouldSimulateOffline())
            {
                return;
            }

            if (IsSpawned && IsOwner)
            {
                sprayVisualActive.Value = active;
                sprayTargetPoint.Value = targetPoint;
            }
        }

        private void UpdateSprayVisuals()
        {
            ResolveReferences();

            bool shouldSpray = isLocallyTagging;
            Vector3 targetPoint = currentTargetPoint;
            if (!ShouldProcessLocalInput())
            {
                shouldSpray = IsSpawned && sprayVisualActive.Value;
                targetPoint = sprayTargetPoint.Value;
            }

            if (sprayOrigin != null && shouldSpray)
            {
                Vector3 direction = targetPoint - sprayOrigin.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    sprayOrigin.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            if (sprayParticles == null)
            {
                return;
            }

            if (shouldSpray)
            {
                if (!sprayParticles.isPlaying)
                {
                    sprayParticles.Play();
                }
            }
            else if (sprayParticles.isPlaying)
            {
                sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void ConfigureSprayParticles()
        {
            if (sprayParticles == null)
            {
                return;
            }

            if (sprayParticles.TryGetComponent(out ParticleSystemRenderer particleRenderer) && particleRenderer.sharedMaterial == null)
            {
                Material material = TaggingVfxMaterials.GetSprayMaterial();
                if (material != null)
                {
                    particleRenderer.sharedMaterial = material;
                }
            }
        }

        [ServerRpc]
        private void RequestBeginTagServerRpc(string sceneName, Vector3 point, Vector3 surfacePoint, Vector3 sprayStart, Vector3 direction, Vector3 up, float size)
        {
            if (activeServerTagInstance != null || !ValidateServerTarget(surfacePoint))
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(GraffitiTagInstance.ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"Unable to load graffiti tag prefab from Resources at '{GraffitiTagInstance.ResourcePath}'.", this);
                return;
            }

            GameObject instance = Instantiate(prefab);
            activeServerTagInstance = instance.GetComponent<GraffitiTagInstance>();
            if (activeServerTagInstance == null)
            {
                Destroy(instance);
                return;
            }

            activeServerTagInstance.ConfigureTarget(sceneName, point, surfacePoint, sprayStart, direction, up, size);
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Destroy(instance);
                activeServerTagInstance = null;
                return;
            }

            networkObject.Spawn(true);
            activeServerTagInstance.BeginRevealServer(tagDuration);
        }

        [ServerRpc]
        private void RequestCancelTagServerRpc()
        {
            if (activeServerTagInstance == null)
            {
                return;
            }

            activeServerTagInstance.BeginEraseServer(cancelDuration);
            activeServerTagInstance = null;
        }

        [ServerRpc]
        private void RequestCompleteTagServerRpc()
        {
            if (activeServerTagInstance == null)
            {
                return;
            }

            activeServerTagInstance.CompleteServer();
            activeServerTagInstance = null;
        }

        private bool ValidateServerTarget(Vector3 point)
        {
            Vector3 origin = sprayOrigin != null ? sprayOrigin.position : transform.position + Vector3.up;
            return Vector3.Distance(origin, point) <= MaxTagDistance + projectionDepth;
        }

        private bool ShouldProcessLocalInput()
        {
            if (IsSpawned)
            {
                return IsOwner;
            }

            return ShouldSimulateOffline();
        }

        private bool ShouldSimulateOffline()
        {
            return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        }
    }
}
