using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

/**
 * This class is a copy of the 2DGenerator class from the Procedural Generation
 * code. It is designed to it in with the sprites we are using for our game,
 * instead if the 3D objects the generator was designed to use originally. It
 * was also modiffied to fit our design choice of having an open roof plan,
 * looking down on the player.
 */
public class SpriteGenerator2D : MonoBehaviour
{
    public NavmeshBaker baker;
    //Enumeration for the position of a sprite in a room. Set as flags.
    [System.Flags]
    enum SpritePositionType : short
    {
        None = 0,
        Top = 1,
        Bottom = 2,
        Left = 4,
        Right = 8
    }

    enum CellType
    {
        None,
        Room,
        Hallway
    }

    class Room
    {
        public RectInt bounds;

        public Room(Vector2Int location, Vector2Int size)
        {
            bounds = new RectInt(location, size);
        }

        public static bool Intersect(Room a, Room b)
        {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField]
    Vector2Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector2Int roomMaxSize;
    //Created to make a minimum room size.
    [SerializeField]
    Vector2Int roomMinSize = new Vector2Int(1, 1);
    //Modified to hold a selection of different floor tiles.
    [SerializeField]
    GameObject[] floorPrefabs;

    //Created to hold a wall tile.
    [SerializeField]
    GameObject[] wallPrefabs;

    //Created to hold the roof tile.
    [SerializeField]
    GameObject[] roofPrefabs;

    //Test for spawning
    [SerializeField]
    GameObject alpacaPrefab;

    //List of Enemies
    [SerializeField]
    GameObject[] enemyPrefabs;
    //Minimum number of enemies per room
    [SerializeField]
    int minEnemyNumber;

    //List of Wall Props
    [SerializeField]
    GameObject[] wallProps;
    //List of Floor Props;
    [SerializeField]
    GameObject[] floorProps;

    [SerializeField]
    GameObject clossedDoorSprite;

    [SerializeField]
    bool bossFloor;
    [SerializeField]
    GameObject bossPrefab;

    public bool generationFinished = false;

    Random random;
    Grid2D<CellType> grid;
    List<Room> rooms;
    Delaunay2D delaunay;
    HashSet<Prim.Edge> selectedEdges;

    void Start()
    {
        EventHandeler.OnPlayerSpawn += SpawnExitWall;
        Generate();
        generationFinished = true;
    }

    void Generate()
    {
        random = new Random();
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();

        //Modified to allow for the roof creation
        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();
        PlaceRoof();

        //Call player spawn and retrieve spawn room.
        Room playerSpawn = SpawnAlpaca();

        baker.Bake();

        //Call enemy spawn
        SpawnEnemies(playerSpawn);
        //Spawn Floor Props.
        SpawnFloorProps();
    }

    void PlaceRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            Vector2Int location = new Vector2Int(
                random.Next(0, size.x),
                random.Next(0, size.y)
            );

