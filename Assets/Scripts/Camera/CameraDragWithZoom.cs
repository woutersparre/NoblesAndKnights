using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CameraDragWithZoom : MonoBehaviour
{
    private Vector3 dragOrigin;
    private bool isDragging;

    [Header("Zoom Settings")]
    public float zoomSpeed = 30.0f;
    public float minZoom = 1.0f;
    public float maxZoom = 30.0f;

    [Header("Input Actions")]
    public InputActionReference middleMouseAction;
    public InputActionReference scrollAction;

    void Update()
    {
        HandleDrag();
        HandleZoom();
    }

    private void HandleDrag()
    {
        if (middleMouseAction.action.WasPressedThisFrame())
        {
            dragOrigin = Camera.main.ScreenToWorldPoint(
                Mouse.current.position.ReadValue()
            );

            isDragging = true;

            if (CameraController.instance.isFocusing)
            {
                CameraController.instance.StopCoroutine(
                    CameraController.instance.currentCoroutine
                );
                CameraController.instance.isFocusing = false;
            }
        }

        if (middleMouseAction.action.WasReleasedThisFrame())
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 currentMousePosition =
                Camera.main.ScreenToWorldPoint(
                    Mouse.current.position.ReadValue()
                );

            Vector3 difference = dragOrigin - currentMousePosition;
            Camera.main.transform.position += difference;
        }
    }

    private void HandleZoom()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        float scroll = scrollAction.action.ReadValue<float>();

        if (Mathf.Abs(scroll) > 0.01f)
        {
            Camera.main.orthographicSize += scroll * zoomSpeed;
            Camera.main.orthographicSize = Mathf.Clamp(
                Camera.main.orthographicSize,
                minZoom,
                maxZoom
            );
        }
    }
}
