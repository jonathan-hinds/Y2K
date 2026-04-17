using System;
using System.Collections.Generic;
using PaintCore;
using PaintIn3D;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Race.Tagging
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class GraffitiTagInstance : NetworkBehaviour
    {
        public const string ResourcePath = "Tagging/GraffitiTagInstance";

        private struct SprayStampHit
        {
            public GraffitiPaintSurface Surface;
            public Vector3 Point;
            public Vector3 Normal;
            public Color Color;
            public float Opacity;
            public float Radius;
            public float RevealValue;
        }

        private enum Lifecycle : byte
        {
            Hidden = 0,
            Revealing = 1,
            Erasing = 2,
            Completed = 3
        }

        private struct PlaybackState : INetworkSerializable, IEquatable<PlaybackState>
        {
            public byte Lifecycle;
            public float StartProgress;
            public float TargetProgress;
            public float Duration;
            public double StartTime;

            public bool Equals(PlaybackState other)
            {
                return Lifecycle == other.Lifecycle
                    && Mathf.Approximately(StartProgress, other.StartProgress)
                    && Mathf.Approximately(TargetProgress, other.TargetProgress)
                    && Mathf.Approximately(Duration, other.Duration)
                    && StartTime.Equals(other.StartTime);
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Lifecycle);
                serializer.SerializeValue(ref StartProgress);
                serializer.SerializeValue(ref TargetProgress);
                serializer.SerializeValue(ref Duration);
                serializer.SerializeValue(ref StartTime);
            }
        }

        private struct TargetState : INetworkSerializable, IEquatable<TargetState>
        {
            public FixedString128Bytes SceneName;
            public FixedString512Bytes RendererPath;
            public Vector3 Point;
            public Vector3 SurfacePoint;
            public Vector3 SprayOrigin;
            public Vector3 Direction;
            public Vector3 Up;
            public float Size;

            public bool Equals(TargetState other)
            {
                return SceneName.Equals(other.SceneName)
                    && RendererPath.Equals(other.RendererPath)
                    && Point == other.Point
                    && SurfacePoint == other.SurfacePoint
                    && SprayOrigin == other.SprayOrigin
                    && Direction == other.Direction
                    && Up == other.Up
                    && Mathf.Approximately(Size, other.Size);
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref SceneName);
                serializer.SerializeValue(ref RendererPath);
                serializer.SerializeValue(ref Point);
                serializer.SerializeValue(ref SurfacePoint);
                serializer.SerializeValue(ref SprayOrigin);
                serializer.SerializeValue(ref Direction);
                serializer.SerializeValue(ref Up);
                serializer.SerializeValue(ref Size);
            }
        }

        [Header("References")]
        [SerializeField] private ParticleSystem impactCloudParticles;
        [SerializeField] private CwPaintSphere sprayBrush;
        [SerializeField] private CwPaintDecal applyDecal;
        [SerializeField] private CwPaintDecal eraseDecal;
        [SerializeField] private Texture2D tagTexture;
        [SerializeField] private MeshRenderer previewRenderer;

        [Header("Stamp")]
        [SerializeField, Min(0.1f)] private float tagHeight = 1.45f;
        [SerializeField, Min(0.01f)] private float updateInterval = 1f / 12f;
        [SerializeField, Range(4, 128)] private int revealSteps = 48;
        [SerializeField, Min(0.1f)] private float hardness = 4f;
        [SerializeField, Range(0f, 1f)] private float wrapping = 1f;
        [SerializeField, Range(0f, 1f)] private float noiseBias = 0.35f;
        [SerializeField, Min(0.05f)] private float projectionDepth = 0.65f;
        [SerializeField, Min(0f)] private float previewSurfaceOffset = 0.02f;
        [SerializeField, Range(12, 96)] private int spraySampleRows = 48;
        [SerializeField, Range(0.25f, 1.75f)] private float sprayDotRadiusMultiplier = 0.7f;
        [SerializeField, Range(0.05f, 0.5f)] private float sprayDepthFlattening = 0.18f;
        [SerializeField, Range(0.001f, 0.25f)] private float sprayAlphaThreshold = 0.02f;

        private readonly NetworkVariable<TargetState> networkTarget = new(writePerm: NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<PlaybackState> networkPlayback = new(writePerm: NetworkVariableWritePermission.Server);
        private readonly List<GraffitiPaintSurface> resolvedSurfaces = new();
        private readonly List<Renderer> candidateRenderers = new();
        private readonly List<GraffitiSurfaceHitSample> resolvedProjectionSamples = new();
        private readonly List<SprayStampHit> resolvedStampHits = new();

        private Texture2D generatedMaskTexture;
        private Material previewMaterialInstance;
        private Color32[] sourcePixels;
        private Color32[] workingPixels;
        private float[] revealNoise;
        private PlaybackState localPlayback;
        private TargetState localTarget;
        private bool useLocalPlayback;
        private bool despawnQueued;
        private bool finalStampApplied;
        private float lastRefreshTime = float.NegativeInfinity;
        private int lastAppliedStep = -1;

        private void Awake()
        {
            ResolveReferences();
            DisableLegacyVisuals();
            EnsureBrushSetup();
            UpdateImpactCloud(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            DisableLegacyVisuals();
            EnsureBrushSetup();
            RefreshVisualState(true);
        }

        private void Update()
        {
            RefreshVisualState(false);

            if (!IsServer || !IsSpawned || despawnQueued)
            {
                return;
            }

            PlaybackState playback = networkPlayback.Value;
            float normalized = EvaluateProgress(playback, GetCurrentTime());
            Lifecycle lifecycle = (Lifecycle)playback.Lifecycle;

            if (lifecycle == Lifecycle.Revealing && normalized >= 0.999f)
            {
                networkPlayback.Value = new PlaybackState
                {
                    Lifecycle = (byte)Lifecycle.Completed,
                    StartProgress = 1f,
                    TargetProgress = 1f,
                    Duration = 0f,
                    StartTime = GetCurrentTime()
                };
            }
            else if (lifecycle == Lifecycle.Erasing && normalized <= 0.001f)
            {
                despawnQueued = true;
                if (NetworkObject != null && NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn(true);
                }
            }
        }

        public override void OnDestroy()
        {
            if (generatedMaskTexture != null)
            {
                Destroy(generatedMaskTexture);
            }

            if (previewMaterialInstance != null)
            {
                Destroy(previewMaterialInstance);
            }

            base.OnDestroy();
        }

        public void ConfigureTarget(string sceneName, string rendererPath, Vector3 point, Vector3 surfacePoint, Vector3 sprayOrigin, Vector3 direction, Vector3 surfaceUp, float size)
        {
            TargetState target = new()
            {
                SceneName = sceneName,
                RendererPath = rendererPath,
                Point = point,
                SurfacePoint = surfacePoint,
                SprayOrigin = sprayOrigin,
                Direction = direction,
                Up = surfaceUp,
                Size = size
            };

            if (IsServer)
            {
                networkTarget.Value = target;
            }

            localTarget = target;
            resolvedSurfaces.Clear();
            candidateRenderers.Clear();
            resolvedProjectionSamples.Clear();
            resolvedStampHits.Clear();
            finalStampApplied = false;
            lastAppliedStep = 0;
            ApplyTargetTransform(target);
        }

        public void BeginRevealServer(float durationSeconds)
        {
            if (!IsServer)
            {
                return;
            }

            networkPlayback.Value = new PlaybackState
            {
                Lifecycle = (byte)Lifecycle.Revealing,
                StartProgress = 0f,
                TargetProgress = 1f,
                Duration = Mathf.Max(0.01f, durationSeconds),
                StartTime = GetCurrentTime()
            };
        }

        public void BeginEraseServer(float durationSeconds)
        {
            if (!IsServer)
            {
                return;
            }

            float currentProgress = EvaluateProgress(networkPlayback.Value, GetCurrentTime());
            networkPlayback.Value = new PlaybackState
            {
                Lifecycle = (byte)Lifecycle.Erasing,
                StartProgress = currentProgress,
                TargetProgress = 0f,
                Duration = Mathf.Max(0.01f, durationSeconds),
                StartTime = GetCurrentTime()
            };
        }

        public void CompleteServer()
        {
            if (!IsServer)
            {
                return;
            }

            networkPlayback.Value = new PlaybackState
            {
                Lifecycle = (byte)Lifecycle.Completed,
                StartProgress = 1f,
                TargetProgress = 1f,
                Duration = 0f,
                StartTime = GetCurrentTime()
            };
        }

        public void BeginLocalReveal(float durationSeconds)
        {
            useLocalPlayback = true;
            lastAppliedStep = -1;
            localPlayback = new PlaybackState
            {
                Lifecycle = (byte)Lifecycle.Revealing,
                StartProgress = 0f,
                TargetProgress = 1f,
                Duration = Mathf.Max(0.01f, durationSeconds),
                StartTime = GetCurrentTime()
            };
        }

        public void BeginLocalErase(float durationSeconds)
        {
            useLocalPlayback = true;
            float currentProgress = EvaluateProgress(localPlayback, GetCurrentTime());
            localPlayback = new PlaybackState
            {
                Lifecycle = (byte)Lifecycle.Erasing,
                StartProgress = currentProgress,
                TargetProgress = 0f,
                Duration = Mathf.Max(0.01f, durationSeconds),
                StartTime = GetCurrentTime()
            };
        }

        public void CompleteLocal()
        {
            useLocalPlayback = true;
            localPlayback = new PlaybackState
            {
                Lifecycle = (byte)Lifecycle.Completed,
                StartProgress = 1f,
                TargetProgress = 1f,
                Duration = 0f,
                StartTime = GetCurrentTime()
            };
        }

        private void ResolveReferences()
        {
            if (impactCloudParticles == null)
            {
                impactCloudParticles = GetComponentInChildren<ParticleSystem>(true);
            }

            if (previewRenderer == null)
            {
                previewRenderer = GetComponentInChildren<MeshRenderer>(true);
            }

            if (sprayBrush == null)
            {
                sprayBrush = GetComponent<CwPaintSphere>();
            }

            if (sprayBrush == null)
            {
                sprayBrush = gameObject.AddComponent<CwPaintSphere>();
            }

            CwPaintDecal[] decals = GetComponents<CwPaintDecal>();
            if (applyDecal == null && decals.Length > 0)
            {
                applyDecal = decals[0];
            }

            if (applyDecal == null)
            {
                applyDecal = gameObject.AddComponent<CwPaintDecal>();
            }

            if (eraseDecal == null && decals.Length > 1)
            {
                eraseDecal = decals[1];
            }

            if (eraseDecal == null)
            {
                eraseDecal = gameObject.AddComponent<CwPaintDecal>();
            }

            ConfigureImpactCloudParticles();
        }

        private void DisableLegacyVisuals()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] is ParticleSystemRenderer)
                {
                    continue;
                }

                if (previewRenderer != null && renderers[index] == previewRenderer)
                {
                    renderers[index].enabled = false;
                    continue;
                }

                renderers[index].enabled = false;
            }
        }

        private void EnsureBrushSetup()
        {
            if (tagTexture == null)
            {
                Debug.LogWarning("Graffiti tag texture is missing from the Paint in 3D graffiti session prefab.", this);
                return;
            }

            if (!tagTexture.isReadable)
            {
                Debug.LogWarning($"Graffiti tag texture '{tagTexture.name}' must have Read/Write enabled for runtime reveal masking.", this);
                return;
            }

            if (generatedMaskTexture == null)
            {
                generatedMaskTexture = new Texture2D(tagTexture.width, tagTexture.height, TextureFormat.RGBA32, false)
                {
                    name = $"{tagTexture.name}_RuntimeMask",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            if (sourcePixels == null || sourcePixels.Length != tagTexture.width * tagTexture.height)
            {
                sourcePixels = tagTexture.GetPixels32();
                workingPixels = new Color32[sourcePixels.Length];
                revealNoise = new float[sourcePixels.Length];
                BuildRevealNoise();
            }

            ConfigureSprayBrush();
            ConfigureDecal(applyDecal, CwBlendMode.AlphaBlend(Vector4.one));
            ConfigureDecal(eraseDecal, CwBlendMode.ReplaceOriginal(Vector4.one));
            EnsurePreviewMaterial();
        }

        private void ConfigureSprayBrush()
        {
            if (sprayBrush == null)
            {
                return;
            }

            sprayBrush.BlendMode = CwBlendMode.AlphaBlend(Vector4.one);
            sprayBrush.Scale = new Vector3(1f, 1f, Mathf.Clamp(sprayDepthFlattening, 0.05f, 0.5f));
            sprayBrush.Hardness = hardness;
            sprayBrush.Opacity = 1f;
            sprayBrush.Color = Color.white;
            sprayBrush.Radius = 0.05f;
            sprayBrush.Layers = applyDecal != null ? applyDecal.Layers : ~0;
        }

        private void ConfigureDecal(CwPaintDecal decal, CwBlendMode blendMode)
        {
            if (decal == null)
            {
                return;
            }

            float aspect = tagTexture != null && tagTexture.height > 0
                ? tagTexture.width / (float)tagTexture.height
                : 1f;

            decal.BlendMode = blendMode;
            decal.Texture = generatedMaskTexture;
            decal.Shape = generatedMaskTexture;
            decal.ShapeChannel = CwChannel.Alpha;
            decal.Color = Color.white;
            decal.Opacity = 1f;
            decal.Scale = new Vector3(aspect, 1f, 1f);
            decal.Radius = tagHeight * 0.5f;
            decal.Hardness = hardness;
            decal.Wrapping = wrapping;
            decal.NormalFront = 1f;
            decal.NormalBack = 0f;
            decal.NormalFade = 0.01f;
        }

        private void RefreshVisualState(bool force)
        {
            if (!force && Time.time < lastRefreshTime + updateInterval)
            {
                return;
            }

            lastRefreshTime = Time.time;

            PlaybackState playback = useLocalPlayback || !IsSpawned
                ? localPlayback
                : networkPlayback.Value;
            TargetState target = useLocalPlayback || !IsSpawned
                ? localTarget
                : networkTarget.Value;

            ApplyTargetTransform(target);

            if (!TryResolveSurfaces(target))
            {
                UpdatePreviewVisual(target, playback);
                UpdateImpactCloud((Lifecycle)playback.Lifecycle == Lifecycle.Revealing);
                return;
            }

            float progress = EvaluateProgress(playback, GetCurrentTime());
            Lifecycle lifecycle = (Lifecycle)playback.Lifecycle;
            ApplyProgress(target, playback, progress);
            if (lifecycle == Lifecycle.Completed)
            {
                EnsurePersistentStamp(target);
            }
            else
            {
                finalStampApplied = false;
            }
            UpdatePreviewVisual(target, playback);
            UpdateImpactCloud((Lifecycle)playback.Lifecycle == Lifecycle.Revealing);

            if (!useLocalPlayback || IsSpawned)
            {
                return;
            }

            if (lifecycle == Lifecycle.Revealing && progress >= 0.999f)
            {
                CompleteLocal();
            }
            else if (lifecycle == Lifecycle.Erasing && progress <= 0.001f)
            {
                lastAppliedStep = 0;
                Destroy(gameObject);
            }
        }

        private bool TryResolveSurfaces(TargetState target)
        {
            if (target.Direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            if (resolvedStampHits.Count > 0)
            {
                return true;
            }

            resolvedSurfaces.Clear();
            candidateRenderers.Clear();
            resolvedProjectionSamples.Clear();
            resolvedStampHits.Clear();

            GraffitiProjectionVolume volume = BuildProjectionVolume(target);
            int sampleRows = ResolveSampleRows();
            int sampleColumns = ResolveSampleColumns();
            if (GraffitiProjectionUtility.CollectSurfaceHitSamples(
                volume,
                ResolveSprayOrigin(target),
                null,
                applyDecal != null ? applyDecal.Layers : ~0,
                sampleColumns,
                sampleRows,
                resolvedProjectionSamples,
                candidateRenderers,
                null) <= 0)
            {
                return false;
            }

            float baseRadius = ResolveBaseSprayRadius(target, sampleColumns, sampleRows);
            for (int index = 0; index < resolvedProjectionSamples.Count; index++)
            {
                GraffitiSurfaceHitSample sample = resolvedProjectionSamples[index];
                Color pixelColor = SampleTagColor(sample.Uv);
                if (pixelColor.a <= sprayAlphaThreshold)
                {
                    continue;
                }

                if (!GraffitiPaintSurface.TryGetOrCreate(sample.Renderer, sample.MaterialIndex, out GraffitiPaintSurface surface))
                {
                    continue;
                }

                TryAddResolvedSurface(surface);
                float sprayAlignment = Mathf.Abs(Vector3.Dot(sample.Normal.normalized, -sample.RayDirection.normalized));
                float compensation = 1f / Mathf.Max(0.35f, sprayAlignment);
                float resolvedRadius = Mathf.Min(baseRadius * compensation, baseRadius * 2.5f);
                resolvedStampHits.Add(new SprayStampHit
                {
                    Surface = surface,
                    Point = sample.Point,
                    Normal = sample.Normal,
                    Color = new Color(pixelColor.r, pixelColor.g, pixelColor.b, 1f),
                    Opacity = pixelColor.a,
                    Radius = resolvedRadius,
                    RevealValue = SampleRevealValue(sample.Uv)
                });
            }

            return resolvedStampHits.Count > 0;
        }

        private bool TryAddResolvedSurface(GraffitiPaintSurface surface)
        {
            if (surface == null)
            {
                return false;
            }

            for (int index = 0; index < resolvedSurfaces.Count; index++)
            {
                if (resolvedSurfaces[index] == surface)
                {
                    return true;
                }
            }

            resolvedSurfaces.Add(surface);
            return true;
        }

        private void ApplyProgress(TargetState target, PlaybackState playback, float progress)
        {
            int nextStep = (Lifecycle)playback.Lifecycle == Lifecycle.Completed
                ? revealSteps
                : Mathf.Clamp(Mathf.RoundToInt(progress * revealSteps), 0, revealSteps);
            if (lastAppliedStep < 0)
            {
                lastAppliedStep = 0;
            }

            ApplyTransition(target, lastAppliedStep, nextStep);
            lastAppliedStep = nextStep;
            finalStampApplied = nextStep >= revealSteps;
        }

        private void ApplyTransition(TargetState target, int fromStep, int toStep)
        {
            if (resolvedSurfaces.Count == 0 || resolvedStampHits.Count == 0 || sprayBrush == null)
            {
                return;
            }

            fromStep = Mathf.Clamp(fromStep, 0, revealSteps);
            toStep = Mathf.Clamp(toStep, 0, revealSteps);
            if (fromStep == toStep)
            {
                return;
            }

            bool revealing = toStep > fromStep;
            float lowerThreshold = Mathf.Min(fromStep, toStep) / (float)revealSteps;
            float upperThreshold = Mathf.Max(fromStep, toStep) / (float)revealSteps;
            ConfigureSprayBrush();
            CwBlendMode originalBlendMode = sprayBrush.BlendMode;
            Color originalColor = sprayBrush.Color;
            float originalOpacity = sprayBrush.Opacity;
            Vector3 originalScale = sprayBrush.Scale;
            float originalRadius = sprayBrush.Radius;
            float originalHardness = sprayBrush.Hardness;

            sprayBrush.BlendMode = revealing
                ? CwBlendMode.AlphaBlend(Vector4.one)
                : CwBlendMode.ReplaceOriginal(Vector4.one);

            CwPaintableManager.GetOrCreateInstance();
            for (int index = 0; index < resolvedSurfaces.Count; index++)
            {
                CwPaintableTexture paintableTexture = resolvedSurfaces[index]?.PaintableTexture;
                if (paintableTexture == null)
                {
                    continue;
                }

                sprayBrush.TargetTexture = paintableTexture;
                bool paintedSurface = false;
                for (int sampleIndex = 0; sampleIndex < resolvedStampHits.Count; sampleIndex++)
                {
                    SprayStampHit stampHit = resolvedStampHits[sampleIndex];
                    if (stampHit.Surface != resolvedSurfaces[index])
                    {
                        continue;
                    }

                    if (stampHit.RevealValue <= lowerThreshold || stampHit.RevealValue > upperThreshold)
                    {
                        continue;
                    }

                    sprayBrush.Color = stampHit.Color;
                    sprayBrush.Opacity = stampHit.Opacity;
                    sprayBrush.Radius = stampHit.Radius;
                    sprayBrush.Scale = new Vector3(1f, 1f, Mathf.Clamp(sprayDepthFlattening, 0.05f, 0.5f));
                    sprayBrush.Hardness = hardness;
                    sprayBrush.HandleHitPoint(false, revealing ? 1 : 0, 1f, sampleIndex, stampHit.Point, BuildRotation(-stampHit.Normal, target.Up));
                    paintedSurface = true;
                }

                if (paintedSurface)
                {
                    paintableTexture.ExecuteCommands(true, true);
                }
            }

            sprayBrush.TargetTexture = null;
            sprayBrush.BlendMode = originalBlendMode;
            sprayBrush.Color = originalColor;
            sprayBrush.Opacity = originalOpacity;
            sprayBrush.Scale = originalScale;
            sprayBrush.Radius = originalRadius;
            sprayBrush.Hardness = originalHardness;
        }

        private bool EnsurePersistentStamp(TargetState target)
        {
            if (finalStampApplied)
            {
                return true;
            }

            finalStampApplied = ApplyFinalStamp(target);
            return finalStampApplied;
        }

        private bool ApplyFinalStamp(TargetState target)
        {
            if (sprayBrush == null || resolvedSurfaces.Count == 0 || resolvedStampHits.Count == 0 || tagTexture == null)
            {
                return false;
            }

            ConfigureSprayBrush();
            CwBlendMode originalBlendMode = sprayBrush.BlendMode;
            Color originalColor = sprayBrush.Color;
            float originalOpacity = sprayBrush.Opacity;
            Vector3 originalScale = sprayBrush.Scale;
            float originalRadius = sprayBrush.Radius;
            float originalHardness = sprayBrush.Hardness;

            CwPaintableManager.GetOrCreateInstance();

            bool stampedAnySurface = false;
            for (int index = 0; index < resolvedSurfaces.Count; index++)
            {
                CwPaintableTexture paintableTexture = resolvedSurfaces[index]?.PaintableTexture;
                if (paintableTexture == null)
                {
                    continue;
                }

                sprayBrush.TargetTexture = paintableTexture;
                bool stampedSurface = false;
                for (int sampleIndex = 0; sampleIndex < resolvedStampHits.Count; sampleIndex++)
                {
                    SprayStampHit stampHit = resolvedStampHits[sampleIndex];
                    if (stampHit.Surface != resolvedSurfaces[index])
                    {
                        continue;
                    }

                    sprayBrush.Color = stampHit.Color;
                    sprayBrush.Opacity = stampHit.Opacity;
                    sprayBrush.Radius = stampHit.Radius;
                    sprayBrush.Scale = new Vector3(1f, 1f, Mathf.Clamp(sprayDepthFlattening, 0.05f, 0.5f));
                    sprayBrush.Hardness = hardness;
                    sprayBrush.HandleHitPoint(false, 1, 1f, sampleIndex, stampHit.Point, BuildRotation(-stampHit.Normal, target.Up));
                    stampedSurface = true;
                }

                if (!stampedSurface)
                {
                    continue;
                }

                paintableTexture.ExecuteCommands(true, true);
                stampedAnySurface = true;
            }

            sprayBrush.TargetTexture = null;
            sprayBrush.BlendMode = originalBlendMode;
            sprayBrush.Color = originalColor;
            sprayBrush.Opacity = originalOpacity;
            sprayBrush.Scale = originalScale;
            sprayBrush.Radius = originalRadius;
            sprayBrush.Hardness = originalHardness;
            return stampedAnySurface;
        }

        private bool BuildMaskTextureRange(float lowerThreshold, float upperThreshold)
        {
            float min = Mathf.Clamp01(lowerThreshold);
            float max = Mathf.Clamp01(upperThreshold);
            bool hasPixels = false;

            for (int index = 0; index < sourcePixels.Length; index++)
            {
                Color32 source = sourcePixels[index];
                float revealValue = revealNoise[index];
                if (source.a <= 0 || revealValue <= min || revealValue > max)
                {
                    workingPixels[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                workingPixels[index] = source;
                hasPixels = true;
            }

            generatedMaskTexture.SetPixels32(workingPixels);
            generatedMaskTexture.Apply(false, false);
            return hasPixels;
        }

        private void BuildRevealNoise()
        {
            int width = Mathf.Max(1, tagTexture.width);
            int height = Mathf.Max(1, tagTexture.height);

            for (int index = 0; index < revealNoise.Length; index++)
            {
                int x = index % width;
                int y = index / width;
                float verticalBias = height <= 1 ? 0f : y / (float)(height - 1);
                revealNoise[index] = Mathf.Clamp01(Hash01(x, y) * (1f - noiseBias) + verticalBias * noiseBias);
            }
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                hash ^= hash >> 13;
                hash *= 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFF) / 16777215f;
            }
        }

        private int ResolveSampleRows()
        {
            int textureHeight = tagTexture != null ? tagTexture.height : spraySampleRows;
            return Mathf.Clamp(Mathf.Min(textureHeight, spraySampleRows), 1, 256);
        }

        private int ResolveSampleColumns()
        {
            int rows = ResolveSampleRows();
            float aspect = tagTexture != null && tagTexture.height > 0
                ? tagTexture.width / (float)tagTexture.height
                : 1f;
            int textureWidth = tagTexture != null ? tagTexture.width : Mathf.RoundToInt(rows * aspect);
            return Mathf.Clamp(Mathf.Min(textureWidth, Mathf.CeilToInt(rows * aspect)), 1, 256);
        }

        private float ResolveBaseSprayRadius(TargetState target, int sampleColumns, int sampleRows)
        {
            float aspect = tagTexture != null && tagTexture.height > 0
                ? tagTexture.width / (float)tagTexture.height
                : 1f;
            float resolvedTagHeight = ResolveTagHeight(target);
            float width = resolvedTagHeight * aspect;
            float height = resolvedTagHeight;
            float stepX = width / Mathf.Max(1, sampleColumns);
            float stepY = height / Mathf.Max(1, sampleRows);
            return Mathf.Max(0.01f, Mathf.Max(stepX, stepY) * sprayDotRadiusMultiplier);
        }

        private Color SampleTagColor(Vector2 uv)
        {
            if (sourcePixels == null || tagTexture == null || tagTexture.width <= 0 || tagTexture.height <= 0)
            {
                return Color.clear;
            }

            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * tagTexture.width), 0, tagTexture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * tagTexture.height), 0, tagTexture.height - 1);
            return sourcePixels[(y * tagTexture.width) + x];
        }

        private float SampleRevealValue(Vector2 uv)
        {
            if (revealNoise == null || tagTexture == null || tagTexture.width <= 0 || tagTexture.height <= 0)
            {
                return 1f;
            }

            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * tagTexture.width), 0, tagTexture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * tagTexture.height), 0, tagTexture.height - 1);
            return revealNoise[(y * tagTexture.width) + x];
        }

        private static Vector3 ResolveSprayOrigin(TargetState target)
        {
            if (target.SprayOrigin.sqrMagnitude > 0.0001f)
            {
                return target.SprayOrigin;
            }

            Vector3 fallbackDirection = target.Direction.sqrMagnitude > 0.0001f ? target.Direction.normalized : Vector3.forward;
            return target.Point - fallbackDirection * 2f;
        }

        private GraffitiProjectionVolume BuildProjectionVolume(TargetState target)
        {
            float aspect = tagTexture != null && tagTexture.height > 0
                ? tagTexture.width / (float)tagTexture.height
                : 1f;
            float resolvedTagHeight = ResolveTagHeight(target);
            Vector3 halfExtents = new(resolvedTagHeight * aspect * 0.5f, resolvedTagHeight * 0.5f, Mathf.Max(0.05f, projectionDepth) * 0.5f);
            return new GraffitiProjectionVolume(
                target.SceneName.ToString(),
                target.Point,
                target.Direction,
                target.Up,
                halfExtents,
                Vector3.Distance(ResolveSprayOrigin(target), target.Point),
                target.SurfacePoint.sqrMagnitude > 0.0001f ? target.SurfacePoint : target.Point,
                target.Point,
                halfExtents,
                target.RendererPath.ToString());
        }

        private static Quaternion BuildRotation(Vector3 direction, Vector3 up)
        {
            Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            Vector3 safeUp = Vector3.ProjectOnPlane(up, safeDirection);
            if (safeUp.sqrMagnitude <= 0.0001f)
            {
                safeUp = Vector3.ProjectOnPlane(Vector3.up, safeDirection);
            }

            if (safeUp.sqrMagnitude <= 0.0001f)
            {
                safeUp = Vector3.ProjectOnPlane(Vector3.right, safeDirection);
            }

            return Quaternion.LookRotation(-safeDirection, safeUp.normalized);
        }

        private float ResolveTagHeight(TargetState target)
        {
            return Mathf.Max(0.1f, target.Size > 0f ? target.Size : tagHeight);
        }

        private void ApplyTargetTransform(TargetState target)
        {
            if (target.Direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion rotation = BuildRotation(target.Direction, target.Up);
            Vector3 offset = target.Direction.normalized * previewSurfaceOffset;
            Vector3 anchorPoint = target.SurfacePoint.sqrMagnitude > 0.0001f ? target.SurfacePoint : target.Point;
            transform.SetPositionAndRotation(anchorPoint - offset, rotation);
        }

        private void EnsurePreviewMaterial()
        {
            if (previewRenderer == null)
            {
                return;
            }

            if (previewMaterialInstance == null)
            {
                Material source = previewRenderer.sharedMaterial;
                if (source == null)
                {
                    return;
                }

                previewMaterialInstance = new Material(source)
                {
                    name = $"{name}_PreviewMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
                previewRenderer.sharedMaterial = previewMaterialInstance;
            }

            if (previewMaterialInstance.HasProperty("_BaseMap"))
            {
                previewMaterialInstance.SetTexture("_BaseMap", tagTexture);
            }
        }

        private void UpdatePreviewVisual(TargetState target, PlaybackState playback)
        {
            if (previewRenderer == null)
            {
                return;
            }

            previewRenderer.enabled = false;
        }

        private void ConfigureImpactCloudParticles()
        {
            if (impactCloudParticles == null)
            {
                return;
            }

            if (impactCloudParticles.TryGetComponent(out ParticleSystemRenderer particleRenderer) && particleRenderer.sharedMaterial == null)
            {
                Material material = TaggingVfxMaterials.GetCloudMaterial();
                if (material != null)
                {
                    particleRenderer.sharedMaterial = material;
                }
            }

            ParticleSystem.MainModule main = impactCloudParticles.main;
            main.startColor = new Color(1f, 0.98f, 0.96f, 0.68f);
        }

        private void UpdateImpactCloud(bool shouldPlay)
        {
            if (impactCloudParticles == null)
            {
                return;
            }

            if (shouldPlay)
            {
                if (!impactCloudParticles.isPlaying)
                {
                    impactCloudParticles.Play();
                }
            }
            else if (impactCloudParticles.isPlaying)
            {
                impactCloudParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private double GetCurrentTime()
        {
            if (NetworkManager != null && NetworkManager.IsListening)
            {
                return NetworkManager.ServerTime.Time;
            }

            return Time.timeAsDouble;
        }

        private static float EvaluateProgress(PlaybackState playback, double currentTime)
        {
            float duration = Mathf.Max(0.0001f, playback.Duration);
            float blend = playback.Duration <= Mathf.Epsilon
                ? 1f
                : Mathf.Clamp01((float)((currentTime - playback.StartTime) / duration));
            return Mathf.Lerp(playback.StartProgress, playback.TargetProgress, blend);
        }
    }
}
