using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Meia no chão (extra). O robô que passa por cima fica preso até apertar E várias vezes.
    /// Depois de solto, a meia ignora aquele robô até ele sair do trigger
    /// (senão ele ficaria preso de novo no mesmo instante).
    /// </summary>
    public class SockTrap : MonoBehaviour
    {
        [SerializeField] int pressesToFree = 5;
        [SerializeField] Transform visual;

        RobotController ignoredRobot;
        Vector3 visualRestPos;

        void Awake()
        {
            if (visual != null) visualRestPos = visual.localPosition;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!RobotDetector.TryGetRobot(other, out RobotController robot)) return;
            if (robot == ignoredRobot || !GameManager.Playing) return;
            robot.GetStuck(this, pressesToFree);
        }

        void OnTriggerExit(Collider other)
        {
            if (!RobotDetector.TryGetRobot(other, out RobotController robot)) return;
            if (robot == ignoredRobot) ignoredRobot = null;
        }

        /// <summary>Chamado pelo robô quando ele se solta.</summary>
        public void OnRobotFreed(RobotController robot)
        {
            ignoredRobot = robot;
            // A meia é "cuspida" um pouquinho para o lado.
            if (visual != null) visual.localPosition = visualRestPos + new Vector3(0.12f, 0f, 0.05f);
        }
    }
}
