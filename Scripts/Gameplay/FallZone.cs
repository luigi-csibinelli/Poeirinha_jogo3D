using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Trigger logo abaixo da borda da escada. Se o robô entra aqui, ele já caiu:
    /// derrota "Caiu da escada!".
    /// </summary>
    public class FallZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (!RobotDetector.TryGetRobot(other, out _)) return;
            if (GameManager.Instance != null) GameManager.Instance.Lose(DefeatReason.FellDownStairs);
        }
    }
}
