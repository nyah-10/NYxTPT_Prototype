#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupGameScene
{
    private const string RuntimeScriptsFolder = "Assets/Scripts/HexGame";
    private const string GridScriptPath = RuntimeScriptsFolder + "/HexGridManager.cs";
    private const string PlayerScriptPath = RuntimeScriptsFolder + "/PlayerController.cs";

    private const string GeneratedFolder = "Assets/GeneratedHexGame";
    private const string TileTexturePath = GeneratedFolder + "/hex_tile.png";
    private const string TilePrefabPath = GeneratedFolder + "/HexTile.prefab";

    private const string PendingSetupKey = "HexGame.SetupAfterCompilation";

    [InitializeOnLoadMethod]
    private static void ResumeAfterCompilation()
    {
        if (!SessionState.GetBool(PendingSetupKey, false))
            return;

        SessionState.EraseBool(PendingSetupKey);

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling)
                return;

            CreateSceneObjects();
        };
    }

    [MenuItem("Tools/Hex Roguelike/Setup Complete Game Scene")]
    public static void SetupCompleteGameScene()
    {
        bool createdNewScripts = CreateRuntimeScriptsIfNeeded();

        // 새 C# 파일이 생성되면 Unity가 도메인 리로드/컴파일을 수행합니다.
        // 컴파일이 끝나면 InitializeOnLoadMethod가 이어서 씬을 만듭니다.
        if (createdNewScripts)
        {
            SessionState.SetBool(PendingSetupKey, true);
            AssetDatabase.Refresh();

            Debug.Log(
                "Hex 런타임 스크립트를 생성했습니다. " +
                "Unity 컴파일 완료 후 씬 구성이 자동으로 계속됩니다."
            );
            return;
        }

        CreateSceneObjects();
    }

    private static bool CreateRuntimeScriptsIfNeeded()
    {
        CreateFolderIfNeeded("Assets", "Scripts");
        CreateFolderIfNeeded("Assets/Scripts", "HexGame");

        bool created = false;

        if (!File.Exists(GridScriptPath))
        {
            File.WriteAllText(GridScriptPath, HexGridManagerSource);
            created = true;
        }

        if (!File.Exists(PlayerScriptPath))
        {
            File.WriteAllText(PlayerScriptPath, PlayerControllerSource);
            created = true;
        }

        return created;
    }

    private static void CreateSceneObjects()
    {
        Type gridType = FindType("HexGridManager");
        Type playerType = FindType("PlayerController");
        Type enemyType = FindType("EnemyController");
        Type turnManagerType = FindType("TurnManager");

        if (gridType == null || playerType == null || enemyType == null || turnManagerType == null)
        {
            Debug.LogError(
                "HexGridManager 또는 PlayerController를 찾을 수 없습니다. " +
                "Console의 컴파일 오류를 먼저 해결한 뒤 메뉴를 다시 실행하세요."
            );
            return;
        }

        CreateFolderIfNeeded("Assets", "GeneratedHexGame");

        GameObject tilePrefab = CreateOrLoadHexTilePrefab();

        GameObject oldRoot = GameObject.Find("Hex Roguelike Auto Setup");
        if (oldRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(oldRoot);
        }

        GameObject gameRoot = new GameObject("Hex Roguelike Auto Setup");

        GameObject tilesRoot = new GameObject("Tiles");
        tilesRoot.transform.SetParent(gameRoot.transform);

        GameObject gridObject = new GameObject("HexGridManager");
        gridObject.transform.SetParent(gameRoot.transform);

        Component gridManager = gridObject.AddComponent(gridType);
        SetProperty(gridManager, "width", 8);
        SetProperty(gridManager, "height", 6);
        SetProperty(gridManager, "hexRadius", 0.5f);
        SetProperty(gridManager, "hexTilePrefab", tilePrefab);
        SetProperty(gridManager, "tileParent", tilesRoot.transform);

        InvokeMethod(gridManager, "GenerateGrid");

        GameObject turnManagerObject = new GameObject("TurnManager");
        turnManagerObject.transform.SetParent(gameRoot.transform);
        Component turnManager = turnManagerObject.AddComponent(turnManagerType);

        GameObject playerObject = new GameObject("Player");
        playerObject.transform.SetParent(gameRoot.transform);

        SpriteRenderer playerRenderer = playerObject.AddComponent<SpriteRenderer>();
        playerRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileTexturePath);
        playerRenderer.color = new Color(0.2f, 0.65f, 1f, 1f);
        playerRenderer.sortingOrder = 10;

        // 플레이어가 타일보다 작게 보이도록 축소
        playerObject.transform.localScale = Vector3.one * 0.45f;

        Component playerController = playerObject.AddComponent(playerType);
        SetProperty(playerController, "gridManager", gridManager);
        SetProperty(playerController, "startCoordinate", Vector2Int.zero);
        SetProperty(playerController, "moveDuration", 0.2f);

        UnitStats playerStats = playerObject.AddComponent<UnitStats>();
        playerObject.AddComponent<UnitHealthBar>();
        playerObject.AddComponent<ActionController>();
        SkillLoadout playerSkills = playerObject.AddComponent<SkillLoadout>();
        SetProperty(playerSkills, "mainSkills", new[] { SkillExampleAssets.GetDefaultMainSkill(), AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/Examples/ArcaneBolt.asset") });
        SetProperty(playerSkills, "subSkills", new[] { SkillExampleAssets.GetDefaultSubSkill(), AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/Examples/Leap.asset") });

        GameObject enemyObject = new GameObject("Enemy");
        enemyObject.transform.SetParent(gameRoot.transform);

        SpriteRenderer enemyRenderer = enemyObject.AddComponent<SpriteRenderer>();
        enemyRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileTexturePath);
        enemyRenderer.color = new Color(0.9f, 0.2f, 0.25f, 1f);
        enemyRenderer.sortingOrder = 10;
        enemyObject.transform.localScale = Vector3.one * 0.45f;

        Component enemyController = enemyObject.AddComponent(enemyType);
        SetProperty(enemyController, "gridManager", gridManager);
        SetProperty(enemyController, "startCoordinate", new Vector2Int(7, 5));
        SetProperty(enemyController, "moveDuration", 0.25f);

        UnitStats enemyStats = enemyObject.AddComponent<UnitStats>();
        enemyObject.AddComponent<UnitHealthBar>();

        SetProperty(turnManager, "player", playerController);
        SetProperty(turnManager, "enemy", enemyController);

        SkillActionUI skillUi = gameRoot.AddComponent<SkillActionUI>();
        SetProperty(skillUi, "playerSkills", playerSkills);
        SetProperty(skillUi, "gridManager", gridManager);
        SetProperty(skillUi, "playerStats", playerStats);
        SetProperty(skillUi, "enemyStats", enemyStats);

        SetupCamera();

        Selection.activeGameObject = gameRoot;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(
            "완료: Hex Tile 프리팹, 8x6 육각형 그리드, 플레이어, 카메라가 생성되었습니다."
        );
    }

    private static GameObject CreateOrLoadHexTilePrefab()
    {
        CreateHexTextureIfNeeded();

        GameObject existingPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(TilePrefabPath);

        if (existingPrefab != null)
            return existingPrefab;

        Sprite hexSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileTexturePath);

        GameObject tileObject = new GameObject("HexTile");

        SpriteRenderer renderer = tileObject.AddComponent<SpriteRenderer>();
        renderer.sprite = hexSprite;
        renderer.color = new Color(0.75f, 0.83f, 0.92f, 1f);

        PolygonCollider2D collider = tileObject.AddComponent<PolygonCollider2D>();

        const float radius = 0.5f;
        float halfWidth = Mathf.Sqrt(3f) * radius * 0.5f;

        collider.points = new[]
        {
            new Vector2(0f, radius),
            new Vector2(halfWidth, radius * 0.5f),
            new Vector2(halfWidth, -radius * 0.5f),
            new Vector2(0f, -radius),
            new Vector2(-halfWidth, -radius * 0.5f),
            new Vector2(-halfWidth, radius * 0.5f)
        };

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tileObject, TilePrefabPath);
        UnityEngine.Object.DestroyImmediate(tileObject);

        return prefab;
    }

    private static void CreateHexTextureIfNeeded()
    {
        if (File.Exists(TileTexturePath))
            return;

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size - 0.5f) * 2f;
                float ny = ((y + 0.5f) / size - 0.5f) * 2f;

                float absX = Mathf.Abs(nx);
                float absY = Mathf.Abs(ny);

                float maxX = absY <= 0.5f
                    ? Mathf.Sqrt(3f) * 0.5f
                    : Mathf.Sqrt(3f) * (1f - absY);

                texture.SetPixel(
                    x,
                    y,
                    absY <= 1f && absX <= maxX ? white : transparent
                );
            }
        }

        texture.Apply();
        File.WriteAllBytes(TileTexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.Refresh();

        TextureImporter importer =
            AssetImporter.GetAtPath(TileTexturePath) as TextureImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = size;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void SetupCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 4.5f;
        mainCamera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        mainCamera.transform.position = new Vector3(4.1f, 1.9f, -10f);
        mainCamera.transform.rotation = Quaternion.identity;
    }

    private static void SetProperty(Component component, string propertyName, object value)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogError($"{component.GetType().Name}에 {propertyName} 필드가 없습니다.");
            return;
        }

        if (value is int intValue) property.intValue = intValue;
        else if (value is float floatValue) property.floatValue = floatValue;
        else if (value is Vector2Int vectorValue) property.vector2IntValue = vectorValue;
        else if (value is UnityEngine.Object[] objectArray)
        {
            property.arraySize = objectArray.Length;
            for (int i = 0; i < objectArray.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = objectArray[i];
        }
        else if (value is UnityEngine.Object objectValue) property.objectReferenceValue = objectValue;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void InvokeMethod(Component component, string methodName)
    {
        MethodInfo method = component.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance
        );

        method?.Invoke(component, null);
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);

            if (type != null)
                return type;
        }

        return null;
    }

    private static void CreateFolderIfNeeded(string parentFolder, string newFolder)
    {
        string folderPath = parentFolder + "/" + newFolder;

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parentFolder, newFolder);
    }

    private const string HexGridManagerSource = @"
