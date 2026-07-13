using UnityEngine;

/// <summary>
/// Minimal billboard: keeps this object facing the main camera every frame. Used by world-space
/// UI panels (e.g. BreathCircleUI) that need to stay legible as the player moves in VR. This is a
/// deliberately small stand-in for the old ComfortBillboard, which is not present in this
/// architecture.
/// </summary>
public class FaceCamera : MonoBehaviour
{
    [Tooltip("Camera to face. Falls back to Camera.main if left unassigned.")]
    [SerializeField] Camera targetCamera;

    void OnEnable()
    {
        if (!targetCamera) targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (!targetCamera)
        {
            targetCamera = Camera.main;
            if (!targetCamera) return;
        }

        Vector3 toCamera = transform.position - targetCamera.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }
}
