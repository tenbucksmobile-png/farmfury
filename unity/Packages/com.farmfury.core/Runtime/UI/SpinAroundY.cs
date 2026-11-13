using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>Spins a UI element slowly around its own vertical axis, like a coin turning (unscaled time).</summary>
    public class SpinAroundY : MonoBehaviour
    {
        [Tooltip("Seconds per full turn.")]
        public float secondsPerTurn = 2.5f;

        private void Update()
        {
            float angle = Time.unscaledTime * 360f / secondsPerTurn;
            transform.localRotation = Quaternion.Euler(0f, angle % 360f, 0f);
        }
    }
}
