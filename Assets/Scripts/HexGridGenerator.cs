using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[System.Serializable]
public class TerrainPrefabOptions {
    public TerrainType terrainType;
    public List<GameObject> prefabs;
}

public class HexGridGenerator : MonoBehaviour {
    [Header("Grid Settings")]
    public GameObject hexTilePrefab;
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float hexSize = 1f;
    
    public float waitTime = 0.05f;

    [Header("Terrain Prefab Options")]
    public List<TerrainPrefabOptions> terrainPrefabOptions;

    // Dictionary for fast lookup of prefab lists by TerrainType
    private Dictionary<TerrainType, List<GameObject>> terrainPrefabDict;
    // List of all hex tiles
    private List<HexTile> hexTiles;
    // Dictionary for neighbor lookup
    private Dictionary<Vector2Int, HexTile> hexTileDict;

    void Start() {
        BuildTerrainPrefabDictionary();
        GenerateGrid(); // Create the grid all at once.
        // Start the WFC algorithm as a coroutine so that each collapse is delayed.
        StartCoroutine(ApplyWaveFunctionCollapseCoroutine());
    }

    /// <summary>
    /// Build a lookup dictionary from TerrainType to prefab options.
    /// </summary>
    void BuildTerrainPrefabDictionary() {
        terrainPrefabDict = new Dictionary<TerrainType, List<GameObject>>();
        foreach (TerrainPrefabOptions options in terrainPrefabOptions) {
            if (!terrainPrefabDict.ContainsKey(options.terrainType)) {
                terrainPrefabDict.Add(options.terrainType, options.prefabs);
            }
        }
    }

    /// <summary>
    /// Synchronously generate the hex grid.
    /// </summary>
    void GenerateGrid() {
        hexTiles = new List<HexTile>();

        // Loop over axial coordinates for a flat-topped hex grid.
        for (int q = -gridWidth; q <= gridWidth; q++) {
            int r1 = Mathf.Max(-gridHeight, -q - gridHeight);
            int r2 = Mathf.Min(gridHeight, -q + gridHeight);
            for (int r = r1; r <= r2; r++) {
                Vector3 pos = HexToWorldPosition(q, r);
                GameObject hexGO = Instantiate(hexTilePrefab, pos, Quaternion.identity, transform);
                hexGO.name = "HexParent(" + r + "," + q + ")";
                HexTile tile = hexGO.GetComponent<HexTile>();
                tile.axialCoord = new Vector2Int(q, r);
                hexTiles.Add(tile);
            }
        }
        // Build a dictionary for neighbor lookup.
        hexTileDict = hexTiles.ToDictionary(tile => tile.axialCoord);
    }

    /// <summary>
    /// Converts axial coordinates to a world position for a flat-topped grid.
    /// </summary>
    Vector3 HexToWorldPosition(int q, int r) {
        float x = hexSize * 1.5f * q;
        float z = hexSize * Mathf.Sqrt(3f) * (r + q / 2f);
        return new Vector3(x, 0, z);
    }

    /// <summary>
    /// A coroutine that performs the Wave Function Collapse algorithm with a delay before each tile collapse.
    /// </summary>
    IEnumerator ApplyWaveFunctionCollapseCoroutine() {
        // Initialize possibilities for each tile (all terrain types available)
        Dictionary<HexTile, List<TerrainType>> possibilities = new Dictionary<HexTile, List<TerrainType>>();
        foreach (HexTile tile in hexTiles) {
            possibilities[tile] = Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>().ToList();
        }
        
        // Continue until every tile is collapsed (only one possibility left)
        while (possibilities.Any(kvp => kvp.Value.Count > 1)) {
            // Select the tile with the fewest possibilities (lowest entropy)
            HexTile tileToCollapse = possibilities
                .Where(kvp => kvp.Value.Count > 1)
                .OrderBy(kvp => kvp.Value.Count)
                .First().Key;
            
            // Delay before processing the next tile collapse (0.5 seconds)
            yield return new WaitForSeconds(waitTime);
            
            // Collapse: randomly choose one terrain from the possibilities
            List<TerrainType> options = possibilities[tileToCollapse];
            TerrainType chosenTerrain = options[UnityEngine.Random.Range(0, options.Count)];
            possibilities[tileToCollapse] = new List<TerrainType> { chosenTerrain };
            tileToCollapse.terrainType = chosenTerrain;
            
            // Update the tile’s visual with a rising animation to its terrain-specific height.
            UpdateTileVisual(tileToCollapse);
            
            // Propagate constraints to neighboring tiles (with a spread over frames)
            yield return StartCoroutine(PropagateCoroutine(tileToCollapse, possibilities));
        }
    }

