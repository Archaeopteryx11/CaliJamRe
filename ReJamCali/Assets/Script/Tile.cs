using System;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    /// Distancia entre centros de tiles contiguos. CÁMBIALA si tus tiles no miden 1 unidad.
    public static float TileSize = 1f;

    [Tooltip("Bloqueado = no se puede entrar (puerta cerrada, etc.)")]
    public bool blocked;

    readonly List<GameObject> occupants = new List<GameObject>();
    Vector2Int key;

    public event Action<Tile> OccupancyChanged;

    public bool IsOccupied { get { return occupants.Count > 0; } }

    /// Una casilla es transitable si NINGÚN tile apilado en ella está bloqueado.
    /// Primer tile bloqueado de esta casilla (o null si está libre).
    public Tile BlockingTile
    {
        get
        {
            var cell = CellTiles(key);
            if (cell == null) return blocked ? this : null;
            foreach (var t in cell)
                if (t != null && t.blocked) return t;
            return null;
        }
    }

    public bool IsWalkable { get { return BlockingTile == null; } }

    /// Caja que está encima de este tile (o null).
    public Box BoxOn
    {
        get
        {
            foreach (var o in occupants)
            {
                if (o == null) continue;
                var b = o.GetComponent<Box>();
                if (b != null) return b;
            }
            return null;
        }
    }

    // ---------- Registro por casilla (varios tiles pueden compartir XZ) ----------
    static readonly Dictionary<Vector2Int, List<Tile>> registry = new Dictionary<Vector2Int, List<Tile>>();

    static Vector2Int Key(Vector3 p)
    {
        return new Vector2Int(Mathf.RoundToInt(p.x / TileSize), Mathf.RoundToInt(p.z / TileSize));
    }

    static List<Tile> CellTiles(Vector2Int k)
    {
        List<Tile> l;
        registry.TryGetValue(k, out l);
        return l;
    }

    /// Tile (el más alto) en esa posición del mundo, o null si no hay (borde del mapa / vacío).
    public static Tile At(Vector3 worldPos)
    {
        var k = Key(worldPos);
        var l = CellTiles(k);
        if (l == null) return null;

        Tile best = null;
        for (int i = l.Count - 1; i >= 0; i--)
        {
            if (l[i] == null) { l.RemoveAt(i); continue; }   // tile destruido de una sesión anterior
            if (best == null || l[i].transform.position.y > best.transform.position.y) best = l[i];
        }
        if (best == null) registry.Remove(k);
        return best;
    }

    void Awake()
    {
        key = Key(transform.position);
        var l = CellTiles(key);
        if (l == null) { l = new List<Tile>(); registry[key] = l; }
        if (!l.Contains(this)) l.Add(this);
    }

    void OnDestroy()
    {
        var l = CellTiles(key);
        if (l == null) return;
        l.Remove(this);
        if (l.Count == 0) registry.Remove(key);
    }

    List<Tile> TilesInCell()
    {
        var l = CellTiles(key);
        return l != null ? new List<Tile>(l) : new List<Tile> { this };
    }

    /// Busca un componente en cualquiera de los tiles de esta casilla (por ejemplo, un PressureButton).
    public T GetInCell<T>() where T : Component
    {
        foreach (var t in TilesInCell())
        {
            if (t == null) continue;
            var c = t.GetComponent<T>();
            if (c != null) return c;
        }
        return null;
    }

    // ---------- Ocupación (se aplica a TODOS los tiles de la casilla) ----------
    public void Enter(GameObject who)
    {
        foreach (var t in TilesInCell()) if (t != null) t.AddOccupant(who);
    }

    public void Exit(GameObject who)
    {
        foreach (var t in TilesInCell()) if (t != null) t.RemoveOccupant(who);
    }

    void AddOccupant(GameObject who)
    {
        if (occupants.Contains(who)) return;
        occupants.Add(who);
        if (OccupancyChanged != null) OccupancyChanged(this);
    }

    void RemoveOccupant(GameObject who)
    {
        if (occupants.Remove(who) && OccupancyChanged != null) OccupancyChanged(this);
    }

    public Vector3 GetTilePosition()
    {
        return (Vector3.right * transform.position.x + Vector3.forward * transform.position.z);
    }
}