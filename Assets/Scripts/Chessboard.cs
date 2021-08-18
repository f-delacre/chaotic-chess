using System;
using System.Collections.Generic;
using TMPro;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class Chessboard : MonoBehaviour
{
    // ========== VARIABLES ==========

    [Header("Art Stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.5f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.04f;
    [SerializeField] private float deathSpacing = 0.5f;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private Button rematchButton;
    [SerializeField] private GameObject turnIndicator;


    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;
    [SerializeField] private Color[] teamColors;

    private ChessPiece[,] chessPieces;
    private ChessPiece currentlySelected;
    private List<Vector2Int> availableMoves;
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector3 bounds;
    private bool isWhiteTurn;
    private Vector2Int selectedTile = -Vector2Int.one;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private List<ChessPieceType> promotionList = new List<ChessPieceType>();
    private bool isInCheck = false;

    // Multiplayer variables
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    private bool[] playerRematch = new bool[2];
    private bool isPromotion;
    private int random;

    // ========== METHODS ==========

    private void Start()
    {
        isWhiteTurn = true;

        promotionList.Add(ChessPieceType.Rook);
        promotionList.Add(ChessPieceType.Knight);
        promotionList.Add(ChessPieceType.Bishop);
        promotionList.Add(ChessPieceType.Queen);

        GenerateGrid(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        

        RegisterEvents();
    }

    private void Update()
    {
        if(!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;

        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight", "Check")))
        {
            // Get the indexes of the tile hited
            Vector2Int hitPosition = TileIndex(info.transform.gameObject);

            // If we presse down on the mouse
            if (Input.GetMouseButtonDown(0))
            {
                if(currentlySelected == null)
                {
                    if (chessPieces[hitPosition.x, hitPosition.y] != null)
                    {
                        // Is it our turn ?
                        if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1))
                        {
                            currentlySelected = chessPieces[hitPosition.x, hitPosition.y];

                            if(selectedTile == -Vector2Int.one)
                            {
                                selectedTile = new Vector2Int(hitPosition.x, hitPosition.y);
                                tiles[selectedTile.x, selectedTile.y].layer = LayerMask.NameToLayer("Hover");
                            }

                            // Get available moves and highlights tiles
                            availableMoves = currentlySelected.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);

                            // Get a list of special moves
                            specialMove = currentlySelected.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                            PreventCheck();

                            HighLightTiles();
                        }
                    }
                }
                else
                {
                    Vector2Int previousPosition = new Vector2Int(currentlySelected.currentX, currentlySelected.currentY);
                    bool isValidMove = MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                    if(isValidMove)
                    {
                        Vector2Int[] lastMove = moveList[moveList.Count - 1];
                        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 0 : 1;

                        ChangeTileIfCheck(targetTeam, "Tile");

                        // Net implementation
                        NetMakeMove mm = new NetMakeMove();
                        mm.originalX = previousPosition.x;
                        mm.originalY = previousPosition.y;
                        mm.destinationX = hitPosition.x;
                        mm.destinationY = hitPosition.y;
                        mm.teamId = currentTeam;
                        mm.isPromotion = ((byte)(isPromotion ? 1 : 0));
                        mm.randomPromotionPiece = 0;

                        if(isPromotion && !localGame)
                        {
                            mm.randomPromotionPiece = random;
                        }

                        Client.Instance.SendToServer(mm);
                    }

                    if(!isValidMove)
                    {
                        currentlySelected.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                    }

                    
                    if (selectedTile != -Vector2Int.one)
                    {
                        tiles[selectedTile.x, selectedTile.y].layer = LayerMask.NameToLayer("Tile");
                        selectedTile = -Vector2Int.one;

                        if (!isValidMove && isInCheck)
                        {
                            Vector2Int[] lastMove = moveList[moveList.Count - 1];
                            int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

                            ChangeTileIfCheck(targetTeam, "Check");
                        }

                    }
                    
                    currentlySelected = null;
                    RemoveHighLightTiles();
                }
            }
        }
        else
        {
            if (currentlySelected && Input.GetMouseButtonDown(0))
            {
                currentlySelected = null;
                RemoveHighLightTiles();
            }

            if (Input.GetMouseButtonDown(0) && selectedTile != -Vector2Int.one)
            {
                tiles[selectedTile.x, selectedTile.y].layer = LayerMask.NameToLayer("Tile");
                selectedTile = -Vector2Int.one;

                if (isInCheck)
                {
                    Vector2Int[] lastMove = moveList[moveList.Count - 1];
                    int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

                    ChangeTileIfCheck(targetTeam, "Check");
                }
            }
        }
    }

    #region Board
    private void GenerateGrid(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;
        tiles = new GameObject[tileCountX, tileCountY];

        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateTile(tileSize, x, y);
            }
        }
    }

    private GameObject GenerateTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;
        vertices[2] = new Vector3((x+1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y+1) * tileSize) - bounds;

        int[] triangles = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }
    #endregion

    #region Pieces spawn
    private void SpawnAllPieces(List<int> allPieces)
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0;
        int blackTeam = 1;

        List<ChessPieceType> cptList = new List<ChessPieceType>();
        cptList.Add(ChessPieceType.Rook);
        cptList.Add(ChessPieceType.Knight);
        cptList.Add(ChessPieceType.Bishop);
        cptList.Add(ChessPieceType.Queen);
        cptList.Add(ChessPieceType.King);
        cptList.Add(ChessPieceType.Pawn);

        // White team
        
        chessPieces[0, 0] = SpawnPiece(cptList[allPieces[0]], whiteTeam);
        chessPieces[1, 0] = SpawnPiece(cptList[allPieces[1]], whiteTeam);
        chessPieces[2, 0] = SpawnPiece(cptList[allPieces[2]], whiteTeam);
        chessPieces[3, 0] = SpawnPiece(cptList[allPieces[3]], whiteTeam);
        chessPieces[4, 0] = SpawnPiece(cptList[allPieces[4]], whiteTeam);
        chessPieces[5, 0] = SpawnPiece(cptList[allPieces[5]], whiteTeam);
        chessPieces[6, 0] = SpawnPiece(cptList[allPieces[6]], whiteTeam);
        chessPieces[7, 0] = SpawnPiece(cptList[allPieces[7]], whiteTeam);
        chessPieces[0, 1] = SpawnPiece(cptList[allPieces[8]], whiteTeam);
        chessPieces[1, 1] = SpawnPiece(cptList[allPieces[9]], whiteTeam);
        chessPieces[2, 1] = SpawnPiece(cptList[allPieces[10]], whiteTeam);
        chessPieces[3, 1] = SpawnPiece(cptList[allPieces[11]], whiteTeam);
        chessPieces[4, 1] = SpawnPiece(cptList[allPieces[12]], whiteTeam);
        chessPieces[5, 1] = SpawnPiece(cptList[allPieces[13]], whiteTeam);
        chessPieces[6, 1] = SpawnPiece(cptList[allPieces[14]], whiteTeam);
        chessPieces[7, 1] = SpawnPiece(cptList[allPieces[15]], whiteTeam);
        
        /*
        for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 1] = SpawnPiece(ChessPieceType.Pawn, whiteTeam);
        }
        */
        // Black team
        /*
        chessPieces[0, 7] = SpawnPiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnPiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnPiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnPiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnPiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnPiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnPiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnPiece(ChessPieceType.Rook, blackTeam);
        */


        chessPieces[0, 7] = SpawnPiece(cptList[allPieces[16]], blackTeam);
        chessPieces[1, 7] = SpawnPiece(cptList[allPieces[17]], blackTeam);
        chessPieces[2, 7] = SpawnPiece(cptList[allPieces[18]], blackTeam);
        chessPieces[3, 7] = SpawnPiece(cptList[allPieces[19]], blackTeam);
        chessPieces[4, 7] = SpawnPiece(cptList[allPieces[20]], blackTeam);
        chessPieces[5, 7] = SpawnPiece(cptList[allPieces[21]], blackTeam);
        chessPieces[6, 7] = SpawnPiece(cptList[allPieces[22]], blackTeam);
        chessPieces[7, 7] = SpawnPiece(cptList[allPieces[23]], blackTeam);
        chessPieces[0, 6] = SpawnPiece(cptList[allPieces[24]], blackTeam);
        chessPieces[1, 6] = SpawnPiece(cptList[allPieces[25]], blackTeam);
        chessPieces[2, 6] = SpawnPiece(cptList[allPieces[26]], blackTeam);
        chessPieces[3, 6] = SpawnPiece(cptList[allPieces[27]], blackTeam);
        chessPieces[4, 6] = SpawnPiece(cptList[allPieces[28]], blackTeam);
        chessPieces[5, 6] = SpawnPiece(cptList[allPieces[29]], blackTeam);
        chessPieces[6, 6] = SpawnPiece(cptList[allPieces[30]], blackTeam);
        chessPieces[7, 6] = SpawnPiece(cptList[allPieces[31]], blackTeam);
        /*
        for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 6] = SpawnPiece(ChessPieceType.Pawn, blackTeam);
        }
        */
    }

    private ChessPiece SpawnPiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<SpriteRenderer>().color = teamColors[team];

        return cp;
    }

    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y] != null)
                {
                    PositionPiece(x, y, true);
                }
            }
        }
    }

    private void PositionPiece(int x, int y, bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
        if(currentTeam == 1)
        {
            chessPieces[x, y].transform.Rotate(new Vector3(0, 0, 180));
        }
    }

    private Vector3 GetTileCenter(int x, int y)
    {
        float newOffset = yOffset;
        if (chessPieces[x,y].type == ChessPieceType.Pawn)
        {
            newOffset += 0.25f;
        }
        return new Vector3(x * tileSize, newOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }
    #endregion

    #region Highlight
    private void HighLightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void RemoveHighLightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }

        availableMoves.Clear();
    }
    #endregion

    #region Move
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        if(moves != null)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                if(moves[i].x == pos.x && moves[i].y == pos.y)
                {
                    return true;
                }
            }
        }

        return false;
    }
    private bool MoveTo(int originalX, int originalY, int x, int y)
    {
        if (!ContainsValidMove(ref availableMoves, new Vector2Int(x, y)))
        {
            return false;
        }

        ChessPiece cp = chessPieces[originalX, originalY];
        Vector2Int previousPosition = new Vector2Int(originalX, originalY);

        // Another piece on the target position ?
        if (chessPieces[x, y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];

            if (cp.team == ocp.team)
            {
                return false;
            }
            
            // If enemy team
            if (ocp.team == 0)
            {
                if(ocp.type == ChessPieceType.King)
                {
                    CheckMate(cp.team);
                }

                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                    - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + (Vector3.back * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                {
                    CheckMate(cp.team);
                }

                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                    - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + (Vector3.forward * deathSpacing) * deadBlacks.Count);
            }
        }
        
        chessPieces[x, y] = cp;
        if (currentTeam == 1)
        {
            cp.transform.Rotate(new Vector3(0, 0, 180));
        }
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionPiece(x, y);
        isWhiteTurn = !isWhiteTurn;
        if(localGame)
        {
            currentTeam = (currentTeam == 0) ? 1 : 0;
        }

        moveList.Add(new Vector2Int[] {
            previousPosition,
            new Vector2Int(x,y)
        });

        ProcessSpecialMove();

        if(CheckForCheckmate())
        {
            CheckMate(cp.team);
        }

        int otherTeam = (cp.team == 0) ? 1 : 0;

        turnIndicator.transform.GetChild(otherTeam).gameObject.SetActive(true);
        turnIndicator.transform.GetChild(cp.team).gameObject.SetActive(false);

        isDraw(otherTeam);

        return true;
    }
    private void ProcessSpecialMove()
    {
        if(specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];

            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if(myPawn.currentX == enemyPawn.currentX)
            {
                if (myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    if(enemyPawn.team == 0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.back * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.forward * deathSpacing) * deadBlacks.Count);
                    }

                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }

        if(specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            Random.seed = DateTime.Now.Millisecond;
            random = Random.Range(0, promotionList.Count);

            if (targetPawn.type == ChessPieceType.Pawn)
            {
                isPromotion = true;

                // White side
                if (targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newPiece = SpawnPiece(promotionList[random], 0);
                    newPiece.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newPiece;
                    PositionPiece(lastMove[1].x, lastMove[1].y);
                }

                // Black side
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newPiece = SpawnPiece(promotionList[random], 1);
                    newPiece.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newPiece;
                    PositionPiece(lastMove[1].x, lastMove[1].y);
                }
            }
            else
            {
                isPromotion = false;
            }
        }
        else
        {
            isPromotion = false;
        }

        if(specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            // Left Rook
            if (lastMove[1].x == 2)
            {
                // White side
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;
                    PositionPiece(3, 0);
                    chessPieces[0, 0] = null;
                }
                
                // Black side
                else if(lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionPiece(3, 7);
                    chessPieces[0, 7] = null;
                }
            }

            // Right Rook
            else if (lastMove[1].x == 6)
            {
                // White side
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionPiece(5, 0);
                    chessPieces[7, 0] = null;
                }

                // Black side
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionPiece(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].type == ChessPieceType.King)
                    {
                        if (chessPieces[x, y].team == currentlySelected.team)
                        {
                            targetKing = chessPieces[x, y];
                        }
                    }
                }
            }
        }

        // Send a ref to delete moves that are putting in check
        SimulateMoveForSinglePiece(currentlySelected, ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        // Save the current values, to reset after the function call
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        // Going through all the moves, simulate them and check if we're in check
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionSim = new Vector2Int(targetKing.currentX, targetKing.currentY);

            // Did we simulate the king's move
            if(cp.type == ChessPieceType.King)
            {
                kingPositionSim = new Vector2Int(simX, simY);
            }

            // Copy the [,], not a reference
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();

            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if(chessPieces[x,y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if(simulation[x,y].team != cp.team)
                        {
                            simAttackingPieces.Add(simulation[x, y]);
                        }
                    }
                }
            }

            // Simulate that move
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            // Did one of the piece got taken down during our simulation
            var deadPiece = simAttackingPieces.Find(c => c.currentX == simX && c.currentY == simY);
            if(deadPiece != null)
            {
                simAttackingPieces.Remove(deadPiece);
            }

            // Get all the simulated attacking pieces moves
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a = 0; a < simAttackingPieces.Count; a++)
            {
                List<Vector2Int> pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                {
                    simMoves.Add(pieceMoves[b]);
                }
            }

            // If king in trouble, remove the move
            if(ContainsValidMove(ref simMoves, kingPositionSim))
            {
                movesToRemove.Add(moves[i]);
            }

            // Restore actual CP data
            cp.currentX = actualX;
            cp.currentY = actualY;
        }

        // Remove from the current available move list
        for (int i = 0; i < movesToRemove.Count; i++)
        {
            moves.Remove(movesToRemove[i]);
        }
    }
    #endregion

    #region Endgame
    private void ChangeTileIfCheck(int targetTeam, string layerName)
    {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].type == ChessPieceType.King)
                    {
                        if (chessPieces[x, y].team == targetTeam)
                        {
                            targetKing = chessPieces[x, y];
                            tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer(layerName);

                            if(layerName == "Tile")
                            {
                                Vector2Int[] lastMove = moveList[moveList.Count - 1];
                                tiles[lastMove[0].x, lastMove[0].y].layer = LayerMask.NameToLayer(layerName);
                            }
                        }
                    }
                }
            }
        }
    }
    private bool CheckForCheckmate()
    {
        Vector2Int[] lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;
        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();

        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[x, y]);

                        if(chessPieces[x, y].type == ChessPieceType.King)
                        {
                            targetKing = chessPieces[x, y];
                        }
                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                }
            }
        }

        // Is the king attacking now ?
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            List<Vector2Int> pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                currentAvailableMoves.Add(pieceMoves[b]);
            }
        }

        // Are we in check ?
        if(ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            // King is under attack
            tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("Check");
            isInCheck = true;

            // Can we help him ?
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);

                // Send a ref to delete moves that are putting in check
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                // Not in checkmate
                if(defendingMoves.Count != 0)
                {
                    return false;
                }
            }

            // Checkmate
            return true;
        }
        else
        {
            isInCheck = false;
        }

        return false;
    }
    private bool isDraw(int targetTeam)
    {
        if(!isInCheck)
        {
            List<ChessPiece> defendingPieces = new List<ChessPiece>();

            ChessPiece targetKing = null;
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (chessPieces[x, y] != null)
                    {
                        if (chessPieces[x, y].team == targetTeam)
                        {
                            defendingPieces.Add(chessPieces[x, y]);

                            if (chessPieces[x, y].type == ChessPieceType.King)
                            {
                                targetKing = chessPieces[x, y];
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);

                // Send a ref to delete moves that are putting in check
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                // Not in checkmate
                if (defendingMoves.Count > 0)
                {
                    return false;
                }

            }
            Draw();

            return true;
        }

        return false;
    }
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void Draw()
    {
        DisplayVictory(-1);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);

        GameObject messageVictory = victoryScreen.transform.GetChild(0).gameObject;
        messageVictory.SetActive(true);

        if(winningTeam == 0)
        {
            messageVictory.GetComponent<TextMeshProUGUI>().text = "White wins !";
        }
        else if(winningTeam == 1)
        {
            messageVictory.GetComponent<TextMeshProUGUI>().text = "Black wins !";
        }
        else
        {
            messageVictory.GetComponent<TextMeshProUGUI>().text = "Draw !";
        }
    }
    public void OnRematchButton()
    {
        if(localGame)
        {
            // White rematch
            NetRematch wrm = new NetRematch();
            wrm.teamId = currentTeam;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            // Black rematch
            NetRematch brm = new NetRematch();
            brm.teamId = currentTeam;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);
        }
        else
        {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);
        }
    }
    public void GameReset()
    {
        // Remove UI
        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);

        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        rematchButton.interactable = true;

        // Fields reset
        currentlySelected = null;
        availableMoves.Clear();
        moveList.Clear();
        playerRematch[0] = playerRematch[1] = false;

        // Clean board
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y] != null)
                {
                    Destroy(chessPieces[x, y].gameObject);
                }

                chessPieces[x, y] = null;
            }
        }

        // Remove dead pieces
        for (int i = 0; i < deadWhites.Count; i++)
        {
           Destroy(deadWhites[i].gameObject);
        }
        for (int i = 0; i < deadBlacks.Count; i++)
        {
            Destroy(deadBlacks[i].gameObject);
        }

        deadWhites.Clear();
        deadBlacks.Clear();

        // Reset the board
        //SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;

    }
    public void OnMenuButton()
    {
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        GameReset();
        GameUI.Instance.OnLeaveFromGameMenu();
        Invoke("ShutdownRelay", 1.0f);

        // Reset some values
        playerCount = -1;
        currentTeam = -1;
    }
    #endregion

    private Vector2Int TileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (tiles[x, y] == hitInfo)
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return -Vector2Int.one; // -1, -1
    }

    #region Multiplayer Methods
    private void RegisterEvents()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_GENERATE_BOARD += OnGenerateBoardServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.S_REMATCH += OnRematchServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_GENERATE_BOARD += OnGenerateBoardClient;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.C_REMATCH += OnRematchClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
    }
    private void UnRegisterEvents()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.S_GENERATE_BOARD -= OnGenerateBoardServer;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_REMATCH -= OnRematchServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;
        NetUtility.C_GENERATE_BOARD -= OnGenerateBoardClient;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.C_REMATCH -= OnRematchClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
    }

    // Server
    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn)
    {
        // Client has connected, assign a team and return the message back to him
        NetWelcome nw = msg as NetWelcome;


        // Assign a team
        nw.AssignedTeam = ++playerCount;

        // Return back to the client
        Server.Instance.SendToClient(cnn, nw);

        // If full, start the game
        if (playerCount == 1)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnGenerateBoardServer(NetMessage msg, NetworkConnection cnn)
    {
        // Receive the message, broadcast it back
        NetGenerateBoard gb = msg as NetGenerateBoard;

        // Receive and just broadcast it back
        Server.Instance.Broadcast(gb);
    }
    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn)
    {
        // Receive the message, broadcast it back
        NetMakeMove mm = msg as NetMakeMove;

        // Receive and just broadcast it back
        Server.Instance.Broadcast(mm);
    }
    private void OnRematchServer(NetMessage msg, NetworkConnection cnn)
    {
        // Receive and just broadcast it back
        Server.Instance.Broadcast(msg);
    }

    // Client
    private void OnWelcomeClient(NetMessage msg)
    {
        // Receive the connection message
        NetWelcome nw = msg as NetWelcome;

        // Assign the team
        currentTeam = nw.AssignedTeam;

        if(localGame && currentTeam == 0)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnStartGameClient(NetMessage msg)
    {
        if (currentTeam == 0)
        {
            generatePieces();
        }
        // We just need to change the camera
        GameUI.Instance.ChangeCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);

        turnIndicator.transform.GetChild(0).gameObject.SetActive(true);

        if (currentTeam == 1)
        {
            transform.Rotate(new Vector3(0, 0, 0));
        }
        
    }
    private void OnGenerateBoardClient(NetMessage msg)
    {
        NetGenerateBoard gb = msg as NetGenerateBoard;

        List<int> allPieces = new List<int>();
        allPieces.Add(gb.whitePiece1);
        allPieces.Add(gb.whitePiece2);
        allPieces.Add(gb.whitePiece3);
        allPieces.Add(gb.whitePiece4);
        allPieces.Add(gb.whitePiece5);
        allPieces.Add(gb.whitePiece6);
        allPieces.Add(gb.whitePiece7);
        allPieces.Add(gb.whitePiece8);
        allPieces.Add(gb.whitePiece9);
        allPieces.Add(gb.whitePiece10);
        allPieces.Add(gb.whitePiece11);
        allPieces.Add(gb.whitePiece12);
        allPieces.Add(gb.whitePiece13);
        allPieces.Add(gb.whitePiece14);
        allPieces.Add(gb.whitePiece15);
        allPieces.Add(gb.whitePiece16);
        allPieces.Add(gb.whitePiece17);
        allPieces.Add(gb.whitePiece18);
        allPieces.Add(gb.whitePiece19);
        allPieces.Add(gb.whitePiece20);
        allPieces.Add(gb.whitePiece21);
        allPieces.Add(gb.whitePiece22);
        allPieces.Add(gb.whitePiece23);
        allPieces.Add(gb.whitePiece24);
        allPieces.Add(gb.whitePiece25);
        allPieces.Add(gb.whitePiece26);
        allPieces.Add(gb.whitePiece27);
        allPieces.Add(gb.whitePiece28);
        allPieces.Add(gb.whitePiece29);
        allPieces.Add(gb.whitePiece30);
        allPieces.Add(gb.whitePiece31);
        allPieces.Add(gb.whitePiece32);

        SpawnAllPieces(allPieces);
        PositionAllPieces();
    }
    private void OnMakeMoveClient(NetMessage msg)
    {
        NetMakeMove mm = msg as NetMakeMove;

        if(mm.teamId != currentTeam)
        {
            ChessPiece target = chessPieces[mm.originalX, mm.originalY];

            //Debug.Log($"isPromotion : {mm.isPromotion}");
            availableMoves = target.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            specialMove = target.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

            bool validMove = MoveTo(mm.originalX, mm.originalY, mm.destinationX, mm.destinationY);

            if(validMove)
            {
                ChangeTileIfCheck(mm.teamId, "Tile");

                if (mm.isPromotion == 1)
                {
                    if (target.type == ChessPieceType.Pawn)
                    {
                        ChessPiece newPiece = SpawnPiece(promotionList[mm.randomPromotionPiece], mm.teamId);
                        newPiece.transform.position = chessPieces[mm.destinationX, mm.destinationY].transform.position;
                        Destroy(chessPieces[mm.destinationX, mm.destinationY].gameObject);
                        chessPieces[mm.destinationX, mm.destinationY] = newPiece;
                        PositionPiece(mm.destinationX, mm.destinationY);
                    }
                }
            }
        }
    }
    private void OnRematchClient(NetMessage msg)
    {
        NetRematch rm = msg as NetRematch;

        // Set the bool for rematch
        playerRematch[rm.teamId] = rm.wantRematch == 1;

        // Activate the piece of UI
        if(rm.teamId != currentTeam)
        {
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);

            if(rm.wantRematch != 1)
            {
                rematchButton.interactable = false;
            }
        }

        // If both wants to rematch
        if(playerRematch[0] && playerRematch[1])
        {
            GameReset();
        }

    }
    private void OnSetLocalGame(bool v)
    {
        playerCount = -1;
        currentTeam = -1;
        localGame = v;
    }

    private void ShutdownRelay()
    {
        Client.Instance.Shutdown();
        Server.Instance.Shutdown();
    }

    private void generatePieces()
    {
        if(currentTeam == 0)
        {
            Random.InitState((int)DateTime.Now.Ticks);
            List<int> randomPiecesOrder = new List<int>();
            List<int> whiteChessPieces = new List<int>();
            whiteChessPieces.Add(0);
            whiteChessPieces.Add(1);
            whiteChessPieces.Add(2);
            whiteChessPieces.Add(3);
            whiteChessPieces.Add(4);
            whiteChessPieces.Add(2);
            whiteChessPieces.Add(1);
            whiteChessPieces.Add(0);
            whiteChessPieces.Add(5);
            whiteChessPieces.Add(5);
            whiteChessPieces.Add(5);
            whiteChessPieces.Add(5);
            whiteChessPieces.Add(5);
            whiteChessPieces.Add(5);
            whiteChessPieces.Add(5);
            whiteChessPieces.Add(5);

            for (int i = 0; i < 16; i++)
            {
                int random = Random.Range(0, whiteChessPieces.Count);
                if(i == 7)
                {
                    if(whiteChessPieces.IndexOf(4) > 0)
                    {
                        randomPiecesOrder.Add(4);
                        whiteChessPieces.RemoveAt(whiteChessPieces.IndexOf(4));

                        continue;
                    }
                }
                randomPiecesOrder.Add(whiteChessPieces[random]);
                whiteChessPieces.RemoveAt(random);
            }

            List<int> blackChessPieces = new List<int>();
            blackChessPieces.Add(0);
            blackChessPieces.Add(1);
            blackChessPieces.Add(2);
            blackChessPieces.Add(3);
            blackChessPieces.Add(4);
            blackChessPieces.Add(2);
            blackChessPieces.Add(1);
            blackChessPieces.Add(0);
            blackChessPieces.Add(5);
            blackChessPieces.Add(5);
            blackChessPieces.Add(5);
            blackChessPieces.Add(5);
            blackChessPieces.Add(5);
            blackChessPieces.Add(5);
            blackChessPieces.Add(5);
            blackChessPieces.Add(5);

            for (int i = 0; i < 16; i++)
            {
                int random = Random.Range(0, blackChessPieces.Count);
                if (i == 7)
                {
                    if (blackChessPieces.IndexOf(4) > 0)
                    {
                        randomPiecesOrder.Add(4);
                        blackChessPieces.RemoveAt(blackChessPieces.IndexOf(4));

                        continue;
                    }
                }
                randomPiecesOrder.Add(blackChessPieces[random]);
                blackChessPieces.RemoveAt(random);
            }


            NetGenerateBoard gb = new NetGenerateBoard();
            gb.whitePiece1 = randomPiecesOrder[0];
            gb.whitePiece2 = randomPiecesOrder[1];
            gb.whitePiece3 = randomPiecesOrder[2];
            gb.whitePiece4 = randomPiecesOrder[3];
            gb.whitePiece5 = randomPiecesOrder[4];
            gb.whitePiece6 = randomPiecesOrder[5];
            gb.whitePiece7 = randomPiecesOrder[6];
            gb.whitePiece8 = randomPiecesOrder[7];
            gb.whitePiece9 = randomPiecesOrder[8];
            gb.whitePiece10 = randomPiecesOrder[9];
            gb.whitePiece11 = randomPiecesOrder[10];
            gb.whitePiece12 = randomPiecesOrder[11];
            gb.whitePiece13 = randomPiecesOrder[12];
            gb.whitePiece14 = randomPiecesOrder[13];
            gb.whitePiece15 = randomPiecesOrder[14];
            gb.whitePiece16 = randomPiecesOrder[15];
            gb.whitePiece17 = randomPiecesOrder[16];
            gb.whitePiece18 = randomPiecesOrder[17];
            gb.whitePiece19 = randomPiecesOrder[18];
            gb.whitePiece20 = randomPiecesOrder[19];
            gb.whitePiece21 = randomPiecesOrder[20];
            gb.whitePiece22 = randomPiecesOrder[21];
            gb.whitePiece23 = randomPiecesOrder[22];
            gb.whitePiece24 = randomPiecesOrder[23];
            gb.whitePiece25 = randomPiecesOrder[24];
            gb.whitePiece26 = randomPiecesOrder[25];
            gb.whitePiece27 = randomPiecesOrder[26];
            gb.whitePiece28 = randomPiecesOrder[27];
            gb.whitePiece29 = randomPiecesOrder[28];
            gb.whitePiece30 = randomPiecesOrder[29];
            gb.whitePiece31 = randomPiecesOrder[30];
            gb.whitePiece32 = randomPiecesOrder[31];
            Client.Instance.SendToServer(gb);
        }
    }
    private ChessPieceType GetPieceType(int index)
    {
        ChessPieceType cpType = ChessPieceType.Pawn;
        switch (index)
        {
            case 0:
                cpType = ChessPieceType.Rook;
                break;
            case 1:
                cpType = ChessPieceType.Knight;
                break;
            case 2:
                cpType = ChessPieceType.Bishop;
                break;
            case 3:
                cpType = ChessPieceType.Queen;
                break;
            case 4:
                cpType = ChessPieceType.King;
                break;
            case 5:
                cpType = ChessPieceType.Pawn;
                break;
            default:
                cpType = ChessPieceType.Pawn;
                break;
        }

        return cpType;
    }
    #endregion
}
