using UnityEngine;

public class UIListener : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private GameObject panelMagnet;
    public bool triggered;

    void Start()
    {
        triggered = false;
    }
    // Update is called once per frame
    void Update()
    {
        if (!triggered && player.magneticMode)
        {
            panelMagnet.SetActive(false);
        }
    }
}