using System.Collections.Generic;
using UnityEngine;

public class HexGridManager : MonoBehaviour
{
    [Header(""Map Size"")]
    [Min(1)] public int width = 8;
    [Min(1)] public int height = 6;

    [Header(""Tile"")]
    public GameObject hexTilePrefab;
    [Min(0.01f)] public float hexRadius = 0.5f;
    public Transform tileParent;

    private readonly Dictionary<Vector2Int, HexTile> tiles = new();

    private void Awake()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        ClearGrid();

        if (hexTilePrefab == null)
        {
            Debug.LogError(""Hex Tile Prefab이 지정되지 않았습니다."");
            return;
        }

        Transform parent = tileParent != null ? tileParent : transform;

        for (int q = 0; q < width; q++)
        {
            for (int r = 0; r < height; r++)
            {
                Vector2Int coordinate = new Vector2Int(q, r);

                GameObject tileObject = Instantiate(
                    hexTilePrefab,
                    AxialToWorld(coordinate),
                    Quaternion.identity,
                    parent
                );

                tileObject.name = $""Hex_{q}_{r}"";

                HexTile tile = tileObject.GetComponent<HexTile>();

                if (tile == null)
                    tile = tileObject.AddComponent<HexTile>();

                tile.Initialize(coordinate);
                tiles[coordinate] = tile;
            }
        }
    }

    // Pointy-top axial 좌표를 월드 좌표로 바꿉니다.
    public Vector3 AxialToWorld(Vector2Int coordinate)
    {
        float x = hexRadius * Mathf.Sqrt(3f) *
                  (coordinate.x + coordinate.y * 0.5f);

        float y = hexRadius * 1.5f * coordinate.y;

        return new Vector3(x, y, 0f);
    }

    public bool TryGetTile(Vector2Int coordinate, out HexTile tile)
    {
        return tiles.TryGetValue(coordinate, out tile);
    }

    private void ClearGrid()
    {
        tiles.Clear();

        Transform parent = tileParent != null ? tileParent : transform;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(parent.GetChild(i).gameObject);
            else
                DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}

