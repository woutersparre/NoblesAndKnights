using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public static CameraController instance;
    public float smoothSpeed = 3f; // Speed of the move camera movement
    public Vector3 offset = new Vector3(0, 5, -10); // Default offset from the target (Z offset is generally fixed in 2D)

    public Coroutine currentCoroutine = null; // Reference to the current coroutine
    public bool isFocusing = false; // Tracks whether a focus coroutine is active

    void Awake()
    {
        if (instance != null && instance != this) 
        { 
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void FocusCameraOn(Vector3 target)
    {
        if (isFocusing)
        {
            // If a focus is already in progress, stop the current coroutine
            StopCoroutine(currentCoroutine);
            isFocusing = false;
        }

        // Start the new focus coroutine
        currentCoroutine = StartCoroutine(SmoothFocus(target));
    }
    public void FocusCameraOnFast(Vector3 target)
    {
        transform.position = target + offset;
    }

    private IEnumerator SmoothFocus(Vector3 target)
    {
        isFocusing = true; // Mark the coroutine as active

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main camera not found!");
            isFocusing = false;
            yield break;
        }

        // Adjust target position (keeping Z fixed for 2D)
        Vector3 targetPosition = target + offset;

        // Check if the camera has reached the target position including the offset
        while (Vector3.Distance(mainCamera.transform.position, targetPosition) > 0.1f)
        {
            // Smoothly move the camera towards the target position (using Lerp directly)
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                targetPosition,
                smoothSpeed * Time.deltaTime
            );

            // Wait for the next frame
            yield return null;
        }

        // Ensure the camera is positioned exactly at the target position (to avoid floating-point inaccuracies)
        mainCamera.transform.position = targetPosition;

        // Mark the coroutine as complete
        isFocusing = false;
    }

    public void FocusCameraOnMainTitle(Character c)
    {
        if (c == null) return;
        FocusCameraOn(c.WorldPosition);
    }
}
