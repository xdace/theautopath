using UnityEngine;

namespace Betaknight.Overworld.Controllers
{
    /// <summary>Weiche 2D-Kameraverfolgung. Läuft in LateUpdate, damit die Figur bereits bewegt wurde.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float followSpeed = 6f;

        public void Configure(Transform followTarget, float speed)
        {
            target = followTarget;
            followSpeed = speed;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            Vector3 p = target.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 current = transform.position;
            Vector3 goal = new Vector3(target.position.x, target.position.y, current.z);
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(current, goal, t);
        }
    }
}
