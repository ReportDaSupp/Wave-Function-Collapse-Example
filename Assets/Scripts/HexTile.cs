using UnityEngine;

public enum TerrainType {
    Grass,
    Water,
    Mountain,
    Forest,
    Desert
}

public class HexTile : MonoBehaviour {
    public Vector2Int axialCoord;
    public TerrainType terrainType;
}