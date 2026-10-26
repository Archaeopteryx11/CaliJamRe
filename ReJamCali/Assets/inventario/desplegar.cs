using UnityEngine;
using UnityEngine.EventSystems;

public class desplegar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Expansión")]
    public bool expand = true;
    public float collapsedHeight = 100f;
    public float expandedHeight = 200f;

    [Header("Deslizamiento")]
    public bool slideLeft = true;
    public float slideDistance = 60f;   // píxeles hacia la izquierda

    [Header("Animación")]
    public float speed = 800f;          // píxeles por segundo (altura)
    public float slideSpeed = 600f;     // píxeles por segundo (posición)

    [Header("Contenido extra (opcional)")]
    public GameObject details;

    RectTransform rt;
    float targetHeight;
    float restX;
    float targetX;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        restX = rt.anchoredPosition.x;
        targetX = restX;
        targetHeight = collapsedHeight;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, collapsedHeight);
        if (details) details.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (expand) targetHeight = expandedHeight;
        if (slideLeft) targetX = restX - slideDistance;
        if (details) details.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetHeight = collapsedHeight;
        targetX = restX;
        if (details) details.SetActive(false);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        float h = Mathf.MoveTowards(rt.sizeDelta.y, targetHeight, speed * dt);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);

        float x = Mathf.MoveTowards(rt.anchoredPosition.x, targetX, slideSpeed * dt);
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }
}