using UnityEngine;
using UnityEngine.InputSystem;

public class VRExtraControls : MonoBehaviour
{
    public GameObject uiCanvas;
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
        // ★ここが新仕様！★
        // UIが表示されている間は、右スティックの上下移動をキャンセルして誤爆を防ぐ
        if (uiCanvas != null && uiCanvas.activeSelf)
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