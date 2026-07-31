using UnityEngine;

// Sun sweeps back and forth along a low dome-shaped arc across the sky —
// "по дуге по всему небу туда и обратно" — instead of sitting at one fixed
// spot. Also keeps facing the camera: once it travels well off to either
// side, a flat, non-billboarded quad looks visibly skewed/stretched near the
// edge of a wide-FOV frustum.
public class SunArc : MonoBehaviour
{
    [SerializeField] private Vector3 center = new Vector3(0f, 5f, 120f);
    [SerializeField] private float radiusX = 100f;
    [SerializeField] private float radiusY = 44f;
    [SerializeField] private float cycleDuration = 45f; // seconds for one one-way sweep

    private void Update()
    {
        float t = Mathf.PingPong(Time.time / cycleDuration, 1f);
        float angle = Mathf.Lerp(0f, Mathf.PI, t);
        transform.position = center + new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}
