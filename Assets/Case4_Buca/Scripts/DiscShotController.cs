using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class DiscShotController : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Boş bırakılırsa Main Camera kullanılır.")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private LayerMask interactionLayers = ~0;
    [Tooltip("Açıksa disc hareket ederken de yeni atış hazırlanabilir.")]
    [SerializeField] private bool allowAimWhileMoving;
    [SerializeField, Min(0f)] private float movingAimSpeedThreshold = 0.15f;

    [Header("Shot")]
    [SerializeField, Min(0.01f)] private float maxPullDistance = 4f;
    [SerializeField, Min(0f)] private float minPullDistance = 0.15f;
    [SerializeField, Min(0f)] private float minLaunchSpeed = 3f;
    [SerializeField, Min(0.01f)] private float maxLaunchSpeed = 18f;
    [SerializeField] private AnimationCurve powerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Movement")]
    [Tooltip("Disc'in saniyedeki yavaşlama miktarı.")]
    [SerializeField, Min(0f)] private float frictionDeceleration = 3.2f;
    [SerializeField, Min(0f)] private float stopSpeedThreshold = 0.12f;
    [Tooltip("Dikey bir yüzeye çarptıktan sonra korunacak hız oranı.")]
    [SerializeField, Range(0f, 1f)] private float wallBounceRetention = 0.78f;

    [Header("Aim Indicator")]
    [Tooltip("Boş bırakılırsa URP Unlit materyal runtime'da oluşturulur.")]
    [SerializeField] private Material indicatorMaterial;
    [SerializeField] private Color lowPowerColor = new(1f, 0.88f, 0.15f, 1f);
    [SerializeField] private Color highPowerColor = new(1f, 0.28f, 0.05f, 1f);
    [SerializeField, Min(0.01f)] private float indicatorMinLength = 0.35f;
    [SerializeField, Min(0.01f)] private float indicatorMaxLength = 4.5f;
    [SerializeField, Min(0.01f)] private float indicatorWidth = 0.8f;
    [Tooltip("Göstergenin disc merkezinden başlama mesafesi.")]
    [SerializeField, Min(0f)] private float indicatorStartOffset = 0.65f;
    [Tooltip("Zeminle çakışmayı engelleyen dünya Y ofseti.")]
    [SerializeField] private float indicatorHeightOffset = 0.08f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Rigidbody discRigidbody;
    private Collider discCollider;
    private GameObject indicatorObject;
    private Mesh indicatorMesh;
    private MeshRenderer indicatorRenderer;
    private Material runtimeIndicatorMaterial;
    private MaterialPropertyBlock indicatorProperties;

    private bool isAiming;
    private Vector3 aimPointerStart;
    private Vector3 aimDirection;
    private float pullDistance;
    private Vector3 lastPlanarVelocity;

    public bool IsAiming => isAiming;
    public float Power01 => maxPullDistance > 0f ? Mathf.Clamp01(pullDistance / maxPullDistance) : 0f;

    private void Awake()
    {
        discRigidbody = GetComponent<Rigidbody>();
        discCollider = GetComponent<Collider>();

        if (interactionCamera == null)
            interactionCamera = Camera.main;

        if (powerCurve == null || powerCurve.length == 0)
            powerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        ConfigureRigidbody();
        CreateAimIndicator();
    }

    private void Update()
    {
        if (!isAiming)
        {
            if (TryGetPointerDown(out var pointerPosition) && CanBeginAim() && WasDiscPressed(pointerPosition))
                BeginAim(pointerPosition);

            return;
        }

        if (WasPointerReleased())
        {
            ReleaseShot();
            return;
        }

        if (TryGetPointerPosition(out var heldPointerPosition))
            UpdateAim(heldPointerPosition);
    }

    private void FixedUpdate()
    {
        if (isAiming)
        {
            discRigidbody.linearVelocity = Vector3.zero;
            discRigidbody.angularVelocity = Vector3.zero;
            lastPlanarVelocity = Vector3.zero;
            return;
        }

        var planarVelocity = GetPlanarVelocity();
        if (planarVelocity.sqrMagnitude <= stopSpeedThreshold * stopSpeedThreshold)
        {
            if (planarVelocity.sqrMagnitude > 0f)
                discRigidbody.linearVelocity = Vector3.zero;

            discRigidbody.angularVelocity = Vector3.zero;
            lastPlanarVelocity = Vector3.zero;
            return;
        }

        var slowedVelocity = Vector3.MoveTowards(
            planarVelocity,
            Vector3.zero,
            frictionDeceleration * Time.fixedDeltaTime);

        discRigidbody.linearVelocity = slowedVelocity;
        lastPlanarVelocity = slowedVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isAiming || lastPlanarVelocity.sqrMagnitude <= stopSpeedThreshold * stopSpeedThreshold)
            return;

        var scatterGroup = collision.collider.GetComponentInParent<CubeScatterController>();
        if (scatterGroup != null)
        {
            var impactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(discRigidbody.position);
            var startedScatter = scatterGroup.TryScatter(impactPoint, lastPlanarVelocity);
            var retainedVelocity = lastPlanarVelocity *
                                   (startedScatter ? scatterGroup.DiscSpeedRetention : 1f);

            retainedVelocity.y = 0f;
            discRigidbody.linearVelocity = retainedVelocity;
            lastPlanarVelocity = retainedVelocity;
            return;
        }

        var bestWallNormal = Vector3.zero;
        var strongestApproach = 0f;

        for (var i = 0; i < collision.contactCount; i++)
        {
            var wallNormal = Vector3.ProjectOnPlane(collision.GetContact(i).normal, Vector3.up);
            if (wallNormal.sqrMagnitude < 0.25f)
                continue;

            wallNormal.Normalize();
            var approach = Vector3.Dot(lastPlanarVelocity.normalized, wallNormal);
            if (approach < strongestApproach)
            {
                strongestApproach = approach;
                bestWallNormal = wallNormal;
            }
        }

        if (bestWallNormal == Vector3.zero || strongestApproach > -0.02f)
            return;

        var reflectedVelocity = Vector3.Reflect(lastPlanarVelocity, bestWallNormal) * wallBounceRetention;
        reflectedVelocity.y = 0f;
        discRigidbody.linearVelocity = reflectedVelocity;
        lastPlanarVelocity = reflectedVelocity;
    }

    private void OnDisable()
    {
        isAiming = false;
        pullDistance = 0f;
        SetIndicatorVisible(false);
    }

    private void OnDestroy()
    {
        if (indicatorObject != null)
            Destroy(indicatorObject);

        if (runtimeIndicatorMaterial != null)
            Destroy(runtimeIndicatorMaterial);

        if (indicatorMesh != null)
            Destroy(indicatorMesh);
    }

    private void ConfigureRigidbody()
    {
        discRigidbody.useGravity = false;
        discRigidbody.isKinematic = false;
        discRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        discRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        discRigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                    RigidbodyConstraints.FreezeRotationX |
                                    RigidbodyConstraints.FreezeRotationZ;
    }

    private bool CanBeginAim()
    {
        return allowAimWhileMoving || GetPlanarVelocity().sqrMagnitude <= movingAimSpeedThreshold * movingAimSpeedThreshold;
    }

    private bool WasDiscPressed(Vector2 screenPosition)
    {
        if (interactionCamera == null)
            return false;

        var ray = interactionCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, interactionLayers, QueryTriggerInteraction.Ignore))
            return false;

        if (discCollider != null && hit.collider == discCollider)
            return true;

        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private void BeginAim(Vector2 screenPosition)
    {
        if (!TryScreenToBoard(screenPosition, out aimPointerStart))
            return;

        isAiming = true;
        pullDistance = 0f;
        aimDirection = Vector3.zero;
        discRigidbody.linearVelocity = Vector3.zero;
        discRigidbody.angularVelocity = Vector3.zero;
        UpdateAim(screenPosition);
    }

    private void UpdateAim(Vector2 screenPosition)
    {
        if (!TryScreenToBoard(screenPosition, out var pointerWorldPosition))
            return;

        // Atış yönü, oyuncunun parmağını/mouse'u çektiği yönün tersidir.
        // Başlangıç noktasını kullanmak, disc'in hangi noktasına basılırsa basılsın gücü sıfırdan başlatır.
        var pullVector = aimPointerStart - pointerWorldPosition;
        pullVector.y = 0f;

        pullDistance = Mathf.Min(pullVector.magnitude, maxPullDistance);
        if (pullVector.sqrMagnitude <= 0.0001f)
        {
            SetIndicatorVisible(false);
            return;
        }

        aimDirection = pullVector.normalized;
        UpdateAimIndicator(Power01);
    }

    private void ReleaseShot()
    {
        isAiming = false;
        SetIndicatorVisible(false);

        if (pullDistance < minPullDistance || aimDirection.sqrMagnitude <= 0.0001f)
        {
            pullDistance = 0f;
            return;
        }

        var evaluatedPower = Mathf.Clamp01(powerCurve.Evaluate(Power01));
        var launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, evaluatedPower);
        var launchVelocity = aimDirection * launchSpeed;
        launchVelocity.y = 0f;

        discRigidbody.linearVelocity = launchVelocity;
        discRigidbody.angularVelocity = Vector3.zero;
        discRigidbody.WakeUp();
        lastPlanarVelocity = launchVelocity;
        pullDistance = 0f;
    }

    private bool TryScreenToBoard(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = default;
        if (interactionCamera == null)
            return false;

        var boardPlane = new Plane(Vector3.up, discRigidbody.position);
        var ray = interactionCamera.ScreenPointToRay(screenPosition);
        if (!boardPlane.Raycast(ray, out var distance))
            return false;

        worldPosition = ray.GetPoint(distance);
        return true;
    }

    private Vector3 GetPlanarVelocity()
    {
        var velocity = discRigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity;
    }

    private void CreateAimIndicator()
    {
        indicatorObject = new GameObject($"{name}_AimIndicator");
        indicatorObject.layer = gameObject.layer;
        indicatorObject.hideFlags = HideFlags.DontSave;

        var meshFilter = indicatorObject.AddComponent<MeshFilter>();
        indicatorRenderer = indicatorObject.AddComponent<MeshRenderer>();
        indicatorRenderer.shadowCastingMode = ShadowCastingMode.Off;
        indicatorRenderer.receiveShadows = false;
        indicatorRenderer.lightProbeUsage = LightProbeUsage.Off;
        indicatorRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        indicatorMesh = new Mesh { name = $"{name}_AimTriangle" };
        indicatorMesh.MarkDynamic();
        meshFilter.sharedMesh = indicatorMesh;

        if (indicatorMaterial != null)
        {
            indicatorRenderer.sharedMaterial = indicatorMaterial;
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("Aim indicator için uygun bir Unlit shader bulunamadı.", this);
                indicatorObject.SetActive(false);
                return;
            }

            runtimeIndicatorMaterial = new Material(shader)
            {
                name = $"{name}_AimIndicator_Runtime",
                renderQueue = 3100
            };
            indicatorRenderer.sharedMaterial = runtimeIndicatorMaterial;
        }

        indicatorProperties = new MaterialPropertyBlock();
        indicatorObject.SetActive(false);
    }

    private void UpdateAimIndicator(float power)
    {
        if (indicatorObject == null || indicatorRenderer == null || indicatorRenderer.sharedMaterial == null)
            return;

        var length = Mathf.Lerp(indicatorMinLength, indicatorMaxLength, power);
        var halfWidth = indicatorWidth * 0.5f;

        indicatorMesh.Clear();
        indicatorMesh.vertices = new[]
        {
            new Vector3(-halfWidth, 0f, 0f),
            new Vector3(0f, 0f, length),
            new Vector3(halfWidth, 0f, 0f)
        };
        indicatorMesh.triangles = new[] { 0, 1, 2 };
        indicatorMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(1f, 0f)
        };
        indicatorMesh.RecalculateNormals();
        indicatorMesh.RecalculateBounds();

        indicatorObject.transform.SetPositionAndRotation(
            discRigidbody.position + Vector3.up * indicatorHeightOffset + aimDirection * indicatorStartOffset,
            Quaternion.LookRotation(aimDirection, Vector3.up));

        var indicatorColor = Color.Lerp(lowPowerColor, highPowerColor, power);
        indicatorRenderer.GetPropertyBlock(indicatorProperties);
        indicatorProperties.SetColor(BaseColorId, indicatorColor);
        indicatorProperties.SetColor(ColorId, indicatorColor);
        indicatorRenderer.SetPropertyBlock(indicatorProperties);
        SetIndicatorVisible(true);
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (indicatorObject != null && indicatorObject.activeSelf != visible)
            indicatorObject.SetActive(visible);
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

    private void OnValidate()
    {
        maxPullDistance = Mathf.Max(0.01f, maxPullDistance);
        minPullDistance = Mathf.Clamp(minPullDistance, 0f, maxPullDistance);
        maxLaunchSpeed = Mathf.Max(minLaunchSpeed, maxLaunchSpeed);
        indicatorMaxLength = Mathf.Max(indicatorMinLength, indicatorMaxLength);
    }
}
