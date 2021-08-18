using Unity.Networking.Transport;
using UnityEngine;

public class NetStartGame : NetMessage
{
    public int pieceWhite;

    public NetStartGame()
    {
        Code = OpCode.START_GAME;
    }
    public NetStartGame(DataStreamReader reader)
    {
        Code = OpCode.START_GAME;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
        writer.WriteInt(pieceWhite);
    }
    public override void Deserialize(DataStreamReader reader)
    {
        pieceWhite = reader.ReadInt();
        Debug.Log($"PieceWhite NetStartGame(Deserialize) : {pieceWhite}");
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_START_GAME?.Invoke(this);
    }
    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_START_GAME?.Invoke(this, cnn);
    }
}
