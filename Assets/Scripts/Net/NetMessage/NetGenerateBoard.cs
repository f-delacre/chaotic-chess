using Unity.Networking.Transport;

public class NetGenerateBoard : NetMessage
{
    public int whitePiece1;
    public int whitePiece2;
    public int whitePiece3;
    public int whitePiece4;
    public int whitePiece5;
    public int whitePiece6;
    public int whitePiece7;
    public int whitePiece8;
    public int whitePiece9;
    public int whitePiece10;
    public int whitePiece11;
    public int whitePiece12;
    public int whitePiece13;
    public int whitePiece14;
    public int whitePiece15;
    public int whitePiece16;
    public int whitePiece17;
    public int whitePiece18;
    public int whitePiece19;
    public int whitePiece20;
    public int whitePiece21;
    public int whitePiece22;
    public int whitePiece23;
    public int whitePiece24;
    public int whitePiece25;
    public int whitePiece26;
    public int whitePiece27;
    public int whitePiece28;
    public int whitePiece29;
    public int whitePiece30;
    public int whitePiece31;
    public int whitePiece32;

    public NetGenerateBoard()
    {
        Code = OpCode.GENERATE_BOARD;
    }

    public NetGenerateBoard(DataStreamReader reader)
    {
        Code = OpCode.GENERATE_BOARD;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
        writer.WriteInt(whitePiece1);
        writer.WriteInt(whitePiece2);
        writer.WriteInt(whitePiece3);
        writer.WriteInt(whitePiece4);
        writer.WriteInt(whitePiece5);
        writer.WriteInt(whitePiece6);
        writer.WriteInt(whitePiece7);
        writer.WriteInt(whitePiece8);
        writer.WriteInt(whitePiece9);
        writer.WriteInt(whitePiece10);
        writer.WriteInt(whitePiece11);
        writer.WriteInt(whitePiece12);
        writer.WriteInt(whitePiece13);
        writer.WriteInt(whitePiece14);
        writer.WriteInt(whitePiece15);
        writer.WriteInt(whitePiece16);
        writer.WriteInt(whitePiece17);
        writer.WriteInt(whitePiece18);
        writer.WriteInt(whitePiece19);
        writer.WriteInt(whitePiece20);
        writer.WriteInt(whitePiece21);
        writer.WriteInt(whitePiece22);
        writer.WriteInt(whitePiece23);
        writer.WriteInt(whitePiece24);
        writer.WriteInt(whitePiece25);
        writer.WriteInt(whitePiece26);
        writer.WriteInt(whitePiece27);
        writer.WriteInt(whitePiece28);
        writer.WriteInt(whitePiece29);
        writer.WriteInt(whitePiece30);
        writer.WriteInt(whitePiece31);
        writer.WriteInt(whitePiece32);
    }

    public override void Deserialize(DataStreamReader reader)
    {
        whitePiece1 = reader.ReadInt();
        whitePiece2 = reader.ReadInt();
        whitePiece3 = reader.ReadInt();
        whitePiece4 = reader.ReadInt();
        whitePiece5 = reader.ReadInt();
        whitePiece6 = reader.ReadInt();
        whitePiece7 = reader.ReadInt();
        whitePiece8 = reader.ReadInt();
        whitePiece9 = reader.ReadInt();
        whitePiece10 = reader.ReadInt();
        whitePiece11 = reader.ReadInt();
        whitePiece12 = reader.ReadInt();
        whitePiece13 = reader.ReadInt();
        whitePiece14 = reader.ReadInt();
        whitePiece15 = reader.ReadInt();
        whitePiece16 = reader.ReadInt();
        whitePiece17 = reader.ReadInt();
        whitePiece18 = reader.ReadInt();
        whitePiece19 = reader.ReadInt();
        whitePiece20 = reader.ReadInt();
        whitePiece21 = reader.ReadInt();
        whitePiece22 = reader.ReadInt();
        whitePiece23 = reader.ReadInt();
        whitePiece24 = reader.ReadInt();
        whitePiece25 = reader.ReadInt();
        whitePiece26 = reader.ReadInt();
        whitePiece27 = reader.ReadInt();
        whitePiece28 = reader.ReadInt();
        whitePiece29 = reader.ReadInt();
        whitePiece30 = reader.ReadInt();
        whitePiece31 = reader.ReadInt();
        whitePiece32 = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_GENERATE_BOARD?.Invoke(this);
    }
    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_GENERATE_BOARD?.Invoke(this, cnn);
    }
}
