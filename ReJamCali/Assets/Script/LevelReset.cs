using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelReset : MonoBehaviour
{
    [Tooltip("Transición con la animación de cambio de escena. Si está vacía, se reinicia directamente.")]
    public SceneTransition transition;

    InputAction reset;

    void Awake()
    {
        reset = new InputAction("Reset", InputActionType.Button, "<Keyboard>/r");
        if (transition == null) transition = FindFirstObjectByType<SceneTransition>();
    }

    void OnEnable() { reset.Enable(); Box.Stuck += OnBoxStuck; }
    void OnDisable() { reset.Disable(); Box.Stuck -= OnBoxStuck; }
    void OnDestroy() { reset.Dispose(); }

    void Update()
    {
        if (reset.WasPressedThisFrame())
            RestartLevel();
    }

    public void RestartLevel()
    {
        Scene current = SceneManager.GetActiveScene();

        if (transition != null)
            transition.StartTransition(current.name);   // anima y recarga la escena actual
        else
            SceneManager.LoadScene(current.buildIndex);  // sin animación
    }

    void OnBoxStuck(Box b)
    {
        // Aquí irá el aviso en pantalla ("Presiona R para reiniciar") cuando hagamos la interfaz.
    }
}