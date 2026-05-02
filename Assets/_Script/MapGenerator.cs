using UnityEngine;
using UnityEngine.Tilemaps; // 타일맵 API 사용
using Unity.Netcode; // NGO 멀티플레이 필수 코어

public class MapGenerator : NetworkBehaviour
{
    [Header("맵 규격 (Map Settings)")]
    [SerializeField] private int mapWidth = 50;
    [SerializeField] private int mapHeight = 50;
    [SerializeField] private float noiseScale = 0.1f; // 돋보기 배율 (작을수록 굴곡이 커짐)

    [Header("타일 및 도화지 연결")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private TileBase tileGround; // 이동 가능한 바닥
    [SerializeField] private TileBase tileWall;   // 막힌 벽 (기계 잔해 등)

    // [핵심 부품] 시드 번호를 담을 네트워크 전용 변수 (초기값 0)
    // 방장(Server)만 값을 바꿀 수 있고, 손님(Client)들은 값을 읽기만 가능함
    private NetworkVariable<int> mapSeed = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // 1. 내가 방장(Host/Server)이라면, 새로운 시드 번호를 랜덤으로 하나 뽑는다!
        if (IsServer)
        {
            mapSeed.Value = Random.Range(-100000, 100000);
        }

        // 2. 방장이 시드 값을 바꿨을 때, 모든 사람의 PC에서 이 함수가 자동으로 실행됨 (구독)
        mapSeed.OnValueChanged += (int previousValue, int newValue) =>
        {
            GenerateMap(newValue);
        };

        // 3. 만약 늦게 접속한 손님이라서 이미 시드 값이 0이 아니라면, 즉시 맵을 찍어냄
        if (mapSeed.Value != 0)
        {
            GenerateMap(mapSeed.Value);
        }
    }

    // 실제 맵을 도화지에 그리는 함수
    private void GenerateMap(int currentSeed)
    {
        // 도화지 싹 지우기 (초기화)
        floorTilemap.ClearAllTiles();

        // 시드 번호를 기반으로 난수 발생기(PRNG) 세팅
        System.Random prng = new System.Random(currentSeed);
        float offsetX = prng.Next(-10000, 10000);
        float offsetY = prng.Next(-10000, 10000);

        // 정해진 가로/세로 크기만큼 반복하며 타일을 깜
        for (int x = -mapWidth / 2; x < mapWidth / 2; x++)
        {
            for (int y = -mapHeight / 2; y < mapHeight / 2; y++)
            {
                // 펄린 노이즈 추출 (0.0 ~ 1.0 사이의 굴곡진 수학 값)
                float sampleX = x * noiseScale + offsetX;
                float sampleY = y * noiseScale + offsetY;
                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);

                Vector3Int tilePosition = new Vector3Int(x, y, 0);

                // 노이즈 값이 0.3 미만이면 장해물(벽), 이상이면 바닥(이동 가능)으로 판정
                if (perlinValue < 0.3f)
                {
                    floorTilemap.SetTile(tilePosition, tileWall);
                }
                else
                {
                    floorTilemap.SetTile(tilePosition, tileGround);
                }
            }
        }

        Debug.Log($"[시스템] 월드 생성 완료! 현재 시드 번호: {currentSeed}");
    }
}