using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // 점프, 구르기

    [SerializeField]
    private float _moveSpeed = 5f;

    private Rigidbody _rb;
    private Vector2 _inputDir;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }
    void OnMove(InputValue value)
    {
        _inputDir = value.Get<Vector2>();
    }

    void FixedUpdate()
    {
        Vector3 move = (transform.forward * _inputDir.y + transform.right * _inputDir.x) * _moveSpeed;
        _rb.MovePosition(_rb.position + move * Time.fixedDeltaTime);
    }


}
