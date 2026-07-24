using UnityEngine;

public class MovingEntity : MonoBehaviour
{
    [SerializeField] private float destroyZ = -10f;

    private void Update()
    {
        float speed = SpeedController.Instance != null ? SpeedController.Instance.CurrentSpeed : 10f;
        transform.position += Vector3.back * speed * Time.deltaTime;

        if (transform.position.z < destroyZ)
            Destroy(gameObject);
    }
}
