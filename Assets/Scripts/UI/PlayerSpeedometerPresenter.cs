using Race.Player;
using UnityEngine;

namespace Race.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpeedometerPresenter : MonoBehaviour
    {
        private const float UnitsToMilesPerHour = 2.23693629f;

        [Header("References")]
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private SpeedometerDisplay speedometerDisplay;

        [Header("Binding")]
        [SerializeField] private bool autoFindPlayerMotor = true;

        [Header("Scaling")]
        [SerializeField, Min(0f)] private float minimumGaugeSpeedMph = 0f;
        [SerializeField] private bool expandGaugeForOverflowSpeed = true;

        private void Awake()
        {
            if (speedometerDisplay == null)
            {
                speedometerDisplay = GetComponent<SpeedometerDisplay>();
            }

            TryBindPlayerMotor();
        }

        private void Update()
        {
            if (speedometerDisplay == null)
            {
                return;
            }

            if (playerMotor == null && autoFindPlayerMotor)
            {
                TryBindPlayerMotor();
            }

            float currentSpeedMph = playerMotor != null
                ? playerMotor.PlanarSpeed * UnitsToMilesPerHour
                : 0f;
            float configuredMaxSpeedMph = playerMotor != null
                ? Mathf.Max(1f, playerMotor.ConfiguredMaxPlanarSpeed * UnitsToMilesPerHour)
                : UnitsToMilesPerHour;
            float maxSpeedMph = Mathf.Max(configuredMaxSpeedMph, minimumGaugeSpeedMph);
            if (expandGaugeForOverflowSpeed)
            {
                maxSpeedMph = Mathf.Max(maxSpeedMph, currentSpeedMph);
            }

            speedometerDisplay.SetTargetSpeed(currentSpeedMph, maxSpeedMph);
        }

        public void Bind(PlayerMotor motor)
        {
            playerMotor = motor;
        }

        private void TryBindPlayerMotor()
        {
            if (playerMotor != null)
            {
                return;
            }

            playerMotor = FindFirstObjectByType<PlayerMotor>();
        }
    }
}
