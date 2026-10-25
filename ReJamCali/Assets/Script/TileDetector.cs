using UnityEngine;

public class TileDetector : MonoBehaviour
{
    public enum TileDetectorType
    {
        Left, Right, Forward, Backward, Floor,
        WallLeft, WallRight, WallForward, WallBackward   // NUEVO: a la altura del cuerpo
    }

    public TileDetectorType detectorType;
    PlayerMovement player;

    void Start()
    {
        player = transform.parent.parent.GetComponent<PlayerMovement>();
    }

    void OnTriggerStay(Collider other)
    {
        Tile tile = other.GetComponent<Tile>();
        if (tile != null) Assign(tile);
    }

    void OnTriggerExit(Collider other)
    {
        Tile tile = other.GetComponent<Tile>();
        if (tile != null) Assign(null);
    }

    void Assign(Tile tile)
    {
        switch (detectorType)
        {
            case TileDetectorType.Left: player.SetLeftTile(tile); break;
            case TileDetectorType.Right: player.SetRightTile(tile); break;
            case TileDetectorType.Forward: player.SetForwardTile(tile); break;
            case TileDetectorType.Backward: player.SetBackwardTile(tile); break;
            case TileDetectorType.Floor: player.SetCurrentTile(tile); break;

            case TileDetectorType.WallLeft: player.SetWallLeftTile(tile); break;
            case TileDetectorType.WallRight: player.SetWallRightTile(tile); break;
            case TileDetectorType.WallForward: player.SetWallForwardTile(tile); break;
            case TileDetectorType.WallBackward: player.SetWallBackwardTile(tile); break;
        }
    }
}