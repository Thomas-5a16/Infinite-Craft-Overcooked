using Cinemachine;
using ECM2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;

public class Player : MonoBehaviour
{
    [Header("Camera Zoom Control")]
    private float cameraZoom = 7.6f;
    [SerializeField] private float zoomSpeed = 10f;
    private float minZoom = 4.05f;
    private float maxZoom = 9.77f;

    private Queue<float> zoomInputs = new Queue<float>();  // Queue to store the last 5 zoom inputs
    private int bufferSize = 5;  // Number of frames to average over

    protected Character _character;
    private CinemachineVirtualCamera virtualCamera;
    private ConeCastHelper coneCastHelper;

    [Header("Holding Object")]
    [SerializeField] private Transform holdingObjectTransform;
    [SerializeField] private Transform lookingRaycastPositionTransform;

    [Header("Raycast")]
    [SerializeField] private bool debugVisualizeRays = true;
    [SerializeField] private float rayCastAngle = 25f;
    [SerializeField] private int numRaycastRays = 20;
    [SerializeField] private float raycastDistance = 1.25f;

    private PickupableObject hoveringObject;
    private PickupableObject pickedupObject;
    private bool isHoldingObject;

    protected virtual void Awake() {
        _character = GetComponent<Character>();
        virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

        coneCastHelper = new ConeCastHelper();
        coneCastHelper.InitializeConeCast(rayCastAngle, numRaycastRays);
    }

    protected virtual void Start() {
        InputManager.Instance.inputActions.Player.Crouch.started += OnCrouchPressed;
        InputManager.Instance.inputActions.Player.Crouch.canceled += OnCrouchReleased;
        InputManager.Instance.inputActions.Player.Jump.started += OnJumpPressed;
        InputManager.Instance.inputActions.Player.Jump.canceled += OnJumpReleased;
        InputManager.Instance.inputActions.Player.Pickup.performed += OnPickupPressed;
        InputManager.Instance.inputActions.Player.LaunchItem.performed += OnLaunchItemPressed;
    }

    protected virtual void Update() {
        // Movement input
        Vector2 inputMove = InputManager.Instance.GetMovementInputVector();

        Vector3 movementDirection = Vector3.zero;

        movementDirection += Vector3.right * inputMove.x;
        movementDirection += Vector3.forward * inputMove.y;

        if (_character.cameraTransform)
            movementDirection = movementDirection.relativeTo(_character.cameraTransform, _character.GetUpVector());
        _character.SetMovementDirection(movementDirection);

        HandleHoverObjects();

        HandleCameraZoom();
    }
    private void HandleHoverObjects() {
        //RaycastHit[] raycastHits = physics.ConeCastAll(lookingRaycastPositionTransform.position, 1.5f, transform.forward, 2f, 50f);
        RaycastHit[] raycastHits = coneCastHelper.ConeCast(lookingRaycastPositionTransform.position, transform.forward, raycastDistance);
        if(debugVisualizeRays) {
            foreach (var hit in raycastHits) {
                Debug.DrawLine(lookingRaycastPositionTransform.position, hit.point, Color.red);
            }
        }

        foreach (RaycastHit hit in raycastHits) {
            PickupableObject pickupableObject = hit.collider.GetComponentInParent<PickupableObject>();
            if (pickupableObject != null && !pickupableObject.IsPickedUp) {
                pickupableObject.HoverOver(this);
                hoveringObject = pickupableObject;
                return;
            }
        }
        hoveringObject = null;
    }

    private void OnPickupPressed(InputAction.CallbackContext context) {
        if (hoveringObject != null && !isHoldingObject) {
            isHoldingObject = true;
            hoveringObject.GetComponent<Rigidbody>().isKinematic = true;
            hoveringObject.Pickup(this);
            pickedupObject = hoveringObject;
        }
        else if (isHoldingObject) {
            isHoldingObject = false;
            pickedupObject.Drop(this);
            pickedupObject.GetComponent<Rigidbody>().isKinematic = false;
            pickedupObject = null;
        }
    }
    private void OnLaunchItemPressed(InputAction.CallbackContext context) {
        if(isHoldingObject) {
            isHoldingObject = false;
            pickedupObject.Drop(this);
            pickedupObject.GetComponent<Rigidbody>().isKinematic = false;
            pickedupObject.GetComponent<Rigidbody>().AddExplosionForce(1500f, holdingObjectTransform.position - (transform.forward * 0.2f), 0.5f, 0.1f);
            pickedupObject = null;
        }
    }
    private void HandleCameraZoom() {
        float zoomInput = InputManager.Instance.GetCameraZoomInputDelta();

        zoomInputs.Enqueue(zoomInput);
        if (zoomInputs.Count > bufferSize) {
            zoomInputs.Dequeue();
        }

        float averageZoomInput = GetAverageZoomInput();

        if (Mathf.Abs(averageZoomInput) > 0.01f) {
            float targetZoom = Mathf.Clamp(cameraZoom - averageZoomInput, minZoom, maxZoom);
            cameraZoom = Mathf.Lerp(cameraZoom, targetZoom, zoomSpeed * Time.deltaTime);

            virtualCamera.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset = new Vector3(0, cameraZoom, CameraZoomZFunction(cameraZoom));
            virtualCamera.transform.rotation = Quaternion.Euler(CameraRotationXFunction(cameraZoom), 0f, 0f);
        }
    }
    private float GetAverageZoomInput() {
        float sum = 0f;
        foreach (float input in zoomInputs) {
            sum += input;
        }
        return sum / zoomInputs.Count;
    }
    private float CameraZoomZFunction(float y) {
        return (0.1375f * y * y) - (2.149f * y) + 4.196f;
    }
    private float CameraRotationXFunction(float y) {
        return (0.6286f * y * y) - (7.124f * y) + 78.95f;
    }
    public Vector3 GetHoldingObjectPosition() {
        return holdingObjectTransform.position;
    }
    private void OnCrouchPressed(InputAction.CallbackContext context) {
        _character.Crouch();
    }
    private void OnCrouchReleased(InputAction.CallbackContext context) {
        _character.UnCrouch();
    }
    private void OnJumpPressed(InputAction.CallbackContext context) {
        _character.Jump();
    }
    private void OnJumpReleased(InputAction.CallbackContext context) {
        _character.StopJumping();
    }

}