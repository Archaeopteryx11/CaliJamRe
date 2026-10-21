using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelReset : MonoBehaviour
{
    InputAction reset;

    void Awake() { reset = new InputAction("Reset", InputActionType.Button, "<Keyboard>/r"); }
    void OnEnable() { reset.Enable(); Box.Stuck += OnBoxStuck; }
    void OnDisable() { reset.Disable(); Box.Stuck -= OnBoxStuck; }
    void OnDestroy() { reset.Dispose(); }

    void Update()
    {
        if (reset.WasPressedThisFrame())
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnBoxStuck(Box b)
    {
        // Aquí irá el aviso en pantalla ("Presiona R para reiniciar") cuando hagamos la interfaz.
    }
}