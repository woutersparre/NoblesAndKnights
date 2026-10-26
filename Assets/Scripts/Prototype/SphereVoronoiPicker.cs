using UnityEngine;
using UnityEngine.InputSystem;

namespace SphereVoronoiPrototype
{
    public class SphereVoronoiPicker : MonoBehaviour
    {
        [Header("References")]
        public SphereVoronoiGenerator generator;
        public Camera mainCamera;
        
        [Header("Debug")]
        public int selectedCellId = -1;
        public bool drawSelection = true;
        

        void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (generator == null) generator = GetComponent<SphereVoronoiGenerator>();
        }
        
        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || mainCamera == null || generator == null) return;
            
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            if (!pressed) return;

            selectedCellId = -1;
            
            Vector2 pos = mouse.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(pos);
            if (!TryIntersectUnitSphere(ray, out Vector3 localDirection))
                return;

            if (generator.TryGetCellAtDirection(localDirection, out selectedCellId))
            {
                Debug.Log($"Picked cell {selectedCellId}, neighbors: " +
                    string.Join(",", generator.Cells[selectedCellId].Neighbors));
            }
        }

        public void ClearSelection()
        {
            selectedCellId = -1;
        }

        bool TryIntersectUnitSphere(Ray worldRay, out Vector3 localDirection)
        {
            localDirection = Vector3.zero;
            Vector3 origin = transform.InverseTransformPoint(worldRay.origin);
            Vector3 direction = transform.InverseTransformVector(worldRay.direction);
            float a = Vector3.Dot(direction, direction);
            if (a < 1e-12f)
                return false;

            float halfB = Vector3.Dot(origin, direction);
            float c = Vector3.Dot(origin, origin) - 1f;
            float discriminant = halfB * halfB - a * c;
            if (discriminant < 0f)
                return false;

            float root = Mathf.Sqrt(discriminant);
            float nearDistance = (-halfB - root) / a;
            float farDistance = (-halfB + root) / a;
            float distance = nearDistance >= 0f ? nearDistance : farDistance;
            if (distance < 0f)
                return false;

            Vector3 localHit = origin + distance * direction;
            if (localHit.sqrMagnitude < 1e-12f)
                return false;

            localDirection = localHit.normalized;
            return true;
        }
        
        void OnDrawGizmos()
        {
            if (!drawSelection || !Application.isPlaying) return;
            if (generator == null || generator.Cells == null) return;
            if (selectedCellId < 0 || selectedCellId >= generator.Cells.Count) return;
            
            var cell = generator.Cells[selectedCellId];
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = generator.transform.localToWorldMatrix;
            Gizmos.color = Color.magenta;
            foreach (Vector3[] polygon in generator.GetCellPolygons(selectedCellId))
            {
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector3 v1 = polygon[i];
                    Vector3 v2 = polygon[(i + 1) % polygon.Length];
                    Gizmos.DrawLine(v1, v2);
                }
            }
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(cell.Seed, 0.1f);
            Gizmos.matrix = previousMatrix;
        }
    }
}
