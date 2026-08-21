using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BlockFragmentContainment : MonoBehaviour
{
    private Rigidbody fragmentRigidbody;
    private Vector3 center;
    private float containmentRadius;
    private float gravityScale;
    private bool isFalling;

    public void Configure(Vector3 containmentCenter, float radius)
    {
        center = containmentCenter;
        containmentRadius = radius;
        fragmentRigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Starts a deliberately softer-than-Physics.gravity fall after the visual
    /// separation phase has had time to read clearly.
    /// </summary>
    public void BeginFalling(float newGravityScale)
    {
        gravityScale = Mathf.Max(0f, newGravityScale);
        isFalling = true;
    }

    private void FixedUpdate()
    {
        if (fragmentRigidbody == null || containmentRadius <= 0f)
            return;

        if (isFalling && gravityScale > 0f)
            fragmentRigidbody.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);

        var position = fragmentRigidbody.position;
        var planarOffset = position - center;
        planarOffset.y = 0f;
        if (planarOffset.sqrMagnitude <= containmentRadius * containmentRadius)
            return;

        var clampedOffset = planarOffset.normalized * containmentRadius;
        fragmentRigidbody.position = new Vector3(
            center.x + clampedOffset.x,
            position.y,
            center.z + clampedOffset.z);

        var velocity = fragmentRigidbody.linearVelocity;
        var outwardSpeed = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), planarOffset.normalized);
        if (outwardSpeed > 0f)
            fragmentRigidbody.linearVelocity -= planarOffset.normalized * outwardSpeed;
    }
}