    /// <summary>
    /// Propagates constraints to neighboring tiles.
    /// </summary>
    IEnumerator PropagateCoroutine(HexTile tile, Dictionary<HexTile, List<TerrainType>> possibilities) {
        Queue<HexTile> queue = new Queue<HexTile>();
        queue.Enqueue(tile);
        
        while (queue.Count > 0) {
            HexTile current = queue.Dequeue();
            foreach (HexTile neighbor in GetNeighbors(current)) {
                // Skip if the neighbor is already collapsed.
                if (possibilities[neighbor].Count == 1)
                    continue;
                    
                int beforeCount = possibilities[neighbor].Count;
                // Get allowed terrain types for the neighbor based on current tile.
                List<TerrainType> allowedForNeighbor = GetAllowedNeighborTerrains(current.terrainType);
                possibilities[neighbor] = possibilities[neighbor].Where(t => allowedForNeighbor.Contains(t)).ToList();
                
                if (possibilities[neighbor].Count == 0) {
                    Debug.LogError("Contradiction encountered! Adjust your constraints or restart generation.");
                    yield break;
                }
                
                if (possibilities[neighbor].Count < beforeCount) {
                    queue.Enqueue(neighbor);
                }
            }
            // Spread propagation over multiple frames.
            yield return null;
        }
    }

    /// <summary>
    /// Returns a list of neighboring tiles for a given tile.
    /// </summary>
    List<HexTile> GetNeighbors(HexTile tile) {
        List<HexTile> neighbors = new List<HexTile>();
        // Axial directions for hex grids.
        Vector2Int[] directions = new Vector2Int[] {
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1)
        };
        foreach (Vector2Int dir in directions) {
            Vector2Int neighborCoord = tile.axialCoord + dir;
            if (hexTileDict.ContainsKey(neighborCoord))
                neighbors.Add(hexTileDict[neighborCoord]);
        }
        return neighbors;
    }

    /// <summary>
    /// Define which terrain types are allowed to be adjacent to a given terrain.
    /// </summary>
    List<TerrainType> GetAllowedNeighborTerrains(TerrainType terrain) {
        switch (terrain) {
            case TerrainType.Grass:
                return new List<TerrainType> { TerrainType.Grass, TerrainType.Forest, TerrainType.Desert, TerrainType.Water };
            case TerrainType.Water:
                return new List<TerrainType> { TerrainType.Water, TerrainType.Grass, TerrainType.Desert};
            case TerrainType.Mountain:
                return new List<TerrainType> { TerrainType.Mountain, TerrainType.Forest, TerrainType.Desert };
            case TerrainType.Forest:
                return new List<TerrainType> { TerrainType.Forest, TerrainType.Grass, TerrainType.Mountain, TerrainType.Desert };
            case TerrainType.Desert:
                return new List<TerrainType> { TerrainType.Desert, TerrainType.Grass, TerrainType.Water, TerrainType.Forest, TerrainType.Mountain };
            default:
                return Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>().ToList();
        }
    }

    /// <summary>
    /// Returns the final height (local Y position) for a given terrain type.
    /// </summary>
    float GetTerrainHeight(TerrainType terrain) {
        switch (terrain) {
            case TerrainType.Water:
                return 0f;
            case TerrainType.Grass:
                return 1f;
            case TerrainType.Forest:
                return 2f;
            case TerrainType.Mountain:
                return 2f;
            case TerrainType.Desert:
                return 1f;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Instantiates the appropriate terrain visual as a child of the tile and animates it rising to a height based on its terrain type.
    /// </summary>
    void UpdateTileVisual(HexTile tile) {
        // Remove any existing visual children.
        for (int i = tile.transform.childCount - 1; i >= 0; i--) {
            Destroy(tile.transform.GetChild(i).gameObject);
        }
        
        if (terrainPrefabDict.TryGetValue(tile.terrainType, out List<GameObject> prefabList)) {
            if (prefabList != null && prefabList.Count > 0) {
                // Randomly choose one prefab from the options.
                GameObject chosenPrefab = prefabList[UnityEngine.Random.Range(0, prefabList.Count)];
                // Instantiate the prefab as a child of the tile.
                GameObject visual = Instantiate(chosenPrefab, tile.transform);
                // Set its local starting position to be 5 units below the target.
                visual.transform.localPosition = new Vector3(0, -5f, 0);
                // Determine the final local Y position based on the terrain type.
                float finalHeight = GetTerrainHeight(tile.terrainType);
                Vector3 targetLocalPos = new Vector3(0, finalHeight, 0);
                // Animate the visual rising to its target position over 1 second.
                StartCoroutine(AnimateTileRising(visual.transform, targetLocalPos, 1.0f));
            }
        } else {
            Debug.LogWarning($"No prefab options found for terrain type {tile.terrainType}");
        }
    }

    /// <summary>
    /// Animates a transform’s local position from its current value to a target value over the specified duration.
    /// </summary>
    IEnumerator AnimateTileRising(Transform visualTransform, Vector3 targetLocalPos, float duration) {
        Vector3 startPos = visualTransform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration) {
            visualTransform.localPosition = Vector3.Lerp(startPos, targetLocalPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        visualTransform.localPosition = targetLocalPos;
    }
}
