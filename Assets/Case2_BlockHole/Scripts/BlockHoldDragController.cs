using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach this to the Blocks parent. Its direct child blocks can then be held
/// and dragged freely on the X/Z plane.
/// </summary>
[DisallowMultipleComponent]
public sealed class BlockHoldDragController : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Boş bırakılırsa Main Camera kullanılır.")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private LayerMask selectableLayers = ~0;

    [Header("Hold Feedback")]
    [SerializeField, Min(0f)] private float liftHeight = 0.35f;
    [SerializeField, Min(0f)] private float positionFollowSharpness = 24f;
    [SerializeField, Range(0f, 45f)] private float maximumTiltAngle = 12f;
    [SerializeField, Min(0f)] private float rotationFollowSharpness = 18f;

    private Transform heldBlock;
    private Vector3 heldRestPosition;
    private Quaternion heldRestRotation;
    private Vector3 dragOffset;
    private Plane dragPlane;

    private void Update()
    {
        if (heldBlock == null)
        {
            if (TryGetPointerDown(out var pointerPosition))
                TryBeginHold(pointerPosition);

            return;
        }

        if (WasPointerReleased())
        {
            EndHold();
            return;
        }

        if (TryGetPointerPosition(out var heldPointerPosition))
            UpdateHeldBlock(heldPointerPosition);
    }

    private void OnDisable()
    {
        if (heldBlock != null)
            EndHold();
    }

    private void TryBeginHold(Vector2 pointerPosition)
    {
        var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
        if (cameraToUse == null)
            return;

        var ray = cameraToUse.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, selectableLayers, QueryTriggerInteraction.Ignore))
            return;

        var blockRoot = FindDirectBlockChild(hit.collider.transform);
        if (blockRoot == null)
            return;

        heldBlock = blockRoot;
        heldRestPosition = heldBlock.position;
        heldRestRotation = heldBlock.rotation;
        SetOutlineEnabled(heldBlock, true);
        dragPlane = new Plane(Vector3.up, heldRestPosition + Vector3.up * liftHeight);

        if (dragPlane.Raycast(ray, out var enterDistance))
        {
            dragOffset = heldRestPosition - ray.GetPoint(enterDistance);
            dragOffset.y = 0f;
        }
        else
        {
            dragOffset = Vector3.zero;
        }

        heldBlock.position = heldRestPosition + Vector3.up * liftHeight;
    }

    private void UpdateHeldBlock(Vector2 pointerPosition)
    {
        var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
        if (cameraToUse == null)
            return;

        var ray = cameraToUse.ScreenPointToRay(pointerPosition);
        if (!dragPlane.Raycast(ray, out var enterDistance))
            return;

        var targetPosition = ray.GetPoint(enterDistance) + dragOffset;
        targetPosition.y = heldRestPosition.y + liftHeight;

        var currentPosition = heldBlock.position;
        var planarDelta = targetPosition - currentPosition;
        planarDelta.y = 0f;

        var positionBlend = GetFrameBlend(positionFollowSharpness);
        heldBlock.position = Vector3.Lerp(currentPosition, targetPosition, positionBlend);

        var targetRotation = heldRestRotation;
        if (planarDelta.sqrMagnitude > 0.000001f)
        {
            var movementDirection = planarDelta.normalized;
            var tiltEuler = new Vector3(
                movementDirection.z * maximumTiltAngle,
                0f,
                -movementDirection.x * maximumTiltAngle);
            targetRotation = heldRestRotation * Quaternion.Euler(tiltEuler);
        }

        heldBlock.rotation = Quaternion.Slerp(
            heldBlock.rotation,
            targetRotation,
            GetFrameBlend(rotationFollowSharpness));
    }

    private void EndHold()
    {
        SetOutlineEnabled(heldBlock, false);
        heldBlock.position = new Vector3(
            heldBlock.position.x,
            heldRestPosition.y,
            heldBlock.position.z);
        heldBlock.rotation = heldRestRotation;
        heldBlock = null;
    }

    private Transform FindDirectBlockChild(Transform hitTransform)
    {
        var current = hitTransform;
        while (current != null && current.parent != transform)
            current = current.parent;

        return current != null && current.parent == transform ? current : null;
    }

    private static void SetOutlineEnabled(Transform blockRoot, bool isEnabled)
    {
        foreach (var behaviour in blockRoot.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour.GetType().Name == "Outline")
                behaviour.enabled = isEnabled;
        }
    }

    private static bool TryGetPointerDown(out Vector2 position)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private static bool TryGetPointerPosition(out Vector2 position)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private static bool WasPointerReleased()
    {
        return (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame) ||
               (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame);
    }

    private static float GetFrameBlend(float sharpness)
    {
        return 1f - Mathf.Exp(-sharpness * Time.deltaTime);
    }
}
