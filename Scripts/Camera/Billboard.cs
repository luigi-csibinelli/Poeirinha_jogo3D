using UnityEngine;

namespace Poeirinha
{
    /// <summary>Faz o objeto (ex.: o "zZ" do gato dormindo) sempre olhar para a câmera.</summary>
    public class Billboard : MonoBehaviour
    {
        Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
