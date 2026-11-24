using UnityEngine;

namespace Poeirinha
{
    /// <summary>Câmera do menu principal: balança devagar de um lado para o outro em volta do cenário.</summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        [SerializeField] Transform pivot;
        [SerializeField] float distance = 1.4f;
        [SerializeField] float height = 0.6f;
        [SerializeField] float centerAngle = -35f;
        [SerializeField] float swingDegrees = 18f;
        [SerializeField] float swingSpeed = 0.15f;

        void LateUpdate()
        {
            if (pivot == null) return;
            float angle = centerAngle + Mathf.Sin(Time.unscaledTime * swingSpeed * Mathf.PI * 2f) * swingDegrees;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height, -distance);
            transform.position = pivot.position + offset;
            transform.LookAt(pivot.position + Vector3.up * 0.12f);
        }
    }
}
