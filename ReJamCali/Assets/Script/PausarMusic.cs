using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Video;

public class PausarMusica : MonoBehaviour
{
    public VideoPlayer video;
    public PlayableDirector timeline;
    bool pausadoPorMi;

    void OnEnable()
    {
        if (video != null) { video.started += OnVideoInicio; video.loopPointReached += OnVideoFin; }
        if (timeline != null) { timeline.played += OnTimelineInicio; timeline.stopped += OnTimelineFin; }
    }

    void OnDisable()
    {
        if (video != null) { video.started -= OnVideoInicio; video.loopPointReached -= OnVideoFin; }
        if (timeline != null) { timeline.played -= OnTimelineInicio; timeline.stopped -= OnTimelineFin; }
        Reanudar();   // si la cinemática se salta o se destruye, la música vuelve igual
    }

    void OnVideoInicio(VideoPlayer v) { Pausar(); }
    void OnVideoFin(VideoPlayer v) { Reanudar(); }
    void OnTimelineInicio(PlayableDirector d) { Pausar(); }
    void OnTimelineFin(PlayableDirector d) { Reanudar(); }

    void Pausar()
    {
        AudioManager.PausarMusica();
        pausadoPorMi = true;
    }

    void Reanudar()
    {
        if (!pausadoPorMi) return;
        pausadoPorMi = false;
        AudioManager.ReanudarMusica();
    }
}