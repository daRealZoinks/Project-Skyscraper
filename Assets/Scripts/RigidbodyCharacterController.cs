using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyCharacterController : MonoBehaviour
{
    public float acceleration = 60f;
    public float topSpeed = 8f;
    public float deceleration = 60f;

    public float airControl = 1f;
    public float airBreak = 1f;

    public float jumpHeight = 2f;
    public float gravityScale = 1.5f;

    public float wallRunInitialImpulse = 5f;
    public float wallCheckDistance = 0.75f;
    public float wallJumpHeight = 1.5f;
    public float wallJumpSideForce = 4f;
    public float wallJumpForwardForce = 1f;

    public new Camera camera;

    public UnityEvent OnJump;
    public UnityEvent OnLanded;

    public Vector2 MoveInput { private get; set; }

    public bool IsGrounded { get; private set; }
    public bool IsWallRight { get; private set; }
    public bool IsWallLeft { get; private set; }

    public bool HasWallRunRight { get; private set; }
    public bool HasWallRunLeft { get; private set; }

    private Rigidbody _rigidbody;
    private CapsuleCollider _collider;

    private RaycastHit _rightHitInfo;
    private RaycastHit _leftHitInfo;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
    }

    private void FixedUpdate()
    {
        Move(MoveInput);

        ApplyGravity();

        UpdateRotationBasedOnCamera();

        if (!IsGrounded)
        {
            if (!HasWallRunRight)
            {
                CheckForWallRight();
            }

            if (!HasWallRunLeft)
            {
                CheckForWallLeft();
            }
        }
        else
        {
            IsWallRight = false;
            IsWallLeft = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                if (!IsGrounded)
                {
                    IsGrounded = true;
                    OnLanded?.Invoke();
                }
                break;
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                IsGrounded = true;
                HasWallRunLeft = false;
                HasWallRunRight = false;
                break;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        IsGrounded = false;
    }

    private void UpdateRotationBasedOnCamera()
    {
        if (camera == null)
        {
            return;
        }

        var cameraRotation = camera.transform.rotation.eulerAngles;
        var cameraYRotation = Quaternion.Euler(0, cameraRotation.y, 0);
        _rigidbody.rotation = cameraYRotation;
    }

    private void CheckForWallRight()
    {
        var wasWallRight = IsWallRight;

        if (Physics.Raycast(transform.position + _collider.center, transform.right, out _rightHitInfo, wallCheckDistance))
        {
            IsWallRight = !_rightHitInfo.collider.isTrigger;
        }
        else
        {
            IsWallRight = false;
        }

        var boostForce = Vector3.zero;

        if (IsWallRight && !wasWallRight)
        {
            boostForce = -Vector3.Cross(_rightHitInfo.normal, transform.up) * wallRunInitialImpulse;
            HasWallRunLeft = false;
            HasWallRunRight = true;
        }

        _rigidbody.AddForce(boostForce, ForceMode.VelocityChange);

        if (IsWallRight) _rigidbody.AddForce(-_rightHitInfo.normal, ForceMode.Acceleration);
    }

    private void CheckForWallLeft()
    {
        var wasWallLeft = IsWallLeft;

        if (Physics.Raycast(transform.position + _collider.center, -transform.right, out _leftHitInfo, wallCheckDistance))
        {
            IsWallLeft = !_leftHitInfo.collider.isTrigger;
        }
        else
        {
            IsWallLeft = false;
        }

        var boostForce = Vector3.zero;

        if (IsWallLeft && !wasWallLeft)
        {
            boostForce = Vector3.Cross(_leftHitInfo.normal, transform.up) * wallRunInitialImpulse;
            HasWallRunLeft = true;
            HasWallRunRight = false;
        }

        _rigidbody.AddForce(boostForce, ForceMode.VelocityChange);

        if (IsWallLeft) _rigidbody.AddForce(-_leftHitInfo.normal, ForceMode.Acceleration);
    }

    private void ApplyGravity()
    {
        if (IsGrounded)
        {
            return;
        }
        var gravity = Physics.gravity * (gravityScale - 1);
        _rigidbody.AddForce(gravity, ForceMode.Acceleration);
    }

    private void Move(Vector2 moveInput)
    {
        var inputDirection = transform.right * moveInput.x + transform.forward * moveInput.y;

        var horizontalRigidbodyVelocity = new Vector3
        {
            x = _rigidbody.linearVelocity.x,
            z = _rigidbody.linearVelocity.z
        };

        var horizontalClampedRigidbodyVelocity = horizontalRigidbodyVelocity.normalized * Mathf.Clamp01(horizontalRigidbodyVelocity.magnitude / topSpeed);

        Vector3 finalForce = inputDirection - horizontalClampedRigidbodyVelocity;

        finalForce *= (inputDirection != Vector3.zero) ? acceleration : deceleration;

        if (!IsGrounded)
        {
            finalForce *= (inputDirection != Vector3.zero) ? airControl : airBreak;
        }

        _rigidbody.AddForce(finalForce, ForceMode.Acceleration);
    }

    public void Jump()
    {
        if (IsGrounded)
        {
            OnJump.Invoke();

            ExecuteJump();
        }
        else
        {
            if (IsWallRight || IsWallLeft)
            {
                ExecuteWallJump();
            }
        }
    }

    private void ExecuteJump()
    {
        var jumpForce = Vector3.up * Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityScale);

        _rigidbody.linearVelocity = new Vector3()
        {
            x = _rigidbody.linearVelocity.x,
            z = _rigidbody.linearVelocity.z
        };

        _rigidbody.AddForce(jumpForce, ForceMode.VelocityChange);

        IsGrounded = false;
    }

    private void ExecuteWallJump()
    {
        var sideForce = Vector3.zero;
        if (IsWallRight) sideForce = _rightHitInfo.normal * wallJumpSideForce;
        if (IsWallLeft) sideForce = _leftHitInfo.normal * wallJumpSideForce;

        var jumpForce = Vector3.up * Mathf.Sqrt(-2 * Physics.gravity.y * gravityScale * wallJumpHeight);

        var forwardForce = transform.forward * wallJumpForwardForce;

        var finalForce = sideForce + jumpForce + forwardForce;

        _rigidbody.linearVelocity = new Vector3()
        {
            x = _rigidbody.linearVelocity.x,
            z = _rigidbody.linearVelocity.z
        };

        _rigidbody.AddForce(finalForce, ForceMode.VelocityChange);

        IsWallRight = false;
        IsWallLeft = false;
    }
}
