using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float _moveSpeed = 5f;

    [SerializeField]
    private float _jumpForce = 1f;

    [SerializeField]
    private float _mouseSensitivity = 0.15f;

    [SerializeField]
    private float _minPitch = -20f;

    [SerializeField]
    private float _maxPitch = 10f;

    [SerializeField]
    private LayerMask _groundLayerMask;

    private Rigidbody _rb;
    private Vector2 _inputDir;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        Yaw = transform.eulerAngles.y;
    }

    private void OnMove(InputValue value)
    {
        _inputDir = value.Get<Vector2>();
    }

    private void OnLook(InputValue value)
    {
        Vector2 delta = value.Get<Vector2>() * _mouseSensitivity;
        Yaw += delta.x;
        Pitch = Mathf.Clamp(Pitch - delta.y, _minPitch, _maxPitch);
        //transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
    }

    private void OnJump(InputValue value)
    {
        if (value.isPressed && IsGrounded())
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
    }

    private bool IsGrounded()
    {
        Ray[] rays = new Ray[4]
        {
            new Ray(
                transform.position + (transform.forward * 0.2f) + (transform.up * 0.01f),
                Vector3.down
            ),
            new Ray(
                transform.position + (-transform.forward * 0.2f) + (transform.up * 0.01f),
                Vector3.down
            ),
            new Ray(
                transform.position + (transform.right * 0.2f) + (transform.up * 0.01f),
                Vector3.down
            ),
            new Ray(
                transform.position + (-transform.right * 0.2f) + (transform.up * 0.01f),
                Vector3.down
            ),
        };

        for (int i = 0; i < rays.Length; i++)
        {
            if (Physics.Raycast(rays[i], 0.1f, _groundLayerMask))
                return true;
        }

        return false;
    }

    private void FixedUpdate()
    {
        _rb.MoveRotation(Quaternion.Euler(0f, Yaw, 0f));

        Vector3 move =
            (transform.forward * _inputDir.y + transform.right * _inputDir.x) * _moveSpeed;
        move.y = _rb.linearVelocity.y;
        _rb.linearVelocity = move;
    }
}
