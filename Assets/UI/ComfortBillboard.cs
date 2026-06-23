using UnityEngine;

/// <summary>
/// Comfort-first placement for the always-on world-space panels (rep icons, guide line):
/// the panel lazily follows where you're looking (flattened forward + offsets, SmoothDamp)
/// and billboards to face you with Slerp damping. This is deliberately NOT a rigid camera
/// child — head-locked UI in VR jitters with every micro head movement and induces nausea.
///
/// WorldPopup billboards itself and PopupSystem follows the gaze for transient cards; this is
/// the equivalent for panels that stay up the whole session.
/// </summary>
public class ComfortBillboard : MonoBehaviour
{
    [Tooltip("Metres in front of the eyes.")]
    [SerializeField] float distance = 1.8f;
    [Tooltip("Sideways offset in metres (+ = right).")]
    [SerializeField] float horizontal = 0f;
    [Tooltip("Vertical offset in metres (+ = up).")]
    [SerializeField] float vertical = -0.35f;
    [Tooltip("Bigger = follows your gaze more lazily / stably.")]
    [SerializeField] float followSmoothTime = 0.3f;
    [Tooltip("Higher = turns to face you more snappily.")]
    [SerializeField] float billboardSharpness = 8f;

    Transform _cam;
    Vector3 _vel;

    void Start()
    {
        if (Camera.main) _cam = Camera.main.transform;
        // Snap to a sensible spot on the first frame so it doesn't fly in from the origin.
        if (_cam) { transform.position = TargetPos(); FaceCamera(1f); }
    }

    void LateUpdate()
    {
        if (_cam == null)
        {
            if (Camera.main == null) return;
            _cam = Camera.main.transform;
        }
        transform.position = Vector3.SmoothDamp(transform.position, TargetPos(), ref _vel, followSmoothTime);
        FaceCamera(1f - Mathf.Exp(-billboardSharpness * Time.deltaTime));
    }

    Vector3 TargetPos()
    {
        Vector3 fwd = _cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd); // camera-relative right, level with the ground
        return _cam.position + fwd * distance + right * horizontal + Vector3.up * vertical;
    }

    void FaceCamera(float t)
    {
        Vector3 toPanel = transform.position - _cam.position;
        if (toPanel.sqrMagnitude < 1e-4f) return;
        Quaternion target = Quaternion.LookRotation(toPanel, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, t);
    }
}
