using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChessPieceType
{
    None = 0,
    Pawn = 1,
    Rook = 2,
    Knight = 3,
    Bishop = 4,
    Queen = 5,
    King = 6
}

public class ChessPiece : MonoBehaviour
{
    public int team;
    public int currentX;
    public int currentY;
    public ChessPieceType type;

    private Vector3 desirePosition;
    private Vector3 desireScale = new Vector3(0.08f, 0.08f, 0.08f);

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, desirePosition, Time.deltaTime * 10);
        transform.localScale = Vector3.Lerp(transform.localScale, desireScale, Time.deltaTime * 10);
    }

    public virtual List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        r.Add(new Vector2Int(3, 3));
        r.Add(new Vector2Int(3, 4));
        r.Add(new Vector2Int(4, 3));
        r.Add(new Vector2Int(4, 4));

        return r;
    }

    public virtual void SetPosition(Vector3 position, bool force = false)
    {
        desirePosition = position;

        if (force)
        {
            transform.position = desirePosition;
        }
    }
    public virtual void SetScale(Vector3 scale, bool force = false)
    {
        desireScale = scale;

        if (force)
        {
            transform.localScale = desireScale;
        }
    }

    public virtual SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        return SpecialMove.None;
    }
}
