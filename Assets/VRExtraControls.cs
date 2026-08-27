using UnityEngine;
using UnityEngine.InputSystem;

public class VRExtraControls : MonoBehaviour
{
    public GameObject uiCanvas;
    public GameObject tutorialCanvas;
    public float verticalSpeed = 3.0f;

    public InputAction toggleUIAction;
    public InputAction rightStickAction;

    void OnEnable()
    {
        toggleUIAction.Enable();
        toggleUIAction.performed += ToggleUI;

        rightStickAction.Enable();
    }

    void OnDisable()
    {
        toggleUIAction.performed -= ToggleUI;
        toggleUIAction.Disable();

        rightStickAction.Disable();
    }

    private void ToggleUI(InputAction.CallbackContext context)
    {
        if (uiCanvas != null)
        {
            uiCanvas.SetActive(!uiCanvas.activeSelf);
        }
    }

    void Update()
    {
        bool isUiOpen = uiCanvas != null && uiCanvas.activeSelf;
        bool isTutorialOpen = tutorialCanvas != null && tutorialCanvas.activeSelf;

        if (isUiOpen || isTutorialOpen)
        {
            return;
        }

        Vector2 stickValue = rightStickAction.ReadValue<Vector2>();
        if (Mathf.Abs(stickValue.y) > 0.1f)
        {
            transform.position += Vector3.up * stickValue.y * verticalSpeed * Time.deltaTime;
        }
    }
}