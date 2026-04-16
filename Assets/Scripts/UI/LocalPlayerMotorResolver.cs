using Race.Multiplayer;
using Race.Player;
using Unity.Netcode;
using UnityEngine;

namespace Race.UI
{
    internal static class LocalPlayerMotorResolver
    {
        public static PlayerMotor FindLocalPlayerMotor()
        {
            MultiplayerSessionController sessionController = Object.FindFirstObjectByType<MultiplayerSessionController>();
            if (sessionController != null
                && sessionController.LocalNetworkPlayer != null
                && IsLocalPlayerMotorCandidate(sessionController.LocalNetworkPlayer.PlayerMotor))
            {
                return sessionController.LocalNetworkPlayer.PlayerMotor;
            }

            PlayerMotor[] motors = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
            for (int index = 0; index < motors.Length; index++)
            {
                PlayerMotor candidate = motors[index];
                if (IsLocalPlayerMotorCandidate(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static bool IsLocalPlayerMotorCandidate(PlayerMotor candidate)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.gameObject.activeInHierarchy)
            {
                return false;
            }

            NetworkPlayerAvatar avatar = candidate.GetComponent<NetworkPlayerAvatar>();
            if (avatar != null)
            {
                if (avatar.IsSpawned)
                {
                    return avatar.IsOwner;
                }

                return IsOfflineLocalCandidate(candidate);
            }

            NetworkObject networkObject = candidate.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                return networkObject.IsOwner;
            }

            return IsOfflineLocalCandidate(candidate);
        }

        private static bool IsOfflineLocalCandidate(PlayerMotor candidate)
        {
            return candidate != null
                && candidate.enabled
                && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);
        }
    }
}
