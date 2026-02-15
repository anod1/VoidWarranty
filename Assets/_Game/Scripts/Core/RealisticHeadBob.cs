using UnityEngine;

/// <summary>
/// Applies a head bob offset to a camera transform based on CharacterController movement.
/// Modular: does not set absolute positions, only adds/removes its own offset.
/// </summary>
public class RealisticHeadBob : MonoBehaviour
{
    [Header("Réglages")]
    public float walkingBobSpeed = 14f;
    public float bobAmountX = 0.05f;
    public float bobAmountY = 0.05f;

    [Header("Références")]
    public Transform cameraTransform;
    public CharacterController controller;

    private float _timer;
    private Vector3 _currentOffset;

    void Update()
    {
        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        float speed = horizontalVelocity.magnitude;

        Vector3 targetOffset;

        if (speed > 0.1f && controller.isGrounded)
        {
            _timer += Time.deltaTime * walkingBobSpeed;
            float offsetY = Mathf.Sin(_timer) * bobAmountY;
            float offsetX = Mathf.Cos(_timer / 2f) * bobAmountX;
            targetOffset = new Vector3(offsetX, offsetY, 0f);
        }
        else
        {
            _timer = 0f;
            targetOffset = Vector3.zero;
        }

        // Remove previous offset, apply new one
        cameraTransform.localPosition -= _currentOffset;
        _currentOffset = Vector3.Lerp(_currentOffset, targetOffset, Time.deltaTime * 5f);
        cameraTransform.localPosition += _currentOffset;
    }

    private void OnDisable()
    {
        // Clean up offset when disabled
        if (cameraTransform != null)
        {
            cameraTransform.localPosition -= _currentOffset;
            _currentOffset = Vector3.zero;
        }
    }
}
