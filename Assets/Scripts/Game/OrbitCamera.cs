using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Free 3D orbit camera. Right mouse drag to orbit, middle drag or WASD to pan,
    /// scroll to zoom from a whole-tank view down to a single frog.
    /// </summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Vector3 target = Vector3.zero;
        public float distance = 18f;
        public float minDistance = 0.5f;
        public float maxDistance = 60f;
        public float yaw = 0f;
        public float pitch = 45f;
        public float orbitSpeed = 4f;
        public float panSpeed = 1f;
        public float zoomSpeed = 0.15f;

        /// <summary>Set by the frog inspector's Follow button: the camera keeps this point centred.</summary>
        public bool following;
        public Vector3 followPoint;

        /// <summary>Moves the camera to look at a point, zooming in if it is far out.</summary>
        public void Focus(Vector3 point)
        {
            target = point;
            distance = Mathf.Min(distance, 7f);
        }

        private void LateUpdate()
        {
            if (following) target = Vector3.Lerp(target, followPoint, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * orbitSpeed;
                pitch -= Input.GetAxis("Mouse Y") * orbitSpeed;
            }
            pitch = Mathf.Clamp(pitch, 5f, 89f);

            var flatRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var flatForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float panScale = panSpeed * distance * 0.05f;
            if (Input.GetMouseButton(2))
                target -= (flatRight * Input.GetAxis("Mouse X") + flatForward * Input.GetAxis("Mouse Y")) * panScale;
            var keys = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (Input.GetMouseButton(2) || keys.sqrMagnitude > 0f) following = false; // panning takes over
            target += (flatRight * keys.x + flatForward * keys.z) * panScale * 10f * Time.unscaledDeltaTime;

            float scroll = Input.mouseScrollDelta.y;
            distance = Mathf.Clamp(distance * (1f - scroll * zoomSpeed), minDistance, maxDistance);
#endif
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }
    }
}
