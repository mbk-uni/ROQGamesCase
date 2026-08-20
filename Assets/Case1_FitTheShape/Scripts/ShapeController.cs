using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Moves a clickable deck shape along a quadratic Bezier arc and removes it on arrival.
/// Attach this component to the root of a deck shape, then assign its matching slot anchor.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShapeController : MonoBehaviour
{
    [Header("Shape Assignment")]
    [Tooltip("Bu objenin şekil türü. Hedef segment ile aynı olmalıdır.")]
    [SerializeField] private ShapeType shapeType = ShapeType.Star;

    [Header("Target")]
    [Tooltip("Önerilen yöntem: Hedef segmentin SegmentController component'ini buraya ata.")]
    [SerializeField] private SegmentController targetSegment;
    [Tooltip("Target Segment atanmamış eski kurulumlar için hedef Transform.")]
    [SerializeField] private Transform targetAnchor;

    [Header("Click Detection")]
    [Tooltip("Boş bırakılırsa Main Camera kullanılır.")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private LayerMask clickLayers = ~0;

    [Header("Flight Curve")]
    [SerializeField, Min(0.01f)] private float duration = 0.55f;
    [Tooltip("Başlangıç ve hedef arasındaki orta noktaya göre eğri kontrol noktasının ofseti. Scene View'daki handle ile düzenlenir.")]
    [SerializeField] private Vector3 controlPointOffset = new Vector3(0f, 1.75f, 0f);
    [Tooltip("Şeklin eğrinin tepe noktasındaki ölçek çarpanı.")]
    [SerializeField, Min(1f)] private float peakScaleMultiplier = 1.3f;
    [SerializeField] private Ease ease = Ease.InOutQuad;
    [SerializeField] private bool ignoreTimeScale = true;

    [Header("Camera Facing")]
    [SerializeField] private bool faceCameraDuringFlight = true;
    [Tooltip("Modelin üst/yüzey yönünü temsil eden local eksen. Bu şekiller için varsayılan Up'tır.")]
    [SerializeField] private Vector3 modelFacingAxis = Vector3.up;

    [Header("Self Rotation")]
    [SerializeField] private bool rotateAroundLocalYDuringFlight = true;
    [Tooltip("Uçuş sırasındaki local Y ekseni dönüş hızı (derece/saniye). Negatif değer ters yöne döner.")]
    [SerializeField] private float localYRotationSpeed = 360f;

    [Header("Flight Visibility")]
    [Tooltip("Uçuş başladığında şeklin doğrudan child objelerini gizler. Gölge/alt katman için kullanılır.")]
    [SerializeField] private bool hideDirectChildrenOnFlight = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onArrived;

    private Collider clickCollider;
    private Tween flightTween;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialScale;
    private bool isFlying;
    private GameObject[] directChildObjects;
    private bool[] childInitialActiveStates;

    public ShapeType ShapeType => shapeType;
    public SegmentController TargetSegment => targetSegment;
    public Transform TargetAnchor => GetTargetAnchor();

    /// <summary>
    /// Returns the editable quadratic Bezier control point in world space.
    /// </summary>
    public Vector3 GetCurveControlPoint()
    {
        var target = GetTargetAnchor();
        if (target == null)
            return transform.position;

        return GetCurveControlPoint(transform.position, target.position);
    }

    /// <summary>
    /// Updates the curve control point from a Scene View handle position.
    /// </summary>
    public void SetCurveControlPoint(Vector3 worldPosition)
    {
        var target = GetTargetAnchor();
        if (target == null)
            return;

        controlPointOffset = worldPosition - (transform.position + target.position) * 0.5f;
    }

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialScale = transform.localScale;
        CacheDirectChildren();
        EnsureClickCollider();
    }

    private void OnEnable()
    {
        isFlying = false;
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialScale;
        RestoreDirectChildren();

        if (clickCollider != null)
            clickCollider.enabled = true;
    }

    private void Update()
    {
        if (isFlying || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
        if (cameraToUse == null)
            return;

        var pointerPosition = Mouse.current.position.ReadValue();
        var ray = cameraToUse.ScreenPointToRay(new Vector3(pointerPosition.x, pointerPosition.y, 0f));
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, clickLayers, QueryTriggerInteraction.Ignore) &&
            (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)))
        {
            FlyToTarget();
        }
    }

    private void OnDisable()
    {
        flightTween?.Kill();
        flightTween = null;
    }

    /// <summary>
    /// Starts the flight. This can also be called from a UI button or another gameplay script.
    /// </summary>
    public void FlyToTarget()
    {
        var target = GetTargetAnchor();
        if (isFlying || target == null)
        {
            if (target == null)
                Debug.LogWarning($"{name} has no target anchor assigned.", this);

            return;
        }

        var segment = GetTargetSegment();
        if (segment != null && segment.TargetShape != shapeType)
        {
            Debug.LogWarning(
                $"{name} is {shapeType}, but {segment.name} only accepts {segment.TargetShape}.",
                this);
            return;
        }

        isFlying = true;
        clickCollider.enabled = false;
        HideDirectChildren();

        var startPosition = transform.position;
        var endPosition = target.position;
        var controlPoint = GetCurveControlPoint(startPosition, endPosition);
        var startScale = transform.localScale;
        var startRotation = transform.rotation;
        var startFacingDirection = startRotation * GetModelFacingAxis();
        var cameraToUse = interactionCamera != null ? interactionCamera : Camera.main;
        var flightElapsedTime = 0f;

        flightTween = DOVirtual.Float(0f, 1f, duration, progress =>
            {
                flightElapsedTime += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                transform.position = EvaluateQuadraticBezier(startPosition, controlPoint, endPosition, progress);
                var scaleProgress = Mathf.Sin(progress * Mathf.PI);
                transform.localScale = startScale * Mathf.Lerp(1f, peakScaleMultiplier, scaleProgress);

                var currentRotation = startRotation;
                if (faceCameraDuringFlight && cameraToUse != null)
                    currentRotation = GetCameraFacingRotation(cameraToUse.transform.position, startRotation, startFacingDirection);

                if (rotateAroundLocalYDuringFlight)
                    currentRotation *= Quaternion.AngleAxis(localYRotationSpeed * flightElapsedTime, Vector3.up);

                transform.rotation = currentRotation;
            })
            .SetEase(ease)
            .SetUpdate(ignoreTimeScale)
            .OnComplete(ArriveAtTarget);
    }

    private void ArriveAtTarget()
    {
        GetTargetSegment()?.PlayArrivalFeedback();
        onArrived?.Invoke();
        gameObject.SetActive(false);
    }

    private void EnsureClickCollider()
    {
        clickCollider = GetComponent<Collider>();
        if (clickCollider != null)
            return;

        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError($"{name} needs a Collider or a MeshFilter to receive clicks.", this);
            enabled = false;
            return;
        }

        var meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        clickCollider = meshCollider;
    }

    private static Vector3 EvaluateQuadraticBezier(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float progress)
    {
        var inverseProgress = 1f - progress;
        return inverseProgress * inverseProgress * start
             + 2f * inverseProgress * progress * control
             + progress * progress * end;
    }

    private Vector3 GetCurveControlPoint(Vector3 startPosition, Vector3 endPosition)
    {
        return (startPosition + endPosition) * 0.5f + controlPointOffset;
    }

    private Transform GetTargetAnchor()
    {
        return targetSegment != null ? targetSegment.HoleTransform : targetAnchor;
    }

    private void CacheDirectChildren()
    {
        directChildObjects = new GameObject[transform.childCount];
        childInitialActiveStates = new bool[transform.childCount];

        for (var index = 0; index < transform.childCount; index++)
        {
            var childObject = transform.GetChild(index).gameObject;
            directChildObjects[index] = childObject;
            childInitialActiveStates[index] = childObject.activeSelf;
        }
    }

    private void HideDirectChildren()
    {
        if (!hideDirectChildrenOnFlight || directChildObjects == null)
            return;

        foreach (var childObject in directChildObjects)
            childObject.SetActive(false);
    }

    private void RestoreDirectChildren()
    {
        if (directChildObjects == null || childInitialActiveStates == null)
            return;

        for (var index = 0; index < directChildObjects.Length; index++)
            directChildObjects[index].SetActive(childInitialActiveStates[index]);
    }

    private SegmentController GetTargetSegment()
    {
        if (targetSegment != null)
            return targetSegment;

        return targetAnchor != null ? targetAnchor.GetComponentInParent<SegmentController>() : null;
    }

    private Vector3 GetModelFacingAxis()
    {
        return modelFacingAxis.sqrMagnitude > 0.0001f ? modelFacingAxis.normalized : Vector3.up;
    }

    private Quaternion GetCameraFacingRotation(
        Vector3 cameraPosition,
        Quaternion startRotation,
        Vector3 startFacingDirection)
    {
        var directionToCamera = cameraPosition - transform.position;
        if (directionToCamera.sqrMagnitude <= 0.0001f)
            return startRotation;

        return Quaternion.FromToRotation(startFacingDirection, directionToCamera.normalized) * startRotation;
    }
}
