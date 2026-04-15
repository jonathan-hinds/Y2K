using System;
using Race.Player;
using Unity.Netcode;
using UnityEngine;

namespace Race.Scoring
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerTrickScoreController : NetworkBehaviour
    {
        public const string GrindingTrickId = "grind";

        public readonly struct ScorePopupRequest
        {
            public ScorePopupRequest(int amount, Vector3 worldPosition)
            {
                Amount = amount;
                WorldPosition = worldPosition;
            }

            public int Amount { get; }
            public Vector3 WorldPosition { get; }
        }

        private struct ScorePopupEventState : INetworkSerializable, IEquatable<ScorePopupEventState>
        {
            public ushort Sequence;
            public int Amount;
            public Vector3 WorldPosition;

            public bool Equals(ScorePopupEventState other)
            {
                return Sequence == other.Sequence
                    && Amount == other.Amount
                    && WorldPosition.Equals(other.WorldPosition);
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Sequence);
                serializer.SerializeValue(ref Amount);
                serializer.SerializeValue(ref WorldPosition);
            }
        }

        [Header("References")]
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private Transform popupOrigin;
        [SerializeField] private TrickScoringSettings scoringSettings;

        [Header("Trick Bindings")]
        [SerializeField] private string grindingTrickId = GrindingTrickId;

        private readonly NetworkVariable<int> totalScore = new NetworkVariable<int>(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ScorePopupEventState> lastPopupEvent = new NetworkVariable<ScorePopupEventState>(writePerm: NetworkVariableWritePermission.Owner);

        private float grindingAwardTimer;
        private int lastPublishedScore = int.MinValue;

        public PlayerMotor PlayerMotor => playerMotor;
        public int CurrentScore => totalScore.Value;

        public event Action<int> ScoreChanged;
        public event Action<ScorePopupRequest> LocalPopupRequested;
        public event Action<ScorePopupRequest> RemotePopupRequested;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            PublishScoreIfChanged(force: true);
        }

        public override void OnNetworkSpawn()
        {
            totalScore.OnValueChanged += HandleScoreChanged;
            lastPopupEvent.OnValueChanged += HandlePopupEventChanged;
            PublishScoreIfChanged(force: true);
        }

        public override void OnNetworkDespawn()
        {
            totalScore.OnValueChanged -= HandleScoreChanged;
            lastPopupEvent.OnValueChanged -= HandlePopupEventChanged;
        }

        private void Update()
        {
            if (!ShouldSimulateLocally())
            {
                grindingAwardTimer = 0f;
                return;
            }

            UpdateGrindingScore();
        }

        public bool TryAwardTrick(string trickId)
        {
            if (!TryResolveDefinition(trickId, out TrickScoreDefinition definition))
            {
                return false;
            }

            AwardScore(definition.PointsPerAward, ResolvePopupWorldPosition(definition.PopupLocalOffset));
            return true;
        }

        public bool TryAwardTrick(string trickId, Vector3 worldPosition)
        {
            if (!TryResolveDefinition(trickId, out TrickScoreDefinition definition))
            {
                return false;
            }

            AwardScore(definition.PointsPerAward, worldPosition);
            return true;
        }

        private void ResolveReferences()
        {
            if (playerMotor == null)
            {
                playerMotor = GetComponent<PlayerMotor>();
            }

            if (popupOrigin == null)
            {
                PlayerRig rig = GetComponent<PlayerRig>();
                popupOrigin = rig != null && rig.CameraTarget != null ? rig.CameraTarget : transform;
            }
        }

        private bool ShouldSimulateLocally()
        {
            if (IsSpawned)
            {
                return IsOwner;
            }

            return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        }

        private void UpdateGrindingScore()
        {
            if (playerMotor == null || !playerMotor.IsGrinding)
            {
                grindingAwardTimer = 0f;
                return;
            }

            if (!TryResolveDefinition(grindingTrickId, out TrickScoreDefinition definition))
            {
                return;
            }

            grindingAwardTimer += Time.deltaTime;
            float interval = definition.AwardIntervalSeconds;
            while (grindingAwardTimer >= interval)
            {
                grindingAwardTimer -= interval;
                AwardScore(definition.PointsPerAward, ResolvePopupWorldPosition(definition.PopupLocalOffset));
            }
        }

        private bool TryResolveDefinition(string trickId, out TrickScoreDefinition definition)
        {
            if (scoringSettings != null && scoringSettings.TryGetDefinition(trickId, out definition))
            {
                return true;
            }

            definition = default;
            return false;
        }

        private Vector3 ResolvePopupWorldPosition(Vector3 popupLocalOffset)
        {
            Transform origin = popupOrigin != null ? popupOrigin : transform;
            return origin.TransformPoint(popupLocalOffset);
        }

        private void AwardScore(int amount, Vector3 worldPosition)
        {
            if (amount <= 0)
            {
                return;
            }

            totalScore.Value += amount;
            PublishScoreIfChanged(force: false);

            var popupRequest = new ScorePopupRequest(amount, worldPosition);
            LocalPopupRequested?.Invoke(popupRequest);

            if (IsSpawned && IsOwner)
            {
                lastPopupEvent.Value = new ScorePopupEventState
                {
                    Sequence = (ushort)(lastPopupEvent.Value.Sequence + 1),
                    Amount = amount,
                    WorldPosition = worldPosition
                };
            }
        }

        private void HandleScoreChanged(int previousValue, int newValue)
        {
            PublishScoreIfChanged(force: false);
        }

        private void HandlePopupEventChanged(ScorePopupEventState previousValue, ScorePopupEventState newValue)
        {
            if (previousValue.Sequence == newValue.Sequence || ShouldSimulateLocally())
            {
                return;
            }

            RemotePopupRequested?.Invoke(new ScorePopupRequest(newValue.Amount, newValue.WorldPosition));
        }

        private void PublishScoreIfChanged(bool force)
        {
            int score = totalScore.Value;
            if (!force && score == lastPublishedScore)
            {
                return;
            }

            lastPublishedScore = score;
            ScoreChanged?.Invoke(score);
        }
    }
}
