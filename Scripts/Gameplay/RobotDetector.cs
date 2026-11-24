using UnityEngine;

namespace Poeirinha
{
    /// <summary>Utilitário: descobre se um collider pertence ao robô.</summary>
    public static class RobotDetector
    {
        /// <summary>
        /// O collider que encosta num trigger é um filho do robô (o "casco"),
        /// então olhamos o Rigidbody ao qual ele está preso.
        /// </summary>
        public static bool TryGetRobot(Collider other, out RobotController robot)
        {
            robot = null;
            Rigidbody body = other.attachedRigidbody;
            return body != null && body.TryGetComponent(out robot);
        }
    }
}
