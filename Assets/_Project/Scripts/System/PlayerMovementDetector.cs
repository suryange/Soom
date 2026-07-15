using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementDetector : MonoBehaviour
{
    [Header("Input Action")]
    public InputActionProperty moveAction;
    public float moveThreshold = 0.1f;

    [Header("Movement Fallback")]
    [Tooltip("XRI 이동 Provider가 입력을 받았지만 XR Origin을 움직이지 못할 때만 직접 이동합니다.")]
    [SerializeField] private bool useMovementFallback = true;
    [SerializeField] private float fallbackMoveSpeed = 2.5f;

    private CharacterController characterController;
    private Transform viewTransform;
    private Vector2 currentMoveInput;
    private Vector3 positionAtLastLateUpdate;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        CacheViewTransform();
        positionAtLastLateUpdate = transform.position;
    }

    private void Update()
    {
        if (PlayerStateManager.Instance == null)
        {
            currentMoveInput = Vector2.zero;
            return;
        }

        currentMoveInput = moveAction.action != null
            ? moveAction.action.ReadValue<Vector2>()
            : Vector2.zero;
        bool isMoving = currentMoveInput.magnitude > moveThreshold;

        PlayerStateManager.Instance.SetMoving(isMoving);
    }

    private void LateUpdate()
    {
        if (!useMovementFallback || currentMoveInput.magnitude <= moveThreshold)
        {
            positionAtLastLateUpdate = transform.position;
            return;
        }

        if (!IsLocomotionAllowed())
        {
            positionAtLastLateUpdate = transform.position;
            return;
        }

        // DynamicMoveProvider가 이미 이동시켰다면 중복 이동하지 않는다.
        if ((transform.position - positionAtLastLateUpdate).sqrMagnitude > 0.000001f)
        {
            positionAtLastLateUpdate = transform.position;
            return;
        }

        if (viewTransform == null)
            CacheViewTransform();

        Vector3 forward = viewTransform != null ? viewTransform.forward : transform.forward;
        Vector3 right = viewTransform != null ? viewTransform.right : transform.right;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;

        Vector3 direction = forward * currentMoveInput.y + right * currentMoveInput.x;
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        Vector3 displacement = direction * (fallbackMoveSpeed * Time.deltaTime);
        if (characterController != null && characterController.enabled)
            characterController.Move(displacement);
        else
            transform.position += displacement;

        positionAtLastLateUpdate = transform.position;
    }

    private bool IsLocomotionAllowed()
    {
        if (PlayerStateManager.Instance == null)
            return true;

        PlayerState state = PlayerStateManager.Instance.CurrentState;
        return state != PlayerState.Interact && state != PlayerState.BreathingActive;
    }

    private void CacheViewTransform()
    {
        Camera mainCamera = Camera.main;
        viewTransform = mainCamera != null ? mainCamera.transform : null;
    }
}
