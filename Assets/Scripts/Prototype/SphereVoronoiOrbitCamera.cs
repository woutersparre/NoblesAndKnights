using UnityEngine;
using UnityEngine.InputSystem;

namespace SphereVoronoiPrototype
{
    public class SphereVoronoiOrbitCamera : MonoBehaviour
    {
        [Header("Orbit Settings")]
        public Transform target;
        public float distance = 24f;
        public float minDistance = 1.1f;
        public float maxDistance = 60f;
        public float rotationSpeed = 200f;
        public float zoomSpeed = 2f;
        
        private float x = 0f;
        private float y = 0f;
        private SphereVoronoiGenerator generator;
        
        void Start()
        {
            if (target == null)
            {
                generator = FindFirstObjectByType<SphereVoronoiGenerator>();
                if (generator != null)
                {
                    target = generator.transform;
                }
                else
                {
                    GameObject go = new GameObject("SphereCenter");
                    go.transform.position = Vector3.zero;
                    target = go.transform;
                }
            }
            else
            {
                generator = target.GetComponent<SphereVoronoiGenerator>();
            }

            if (generator != null)
            {
                minDistance = 1.1f;
                maxDistance = Mathf.Max(maxDistance, 6f);
                distance = Mathf.Max(distance, 2.2f);
            }
            
            Vector3 angles = transform.eulerAngles;
            x = angles.y;
            y = angles.x;
        }
        
        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            
            // Middle-drag orbits; the left button is reserved for selecting cells.
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                x += delta.x * rotationSpeed * Time.deltaTime;
                y -= delta.y * rotationSpeed * Time.deltaTime;
                y = ClampAngle(y, -90f, 90f);
            }
            
            float scroll = mouse.scroll.ReadValue().y;
            float zoomScale = Mathf.Max(1f, distance / 24f);
            distance -= scroll * zoomSpeed * 0.01f * zoomScale;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            
            Quaternion rotation = Quaternion.Euler(y, x, 0);
            Vector3 position = rotation * new Vector3(0f, 0f, -distance) + target.position;
            transform.rotation = rotation;
            transform.position = position;
        }
        
        float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }
    }
}
