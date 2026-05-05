using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Netcode;

public class MapGenerator : NetworkBehaviour
{
    [Header("맵 규격 (Map Settings)")]
    [SerializeField] private int mapWidth = 50;
    [SerializeField] private int mapHeight = 50;
    [SerializeField] private float noiseScale = 0.1f;

    [Header("3중 도화지 연결 (Layer 0, 1, 2)")]
    [SerializeField] private Tilemap voidTilemap;   // (추가됨) 심연 전용 도화지 (투명 벽 충돌체 포함)
    [SerializeField] private Tilemap groundTilemap; // 바닥 전용 도화지 
    [SerializeField] private Tilemap wallTilemap;   // 벽 전용 도화지 

    [SerializeField] private TileBase tileVoid;   // (추가됨) 구덩이 판정용 투명 타일
    [SerializeField] private TileBase tileGround; // 이동 가능한 바닥
    [SerializeField] private TileBase tileWall;   // 막힌 벽 (기계 잔해 등)

    private NetworkVariable<int> mapSeed = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer) mapSeed.Value = Random.Range(-100000, 100000);

        mapSeed.OnValueChanged += (int previousValue, int newValue) => GenerateMap(newValue);

        if (mapSeed.Value != 0) GenerateMap(mapSeed.Value);
    }

    private void GenerateMap(int currentSeed)
    {
        // 3장 도화지 초기화
        voidTilemap.ClearAllTiles();
        groundTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();

        System.Random prng = new System.Random(currentSeed);
        float offsetX = prng.Next(-10000, 10000);
        float offsetY = prng.Next(-10000, 10000);

        for (int x = -mapWidth / 2; x < mapWidth / 2; x++)
        {
            for (int y = -mapHeight / 2; y < mapHeight / 2; y++)
            {
                float sampleX = x * noiseScale + offsetX;
                float sampleY = y * noiseScale + offsetY;
                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);

                Vector3Int tilePosition = new Vector3Int(x, y, 0);

                // [핵심 3단 판별 로직]
                if (perlinValue < 0.2f)
                {
                    // 1. 구덩이 구역: 바닥을 깔지 않고, Void(투명 벽) 타일만 깐다.
                    voidTilemap.SetTile(tilePosition, tileVoid);
                }
                else
                {
                    // 2. 대지 구역: 구덩이가 아니므로 무조건 순수 바닥을 깐다.
                    groundTilemap.SetTile(tilePosition, tileGround);

                    // 3. 장벽 구역: 바닥이 깔린 곳 중에서, 값이 높은 곳에만 벽을 얹는다!
                    if (perlinValue > 0.7f)
                    {
                        wallTilemap.SetTile(tilePosition, tileWall);
                    }
                }
            }
        }

        Debug.Log($"[시스템] 3중 레이어 월드 생성 완료! 현재 시드 번호: {currentSeed}");
    }
}