public class HexTile : MonoBehaviour
{
    public Vector2Int Coordinate { get; private set; }

    public void Initialize(Vector2Int coordinate)
    {
        Coordinate = coordinate;
    }
}";

    private const string PlayerControllerSource = @"
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerController : MonoBehaviour
{
    public HexGridManager gridManager;
    public Vector2Int startCoordinate = Vector2Int.zero;

    [Min(0.01f)]
    public float moveDuration = 0.2f;

    private Vector2Int currentCoordinate;
    private bool isMoving;

    private static readonly Vector2Int[] NeighborDirections =
    {
        new Vector2Int(0, -1),  // Q: 위-왼쪽
        new Vector2Int(1, -1),  // W: 위-오른쪽
        new Vector2Int(1, 0),   // E: 오른쪽
        new Vector2Int(0, 1),   // D: 아래-오른쪽
        new Vector2Int(-1, 1),  // S: 아래-왼쪽
        new Vector2Int(-1, 0)   // A: 왼쪽
    };

    private void Start()
    {
        if (gridManager == null ||
            !gridManager.TryGetTile(startCoordinate, out HexTile startTile))
        {
            Debug.LogError(""플레이어 시작 타일을 찾지 못했습니다."");
            enabled = false;
            return;
        }

        currentCoordinate = startCoordinate;
        transform.position = startTile.transform.position;
    }

    private void Update()
    {
        if (isMoving)
            return;

        HandleKeyboardInput();
        HandleMouseInput();
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) TryMove(NeighborDirections[0]);
        else if (Input.GetKeyDown(KeyCode.W)) TryMove(NeighborDirections[1]);
        else if (Input.GetKeyDown(KeyCode.E)) TryMove(NeighborDirections[2]);
        else if (Input.GetKeyDown(KeyCode.D)) TryMove(NeighborDirections[3]);
        else if (Input.GetKeyDown(KeyCode.S)) TryMove(NeighborDirections[4]);
        else if (Input.GetKeyDown(KeyCode.A)) TryMove(NeighborDirections[5]);
    }

    private void HandleMouseInput()
    {
        if (!Input.GetMouseButtonDown(0) ||
            (EventSystem.current != null &&
             EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);

        if (hit.collider == null)
            return;

        HexTile clickedTile = hit.collider.GetComponent<HexTile>();

        if (clickedTile == null)
            return;

        Vector2Int difference = clickedTile.Coordinate - currentCoordinate;

        foreach (Vector2Int direction in NeighborDirections)
        {
            if (difference == direction)
            {
                TryMove(direction);
                return;
            }
        }
    }

    private void TryMove(Vector2Int direction)
    {
        Vector2Int targetCoordinate = currentCoordinate + direction;

        if (!gridManager.TryGetTile(targetCoordinate, out HexTile targetTile))
            return;

        StartCoroutine(MoveToTile(targetCoordinate, targetTile.transform.position));
    }

    private IEnumerator MoveToTile(
        Vector2Int targetCoordinate,
        Vector3 targetPosition)
    {
        isMoving = true;

        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / moveDuration);
            progress = Mathf.SmoothStep(0f, 1f, progress);

            transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                progress
            );

            yield return null;
        }

        transform.position = targetPosition;
        currentCoordinate = targetCoordinate;
        isMoving = false;
    }
}";
}
#endif
