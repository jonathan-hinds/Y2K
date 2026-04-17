using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Race.Player;
using Race.Tagging;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Race.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerSessionController : MonoBehaviour
    {
        private const int MaxPlayers = 4;
        private const int RelayConnections = MaxPlayers - 1;
        private const string RelayConnectionType = "dtls";

        [Header("Scene References")]
        [SerializeField] private GameObject offlinePlayerRoot;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private PlayerCinemachineCameraRig cameraRig;
        [SerializeField] private MultiplayerMenuPresenter menuPresenter;

        [Header("Spawn")]
        [SerializeField] private float spawnRadius = 7.5f;
        [SerializeField] private Vector3[] spawnOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 4f),
            new Vector3(-4f, 0f, 4f),
            new Vector3(4f, 0f, -4f)
        };

        [Header("Networking")]
        [SerializeField] private ushort directConnectPort = 7777;
        [SerializeField] private int tickRate = 60;

        private readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new();
        private Vector3 offlineSpawnPosition;
        private Quaternion offlineSpawnRotation;
        private NetworkManager runtimeNetworkManager;
        private UnityTransport unityTransport;
        private NetworkPlayerAvatar localNetworkPlayer;
        private bool servicesInitialized;
        private bool authenticationInitialized;
        private bool sessionInProgress;
        private string activeJoinCode;

        public bool IsSessionActive => runtimeNetworkManager != null && runtimeNetworkManager.IsListening;
        public bool IsHost => runtimeNetworkManager != null && runtimeNetworkManager.IsHost;
        public string ActiveJoinCode => activeJoinCode;
        public NetworkPlayerAvatar LocalNetworkPlayer => localNetworkPlayer;

        public event Action<string> StatusChanged;
        public event Action<bool> SessionStateChanged;

        private void Reset()
        {
            offlinePlayerRoot = GameObject.Find("Player");
            cameraRig = FindFirstObjectByType<PlayerCinemachineCameraRig>();
            menuPresenter = FindFirstObjectByType<MultiplayerMenuPresenter>();
        }

        private void Awake()
        {
            if (offlinePlayerRoot == null)
            {
                PlayerMotor offlineMotor = FindFirstObjectByType<PlayerMotor>();
                offlinePlayerRoot = offlineMotor != null ? offlineMotor.gameObject : null;
            }

            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<PlayerCinemachineCameraRig>();
            }

            if (menuPresenter == null)
            {
                menuPresenter = FindFirstObjectByType<MultiplayerMenuPresenter>();
            }

            if (offlinePlayerRoot != null)
            {
                offlineSpawnPosition = offlinePlayerRoot.transform.position;
                offlineSpawnRotation = offlinePlayerRoot.transform.rotation;
            }

            if (menuPresenter != null)
            {
                menuPresenter.Bind(this);
            }
        }

        private void OnDestroy()
        {
            TeardownNetworkManager();
        }

        public async Task HostSessionAsync()
        {
            if (sessionInProgress || IsSessionActive)
            {
                return;
            }

            sessionInProgress = true;
            SetStatus("Initializing services...");

            try
            {
                await EnsureServicesAsync();
                PrepareForOnlineSession();
                EnsureNetworkManager();

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(RelayConnections);
                activeJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                RelayServerData relayServerData = allocation.ToRelayServerData(RelayConnectionType);
                unityTransport.SetRelayServerData(relayServerData);

                if (!runtimeNetworkManager.StartHost())
                {
                    throw new InvalidOperationException("Failed to start host session.");
                }

                GUIUtility.systemCopyBuffer = activeJoinCode;
                SetStatus($"Hosting session. Join code copied: {activeJoinCode}");
                SessionStateChanged?.Invoke(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                RestoreOfflinePlayer();
                SetStatus($"Host failed: {exception.Message}");
                throw;
            }
            finally
            {
                sessionInProgress = false;
            }
        }

        public async Task JoinSessionAsync(string joinCode)
        {
            if (sessionInProgress || IsSessionActive)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new ArgumentException("Join code is required.", nameof(joinCode));
            }

            sessionInProgress = true;
            SetStatus("Joining session...");

            try
            {
                await EnsureServicesAsync();
                PrepareForOnlineSession();
                EnsureNetworkManager();

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());
                RelayServerData relayServerData = allocation.ToRelayServerData(RelayConnectionType);
                unityTransport.SetRelayServerData(relayServerData);

                if (!runtimeNetworkManager.StartClient())
                {
                    throw new InvalidOperationException("Failed to start client session.");
                }

                activeJoinCode = joinCode.Trim().ToUpperInvariant();
                SetStatus($"Joining with code {activeJoinCode}...");
                SessionStateChanged?.Invoke(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                RestoreOfflinePlayer();
                SetStatus($"Join failed: {exception.Message}");
                throw;
            }
            finally
            {
                sessionInProgress = false;
            }
        }

        public void LeaveSession()
        {
            if (!IsSessionActive && !sessionInProgress)
            {
                RestoreOfflinePlayer();
                return;
            }

            if (runtimeNetworkManager != null)
            {
                runtimeNetworkManager.Shutdown();
            }

            TeardownNetworkManager();
            RestoreOfflinePlayer();
            SetStatus("Returned to offline play.");
            SessionStateChanged?.Invoke(false);
        }

        public void RegisterLocalNetworkPlayer(NetworkPlayerAvatar avatar)
        {
            localNetworkPlayer = avatar;
            ApplyGameplayInputBlocked(menuPresenter != null && menuPresenter.IsVisible);
            RetargetCamera(avatar);
        }

        public void UnregisterLocalNetworkPlayer(NetworkPlayerAvatar avatar)
        {
            if (localNetworkPlayer == avatar)
            {
                localNetworkPlayer = null;
            }
        }

        public void SetGameplayMenuVisible(bool visible)
        {
            ApplyGameplayInputBlocked(visible);
        }

        private async Task EnsureServicesAsync()
        {
            if (!servicesInitialized)
            {
                await UnityServices.InitializeAsync();
                servicesInitialized = true;
            }

            if (!authenticationInitialized)
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                authenticationInitialized = true;
            }
        }

        private void PrepareForOnlineSession()
        {
            if (offlinePlayerRoot != null)
            {
                offlinePlayerRoot.SetActive(false);
            }
        }

        private void RestoreOfflinePlayer()
        {
            localNetworkPlayer = null;
            activeJoinCode = string.Empty;
            sessionInProgress = false;

            if (offlinePlayerRoot != null)
            {
                offlinePlayerRoot.transform.SetPositionAndRotation(offlineSpawnPosition, offlineSpawnRotation);
                offlinePlayerRoot.SetActive(true);

                PlayerInputReader inputReader = offlinePlayerRoot.GetComponent<PlayerInputReader>();
                if (inputReader != null)
                {
                    inputReader.InputBlocked = menuPresenter != null && menuPresenter.IsVisible;
                }

                if (cameraRig != null)
                {
                    cameraRig.SetTarget(
                        offlinePlayerRoot.GetComponent<PlayerRig>(),
                        offlinePlayerRoot.GetComponent<PlayerInputReader>(),
                        offlinePlayerRoot.GetComponent<PlayerMotor>());
                }
            }
        }

        private void EnsureNetworkManager()
        {
            if (runtimeNetworkManager != null)
            {
                return;
            }

            GameObject networkRoot = new GameObject("RuntimeNetworkManager");
            runtimeNetworkManager = networkRoot.AddComponent<NetworkManager>();
            unityTransport = networkRoot.AddComponent<UnityTransport>();

            runtimeNetworkManager.NetworkConfig = new NetworkConfig
            {
                TickRate = (uint)Mathf.Max(1, tickRate),
                EnableSceneManagement = false,
                AutoSpawnPlayerPrefabClientSide = false,
                ConnectionApproval = false,
                PlayerPrefab = null,
                NetworkTransport = unityTransport
            };

            unityTransport.SetConnectionData("127.0.0.1", directConnectPort, "0.0.0.0");
            runtimeNetworkManager.AddNetworkPrefab(playerPrefab);

            GameObject graffitiTagPrefab = Resources.Load<GameObject>(GraffitiTagInstance.ResourcePath);
            if (graffitiTagPrefab != null)
            {
                runtimeNetworkManager.AddNetworkPrefab(graffitiTagPrefab);
            }
            else
            {
                Debug.LogWarning($"Unable to load graffiti tag prefab from Resources at '{GraffitiTagInstance.ResourcePath}'.", this);
            }

            runtimeNetworkManager.OnClientConnectedCallback += HandleClientConnected;
            runtimeNetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            runtimeNetworkManager.OnTransportFailure += HandleTransportFailure;
            runtimeNetworkManager.OnServerStopped += HandleServerStopped;
            runtimeNetworkManager.OnClientStopped += HandleClientStopped;
        }

        private void TeardownNetworkManager()
        {
            if (runtimeNetworkManager == null)
            {
                return;
            }

            runtimeNetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            runtimeNetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            runtimeNetworkManager.OnTransportFailure -= HandleTransportFailure;
            runtimeNetworkManager.OnServerStopped -= HandleServerStopped;
            runtimeNetworkManager.OnClientStopped -= HandleClientStopped;

            Destroy(runtimeNetworkManager.gameObject);
            runtimeNetworkManager = null;
            unityTransport = null;
            spawnedPlayers.Clear();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!runtimeNetworkManager.IsServer)
            {
                if (clientId == runtimeNetworkManager.LocalClientId)
                {
                    SetStatus("Connected to session.");
                }

                return;
            }

            if (spawnedPlayers.ContainsKey(clientId))
            {
                return;
            }

            (Vector3 spawnPosition, Quaternion spawnRotation) = GetSpawnPose(spawnedPlayers.Count);
            GameObject instance = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            NetworkPlayerAvatar avatar = instance.GetComponent<NetworkPlayerAvatar>();
            avatar?.SetServerSpawnPose(spawnPosition, spawnRotation);
            networkObject.SpawnAsPlayerObject(clientId, true);
            spawnedPlayers[clientId] = networkObject;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (spawnedPlayers.TryGetValue(clientId, out NetworkObject networkObject))
            {
                spawnedPlayers.Remove(clientId);

                if (networkObject != null && networkObject.IsSpawned && runtimeNetworkManager != null && runtimeNetworkManager.IsServer)
                {
                    networkObject.Despawn(true);
                }
            }

            if (runtimeNetworkManager != null && clientId == runtimeNetworkManager.LocalClientId)
            {
                LeaveSession();
            }
        }

        private void HandleTransportFailure()
        {
            LeaveSession();
            SetStatus("Transport connection failed.");
        }

        private void HandleServerStopped(bool _)
        {
            LeaveSession();
        }

        private void HandleClientStopped(bool _)
        {
            if (!IsHost)
            {
                LeaveSession();
            }
        }

        private void ApplyGameplayInputBlocked(bool blocked)
        {
            if (localNetworkPlayer != null)
            {
                localNetworkPlayer.SetGameplayInputBlocked(blocked);
                return;
            }

            if (offlinePlayerRoot == null)
            {
                return;
            }

            PlayerInputReader inputReader = offlinePlayerRoot.GetComponent<PlayerInputReader>();
            if (inputReader != null)
            {
                inputReader.InputBlocked = blocked;
            }
        }

        private void RetargetCamera(NetworkPlayerAvatar avatar)
        {
            if (cameraRig == null || avatar == null)
            {
                return;
            }

            cameraRig.SetTarget(avatar.PlayerRig, avatar.InputReader, avatar.PlayerMotor);
        }

        private (Vector3 position, Quaternion rotation) GetSpawnPose(int index)
        {
            MultiplayerSpawnPoint[] spawnPoints = FindObjectsByType<MultiplayerSpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(point => point.SpawnIndex)
                .ThenBy(point => point.name, StringComparer.Ordinal)
                .ToArray();

            if (spawnPoints.Length > 0)
            {
                MultiplayerSpawnPoint spawnPoint = spawnPoints[index % spawnPoints.Length];
                return (spawnPoint.transform.position, spawnPoint.transform.rotation);
            }

            Vector3 basePosition = offlineSpawnPosition;
            if (spawnOffsets != null && spawnOffsets.Length > 0)
            {
                return (basePosition + spawnOffsets[index % spawnOffsets.Length], offlineSpawnRotation);
            }

            float angle = index / (float)MaxPlayers * Mathf.PI * 2f;
            Vector3 position = basePosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
            return (position, offlineSpawnRotation);
        }

        private void SetStatus(string message)
        {
            StatusChanged?.Invoke(message);
        }
    }
}
