using UnityEngine;

public class HabilidadesNivel : MonoBehaviour
{
    public bool imantar = false;
    public bool canon = false;
    public bool remoto = false;

    static HabilidadesNivel actual;
    void Awake() { actual = this; }
    void OnDestroy() { if (actual == this) actual = null; }

    // Si una escena no tiene el script, todo queda permitido (útil para pruebas).
    public static bool Imantar => actual == null || actual.imantar;
    public static bool Canon => actual == null || actual.canon;
    public static bool Remoto => actual == null || actual.remoto;
}