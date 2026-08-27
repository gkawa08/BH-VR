using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public sealed class MainCanvasPlayerFacingGrab : MonoBehaviour
{
    [Header("Player Facing")]
    [SerializeField] private Transform playerHead;
    [SerializeField] private bool keepUpright = true;
    [SerializeField] private bool facePlayerOnRelease = true;
    [SerializeField] private bool canvasFrontIsNegativeZ = true;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool isGrabbed;

    private void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        ResolvePlayerHead();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        grabInteractable.selectEntered.AddListener(OnGrabStarted);
        grabInteractable.selectExited.AddListener(OnGrabEnded);
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.RemoveListener(OnGrabStarted);
        grabInteractable.selectExited.RemoveListener(OnGrabEnded);
        isGrabbed = false;
    }

    private void LateUpdate()
    {
        if (!isGrabbed)
        {
            return;
        }

        FacePlayer();
    }

    private void OnGrabStarted(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        FacePlayer();
    }

    private void OnGrabEnded(SelectExitEventArgs args)
    {
        isGrabbed = false;

        if (facePlayerOnRelease)
        {
            FacePlayer();
        }
    }

    private void ResolvePlayerHead()
    {
        if (playerHead != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            playerHead = mainCamera.transform;
        }
    }

    private void FacePlayer()
    {
        ResolvePlayerHead();
        if (playerHead == null)
        {
            return;
        }

        Vector3 panelForward = transform.position - playerHead.position;

        if (keepUpright)
        {
            panelForward = Vector3.ProjectOnPlane(panelForward, Vector3.up);
        }

        if (panelForward.sqrMagnitude < 0.000001f)
        {
            return;
        }

        if (!canvasFrontIsNegativeZ)
        {
            panelForward = -panelForward;
        }

        Vector3 up = keepUpright ? Vector3.up : playerHead.up;
        transform.rotation = Quaternion.LookRotation(panelForward.normalized, up);
    }
}
