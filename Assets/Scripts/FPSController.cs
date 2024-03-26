using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour {
    
    [SerializeField] private float walkingSpeed = 7.5f,runningSpeed = 11.5f, jumpSpeed = 8.0f, gravity = 20.0f, lookSpeed = 2.0f,lookXLimit = 45.0f;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift, jumpKey = KeyCode.Space;
    [HideInInspector]public bool canMove = true;
    
    private CharacterController _characterController;
    private Vector3 _moveDirection = Vector3.zero;
    private float _rotationX;
    private Camera _playerCamera;

    private void Start(){
        _characterController = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>();

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update() {
        Movement();
    }

    private void Movement() {
        var forward = GroundedMovement(out var right);
        var movementDirectionY = MovementDirectionCalculation(forward, right);
        Jump(movementDirectionY);
        ApplyGravity();
        MoveController();
        Rotation();
    }

    private void Rotation() {
        // Player and Camera rotation
        if (!canMove) return;
        _rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        _rotationX = Mathf.Clamp(_rotationX, -lookXLimit, lookXLimit);
        _playerCamera.transform.localRotation = Quaternion.Euler(_rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }

    private void MoveController() {
        // Move the controller
        _characterController.Move(_moveDirection * Time.deltaTime);
    }

    private void ApplyGravity() {
        // Apply gravity. 
        if (!_characterController.isGrounded)_moveDirection.y -= gravity * Time.deltaTime;
    }

    private void Jump(float movementDirectionY) {
        if (Input.GetKeyDown(jumpKey) && canMove && _characterController.isGrounded)_moveDirection.y = jumpSpeed;
        else  _moveDirection.y = movementDirectionY;
    }

    private float MovementDirectionCalculation(Vector3 forward, Vector3 right) {
        // Press Left Shift to run
        var isRunning = Input.GetKey(sprintKey);
        var curSpeedX = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical") : 0;
        var curSpeedY = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal") : 0;
        var movementDirectionY = _moveDirection.y;
        _moveDirection = (forward * curSpeedX) + (right * curSpeedY);
        return movementDirectionY;
    }

    private Vector3 GroundedMovement(out Vector3 right) {
        // We are grounded, so recalculate move direction based on axes
        var forward = transform.TransformDirection(Vector3.forward);
        right = transform.TransformDirection(Vector3.right);
        return forward;
    }
}