            //Modified to have a minimum room size.
            Vector2Int roomSize = new Vector2Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            foreach (var room in rooms)
            {
                if (Room.Intersect(room, buffer))
                {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y)
            {
                add = false;
            }

            if (add)
            {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                foreach (var pos in newRoom.bounds.allPositionsWithin)
                {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate()
    {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms)
        {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay2D.Triangulate(vertices);
    }

    void CreateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> mst = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(mst);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges)
        {
            if (random.NextDouble() < 0.125)
            {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways()
    {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);

        foreach (var edge in selectedEdges)
        {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) => {
                var pathCost = new DungeonPathfinder2D.PathCost();

                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

                if (grid[b.Position] == CellType.Room)
                {
                    pathCost.cost += 10;
                }
                else if (grid[b.Position] == CellType.None)
                {
                    pathCost.cost += 5;
                }
                else if (grid[b.Position] == CellType.Hallway)
                {
                    pathCost.cost += 1;
                }

                pathCost.traversable = true;

                return pathCost;
            });

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];

                    if (grid[current] == CellType.None)
                    {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0)
                    {
                        var prev = path[i - 1];

                        var delta = current - prev;
                    }
                }

                foreach (var pos in path)
                {
                    if (grid[pos] == CellType.Hallway)
                    {
                        PlaceHallway(pos);
                    }
                }
            }
        }
    }

    /**
     * Placing cubes has been modified into two methods, to fit with our game design. And, to create a roof around the
     * level, a method has been added to create the roof.
     */

    //Modified to work with sprites instead of cubes.
    void PlaceFloorSprite(Vector2Int location, Vector2Int size, string tagName)
    {
        GameObject go = Instantiate(NextSprite(floorPrefabs), FloorSpriteLocationFix(size, location), Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, size.y, 1);
        //Rotate sprite to be flat, then a random 90 degree rotation on the ground.
        go.GetComponent<Transform>().rotation = Quaternion.Euler(90, random.Next(0, 4) * 90, 0);
        go.tag = tagName;
    }

    //Created to place a roof tile.
    void PlaceRoofSprite(Vector2Int location, Vector2Int size)
    {
        Vector3 fixedLoaction = FloorSpriteLocationFix(size, location);
        Vector3 placeAt = new Vector3(fixedLoaction.x, fixedLoaction.y + 1.0f, fixedLoaction.z);

        GameObject go = Instantiate(GetRoofTile(fixedLoaction), placeAt, Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, size.y, 1);
        go.GetComponent<Transform>().rotation = Quaternion.Euler(90, 0, 0);
    }

    //Created to place a wall at given flags, with appropriate positions and rotation.
    void PlaceWallSprite(Vector2Int location, Vector2Int size, SpritePositionType relativePos)
    {
        SpritePositionType[] allWallTypes =
        {
            SpritePositionType.Top,
            SpritePositionType.Right,
            SpritePositionType.Bottom,
            SpritePositionType.Left
        };

        if ((relativePos | SpritePositionType.None) != SpritePositionType.None)
        {
            for (int i = 0; i < allWallTypes.Length; i++)
            {
                GameObject wall = null;
                SpritePositionType currentPos = relativePos & allWallTypes[i];
                int placePropChance = random.Next() % 3;

                if (currentPos == allWallTypes[i])
                {
                    wall = Instantiate(NextSprite(wallPrefabs), WallSpriteLocationFix(size, location, currentPos), Quaternion.identity);
                    wall.GetComponent<Transform>().rotation = Quaternion.Euler(0, 90 * i, 0);
                }
                if (wall != null)
                {
                    wall.GetComponent<Transform>().localScale = new Vector3(size.x, size.y, 1);

                    if (placePropChance == 0)
                    {
                        GameObject prop = Instantiate(NextSprite(wallProps), WallPropLocationFix(size, location, currentPos), Quaternion.identity);
                        prop.GetComponent<Transform>().localScale = new Vector3(size.x, size.y, 1);
                        prop.GetComponent<Transform>().rotation = Quaternion.Euler(0, 90 * i, 0);
                    }
                }
            }
        }
    }

    //Modified to place a sprite on each unit of a room.
    void PlaceRoom(Vector2Int location, Vector2Int size)
    {
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                SpritePositionType spriteRelativePosition = GetSpriteRelativePosition(i, j, size);

                Vector2Int nextSpriteLocation = new Vector2Int(location.x + i, location.y + j);
                PlaceFloorSprite(nextSpriteLocation, new Vector2Int(1, 1), "Room");
                PlaceWallSprite(nextSpriteLocation, new Vector2Int(1, 1), spriteRelativePosition);
            }
        }
    }

    //Method to calculate a sprites position, to understand where walls should be placed.
    SpritePositionType GetSpriteRelativePosition(int xPos, int yPos, Vector2Int size)
    {
        SpritePositionType position = SpritePositionType.None;

        if (xPos == 0)
        {
            position = position | SpritePositionType.Left;
        }
        if (xPos == (size.x - 1))
        {
            position = position | SpritePositionType.Right;
        }
        if (yPos == 0)
        {
            position = position | SpritePositionType.Bottom;
        }
        if (yPos == (size.y - 1))
        {
            position = position | SpritePositionType.Top;
        }

        return position;
    }

    void PlaceHallway(Vector2Int location)
    {
        if (!DetectSprite(FloorSpriteLocationFix(new Vector2Int(1, 1), location)))
        {
        /*
         * Before creating hallways, the program must find out what walls to delete, and learn the hallways relative
         * position, based on the deleted walls.
         */
            Vector3 wallDetection = FloorSpriteLocationFix(new Vector2Int(1, 1), location);
            wallDetection = new Vector3(wallDetection.x, wallDetection.y + 0.7f, wallDetection.z);
            int layer = 1 << LayerMask.NameToLayer("Environment");
            Collider[] wallsFound = Physics.OverlapSphere(wallDetection, 0.6f, layer);

            SpritePositionType hallwayWalls = (
                SpritePositionType.Top |
                SpritePositionType.Bottom |
                SpritePositionType.Left |
                SpritePositionType.Right);

            foreach (Collider wall in wallsFound)
            {
                if (!wall.tag.Equals("Prop"))
                {
                    float wallXPos = wall.gameObject.transform.position.x;
                    float wallZPos = wall.gameObject.transform.position.z;

                    if (wallXPos > wallDetection.x)
                    {
                        hallwayWalls = hallwayWalls & ~SpritePositionType.Right;
                    }
                    if (wallXPos < wallDetection.x)
                    {
                        hallwayWalls = hallwayWalls & ~SpritePositionType.Left;
                    }
                    if (wallZPos > wallDetection.z)
                    {
                        hallwayWalls = hallwayWalls & ~SpritePositionType.Top;
                    }
                    if (wallZPos < wallDetection.z)
                    {
                        hallwayWalls = hallwayWalls & ~SpritePositionType.Bottom;
                    }
                }

                Destroy(wall.gameObject);
            }

            PlaceFloorSprite(location, new Vector2Int(1, 1), "Hallway");
            PlaceWallSprite(location, new Vector2Int(1, 1), hallwayWalls);
        }
    }

    Vector3 FloorSpriteLocationFix(Vector2Int spriteSize, Vector2Int spriteLocation)
    {
        return SpriteLocationFix(spriteSize, spriteLocation, SpritePositionType.None);
    }

    Vector3 WallSpriteLocationFix(Vector2Int spriteSize, Vector2Int spriteLocation, SpritePositionType relativePos)
    {
        return SpriteLocationFix(spriteSize, spriteLocation, relativePos);
    }

    /**
     * Sets prop location to just infront of the wall sprite it is attached to.
     */
    Vector3 WallPropLocationFix(Vector2Int spriteSize, Vector2Int spriteLocation, SpritePositionType relativePos)
    {
        Vector3 propPosition = WallSpriteLocationFix(spriteSize, spriteLocation, relativePos);
        float propXPos = propPosition.x;
        float propZPos = propPosition.z;

        switch (relativePos)
        {
            case SpritePositionType.Left:
                propXPos += 0.01f;
                break;
            case SpritePositionType.Right:
                propXPos -= 0.01f;
                break;
            case SpritePositionType.Top:
                propZPos -= 0.01f;
                break;
            case SpritePositionType.Bottom:
                propZPos += 0.01f;
                break;
        }

        return new Vector3(propXPos, propPosition.y, propZPos);
    }

    /**
     * Changes a sprite position to be placed as if it's center of transformation
     * is on the bottom left side of the sprite, and not the centre of the sprite.
     */
    Vector3 SpriteLocationFix(Vector2Int spriteSize, Vector2Int spriteLocation, SpritePositionType relativePos)
    {
        float spriteLocationX = spriteLocation.x + (spriteSize.x / 2.0f);
        float spriteLocationZ = spriteLocation.y + (spriteSize.y / 2.0f);
        float spriteLocationY = 0.0f;

        if (relativePos != SpritePositionType.None)
        {
            spriteLocationY = 0.5f;

            if (relativePos == SpritePositionType.Left)
            {
                spriteLocationX = spriteLocation.x;
            }
            else if (relativePos == SpritePositionType.Right)
            {
                spriteLocationX = spriteLocation.x + spriteSize.x;
            }

            if (relativePos == SpritePositionType.Top)
            {
                spriteLocationZ = spriteLocation.y + spriteSize.x;
            }
            else if (relativePos == SpritePositionType.Bottom)
            {
                spriteLocationZ = spriteLocation.y;
            }
        }

        return new Vector3(spriteLocationX, spriteLocationY, spriteLocationZ);
    }

    //Method to choose a random sprite from the given sprites entered.
    GameObject NextSprite(GameObject[] sprites)
    {
        return sprites[random.Next(0, sprites.Length)];
    }

    //Used to detect floor tiles.
    bool DetectSprite (Vector3 cornerPosition)
    {
        Vector3 atPosition = new Vector3(cornerPosition.x, cornerPosition.y - 0.1f, cornerPosition.z);

        bool existsAtPosition = false;

        Collider[] foundColliders = Physics.OverlapSphere(atPosition, 0.1f);

        if (foundColliders.Length > 0)
        {
            existsAtPosition = true;
        }

        return existsAtPosition;
    }

    //Used to place roof tiles.
    void PlaceRoof()
    {
        //Used to have roof extend over map range, giving less chance of camera to see outside the roof.
        int roofExtension = 10;

        for (int i = (0 - roofExtension); i < (size.x + roofExtension); i++)
        {
            for (int j = (0 - roofExtension); j < (size.y + roofExtension); j++)
            {
                Vector2Int location = new Vector2Int(i, j);

                if (!DetectSprite(FloorSpriteLocationFix(new Vector2Int(1, 1), location)))
                {
                    PlaceRoofSprite(location, new Vector2Int(1, 1));
                }
            }
        }
    }

    //Used to get the required roof tile, based on the walls closest to the roof tile.
    GameObject GetRoofTile(Vector3 location)
    {
        SpritePositionType wallPositions = SpritePositionType.None;

        Vector3 wallDetector = new Vector3(location.x, location.y + 0.7f, location.z);
        Collider[] wallsFound = Physics.OverlapSphere(wallDetector, 0.6f);

        int arrayLocation;

        foreach (Collider wall in wallsFound)
        {
            float wallXPos = wall.gameObject.transform.position.x;
            float wallZPos = wall.gameObject.transform.position.z;

            if (wallXPos > wallDetector.x)
            {
                wallPositions |= SpritePositionType.Right;
            }
            if (wallXPos < wallDetector.x)
            {
                wallPositions |= SpritePositionType.Left;
            }
            if (wallZPos > wallDetector.z)
            {
                wallPositions |= SpritePositionType.Top;
            }
            if (wallZPos < wallDetector.z)
            {
                wallPositions |= SpritePositionType.Bottom;
            }
        }

        switch (wallPositions)
        {
            case SpritePositionType.Top:
                arrayLocation = 1;
                break;
            case SpritePositionType.Bottom:
                arrayLocation = 2;
                break;
            case SpritePositionType.Left:
                arrayLocation = 3;
                break;
            case SpritePositionType.Right:
                arrayLocation = 4;
                break;
            case SpritePositionType.Top | SpritePositionType.Left:
                arrayLocation = 5;
                break;
            case SpritePositionType.Top | SpritePositionType.Right:
                arrayLocation = 6;
                break;
            case SpritePositionType.Bottom | SpritePositionType.Left:
                arrayLocation = 7;
                break;
            case SpritePositionType.Bottom | SpritePositionType.Right:
                arrayLocation = 8;
                break;
            case SpritePositionType.Top | SpritePositionType.Bottom:
                arrayLocation = 9;
                break;
            case SpritePositionType.Left | SpritePositionType.Right:
                arrayLocation = 10;
                break;
            case SpritePositionType.Top | SpritePositionType.Left | SpritePositionType.Right:
                arrayLocation = 11;
                break;
            case SpritePositionType.Bottom | SpritePositionType.Left | SpritePositionType.Right:
                arrayLocation = 12;
                break;
            case SpritePositionType.Top | SpritePositionType.Bottom | SpritePositionType.Left:
                arrayLocation = 13;
                break;
            case SpritePositionType.Top | SpritePositionType.Bottom | SpritePositionType.Right:
                arrayLocation = 14;
                break;
            case SpritePositionType.Top | SpritePositionType.Bottom | SpritePositionType.Left | SpritePositionType.Right:
                arrayLocation = 15;
                break;
            default:
                arrayLocation = GetCornerRoofTiles(wallDetector);
                break;
        }

        return roofPrefabs[arrayLocation];
    }

    int GetCornerRoofTiles(Vector3 originalDetector)
    {
        SpritePositionType wallPositions = SpritePositionType.None;

        Vector3 extendedDetector = new Vector3(
            originalDetector.x,
            originalDetector.y,
            originalDetector.z);

        Collider[] wallsFound = Physics.OverlapSphere(extendedDetector, 1.0f);

        int arrayLocation;

        foreach (Collider wall in wallsFound)
        {
            float wallXPos = wall.gameObject.transform.position.x;
            float wallZPos = wall.gameObject.transform.position.z;

            if (wallXPos > extendedDetector.x)
            {
                wallPositions |= SpritePositionType.Right;
            }
            if (wallXPos < extendedDetector.x)
            {
                wallPositions |= SpritePositionType.Left;
            }
            if (wallZPos > extendedDetector.z)
            {
                wallPositions |= SpritePositionType.Top;
            }
            if (wallZPos < extendedDetector.z)
            {
                wallPositions |= SpritePositionType.Bottom;
            }
        }

        switch (wallPositions)
        {
            case SpritePositionType.Top | SpritePositionType.Left:
                arrayLocation = 16;
                break;
            case SpritePositionType.Top | SpritePositionType.Right:
                arrayLocation = 17;
                break;
            case SpritePositionType.Bottom | SpritePositionType.Left:
                arrayLocation = 18;
                break;
            case SpritePositionType.Bottom | SpritePositionType.Right:
                arrayLocation = 19;
                break;
            default:
                arrayLocation = 0;
                break;
        }

        return arrayLocation;
    }

    Room SpawnAlpaca()
    {
        Room spawnRoom = rooms.ToArray()[random.Next(0, rooms.Count)];

        Vector2Int spawnRoomEdge = spawnRoom.bounds.position;
        Vector3 spawnAt = new Vector3(
            (float)spawnRoomEdge.x + ((float)spawnRoom.bounds.size.x / 2.0f),
            0.5f,
            (float)spawnRoomEdge.y + ((float)spawnRoom.bounds.size.y / 2.0f));


        GameObject alpaca = Instantiate(alpacaPrefab, spawnAt, Quaternion.identity);
        alpaca.name = "Alpaca";
        alpaca.GetComponent<Transform>().localScale = new Vector3(1.0f, 1.0f, 1.0f);

        return spawnRoom;
    }

    void SpawnEnemies(Room playerSpawn)
    {
        bool bossHasSpawned = false;

        foreach (Room toSpawnIn in rooms)
        {
            if (toSpawnIn != playerSpawn)
            {
                Vector2Int spawnRoomEdge = toSpawnIn.bounds.position;
                int maxEnemyNumber = Mathf.Min(toSpawnIn.bounds.size.x, toSpawnIn.bounds.size.y) - 2;
                int enemiesSpawned = 0;
                int enemiesToSpawn = random.Next(minEnemyNumber, maxEnemyNumber);

                while (enemiesSpawned < enemiesToSpawn)
                {
                    Vector2Int spawnPosition = new Vector2Int(
                    spawnRoomEdge.x + random.Next(0, toSpawnIn.bounds.size.x),
                    spawnRoomEdge.y + random.Next(0, toSpawnIn.bounds.size.y));

                    Vector3 spawnAt = FloorSpriteLocationFix(new Vector2Int(1, 1), spawnPosition);
                    spawnAt = new Vector3(spawnAt.x, 0.5f, spawnAt.z);

                    bool enemyExists = false;
                    Collider[] potentialEnemies = Physics.OverlapSphere(spawnAt, 0.1f);
                    foreach (Collider sprite in potentialEnemies)
                    {
                        if (sprite.tag.Equals("Enemy"))
                        {
                            enemyExists = true;
                        }
                    }

                    if (!enemyExists)
                    {
                        if (bossFloor && !bossHasSpawned)
                        {
                            GameObject enemy = Instantiate(bossPrefab, spawnAt, Quaternion.identity);
                            enemy.GetComponent<Transform>().localScale = new Vector3(1.0f, 1.0f, 1.0f);
                            bossHasSpawned = true;
                        }
                        else
                        {
                            GameObject enemy = Instantiate(enemyPrefabs[random.Next(0, enemyPrefabs.Length)], spawnAt, Quaternion.identity);
                            enemy.GetComponent<Transform>().localScale = new Vector3(1.0f, 1.0f, 1.0f);
                        }

                        enemiesSpawned++;
                    }
                }
            }
        }
    }

    void SpawnFloorProps()
    {
        foreach (Room currentRoom in rooms)
        {
            int propNumber = random.Next(0, 10);
            List<Vector2Int> propLocations = new List<Vector2Int>();

            for (int i = 0; i < propNumber; i++)
            {
                Vector2Int spawnRoomEdge = currentRoom.bounds.position;
                Vector2Int spawnPosition = new Vector2Int(
                    spawnRoomEdge.x + random.Next(0, currentRoom.bounds.size.x),
                    spawnRoomEdge.y + random.Next(0, currentRoom.bounds.size.y));

                if (!propLocations.Contains(spawnPosition))
                {
                    Vector3 spawnAt = FloorSpriteLocationFix(new Vector2Int(1, 1), spawnPosition);
                    spawnAt = new Vector3(spawnAt.x, 0.2f, spawnAt.z);

                    bool enemyExists = false;
                    Collider[] potentialEnemies = Physics.OverlapSphere(spawnAt, 0.1f);
                    foreach (Collider sprite in potentialEnemies)
                    {
                        if (sprite.tag.Equals("Enemy"))
                        {
                            enemyExists = true;
                        }
                    }

                    if (!enemyExists)
                    {
                        GameObject prop = Instantiate(NextSprite(floorProps), spawnAt, Quaternion.identity);
                        prop.GetComponent<Transform>().localScale = new Vector3(1.0f, 1.0f, 1.0f);
                        prop.GetComponent<Transform>().rotation = Quaternion.Euler(40, 0, 0);

                        propLocations.Add(spawnPosition);
                    }
                }
            }
        }
    }

    void SpawnExitWall()
    {
        Room[] availableRooms = rooms.ToArray();

        List<Room> unavailableRooms = new List<Room>();

        bool doorPlaced = false;

        while (!doorPlaced)
        {
            //Get a new room that hasn't been checked yet.
            Room exitRoom = null;
            while (exitRoom == null)
            {
                Room toExit = availableRooms[random.Next(0, availableRooms.Length)];
                if (!unavailableRooms.Contains(toExit))
                {
                    exitRoom = toExit;
                }
            }

            Vector2Int topWallStart = exitRoom.bounds.position;
            topWallStart.y += exitRoom.bounds.size.y - 1;

            List<int> unavailableWalls = new List<int>();
            int allWalls = topWallStart.x + exitRoom.bounds.size.x;
            int topWallNumber = exitRoom.bounds.size.x;

            //Check if all walls have been viewed, or if the door has been placed.
            while ((unavailableWalls.Count < topWallNumber) && (!doorPlaced))
            {
                //Get next wall and check if it has already been tested.
                int nextWall = random.Next(topWallStart.x + 1, allWalls);
                if (!unavailableWalls.Contains(nextWall))
                {
                    Vector2Int doorPosition = new Vector2Int(nextWall, topWallStart.y);
                    Vector3 hallwayDetector = FloorSpriteLocationFix(
                        new Vector2Int(1, 1),
                        new Vector2Int(doorPosition.x, doorPosition.y + 1));

                    Collider[] hallways = Physics.OverlapSphere(hallwayDetector, 0.2f, 1 << LayerMask.NameToLayer("Floor"));

                    if (hallways.Length.Equals(0))
                    {
                        Vector3 placementPosition = WallSpriteLocationFix(new Vector2Int(1, 1), doorPosition, SpritePositionType.Top);

                        //Check if the current placement has a wall to remove.
                        Collider[] currentWall = Physics.OverlapSphere(placementPosition, 0.2f, 1 << LayerMask.NameToLayer("Environment"));
                        if (currentWall.Length != 0)
                        {
                            foreach (Collider environmentElement in currentWall)
                            {
                                Destroy(environmentElement.gameObject);
                            }

                            GameObject exitDoor = Instantiate(clossedDoorSprite, placementPosition, Quaternion.identity);
                            exitDoor.name = "LevelDoor";
                            exitDoor.GetComponent<Transform>().localScale = Vector3.one;

                            doorPlaced = true;
                        }
                        else
                        {
                            unavailableWalls.Add(nextWall);
                        }
                    }
                }
            }

            if (!doorPlaced)
            {
                unavailableRooms.Add(exitRoom);
            }
        }
    }

    private void OnDestroy()
    {
        EventHandeler.OnPlayerSpawn -= SpawnExitWall;
    }
}
