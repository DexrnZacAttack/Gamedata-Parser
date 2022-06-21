// Written by /u/PhoenixARC-Real

/*
File format note from PhoenixARC:
<Is the GAMEDATA an archive that has multiple files?>

- Yeah, it's an archive with several files, if you've ever used mcc tool chest then those are the files inside it.
- also, the first 4 bytes of the file is an int, the offset where the filenames are, each filename buffer is 144 bytes in length,
  so it takes the total amount of files, multiplies it by 144, and takes that amount of data from the file at that offset

More help from PhoenixARC about the chained BitConverter methods:
<Would you mind explaining how to calculate the offsets? I got the file name calculations working, but I'm not sure how I should
implement the DataAmt and DataOffset sections, specifically the .Skip().Take().Reverse().ToArray() parts. I think I understand
that it is clipping a view of the byte array to access the number which holds the data for how long the embedded file is and what
it's offset is, but I'm having some trouble reverse-engineering exactly those calls are doing to the bytes to access those
sections of the data, or at least where they are at in the byte stream. Thanks!>

- .skip skips the byte array by the amount skip is specified so skip(11) skips to the file offset 0x0000000A. Take is the amount
  of bytes to take, so .take(4) would result in {00, 00, 00, 00}, reverse reverses the resulting list, so {01, 02, 03, 04} turns
  into {04, 03, 02, 01}. And toarray just turns a list of bytes into an array of bytes
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zlib;

namespace SaveWorker.model
{
  public class ZlibActions
  {

    private ArraySupport ArrSupport;
    public ZlibActions()
        {
      ArrSupport = new ArraySupport();
    }

    #region WiiU Working File


    public void CreateWiiUWorkingFile(string inputFilePath, string outputFilePath)
    {
      byte[] dat = File.ReadAllBytes(inputFilePath);
      byte[] u = dat.Skip(8).Take(dat.Length - 8).ToArray();
      byte[] bytes = zLibStreamDecompress(u);
      File.WriteAllBytes(outputFilePath, bytes);
      List<byte[]> filenameBytes = LoadFilenames(outputFilePath);
      /**/
      foreach (byte[] data in filenameBytes)
      {
        string data1 = Encoding.Unicode.GetString(endianReverseUnicode(readBytes(144, data))).Split(new[] { (char)0x00 }, StringSplitOptions.None)[0];

        int DataAmt = BitConverter.ToInt32(data.Skip(128).Take(4).Reverse().ToArray(), 0);
        int DataOffset = BitConverter.ToInt32(data.Skip(132).Take(4).Reverse().ToArray(), 0);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(inputFilePath) + "/savegameData/" + data1));
        if (data1.EndsWith(".mcr"))
        {
          PrintAllBlocks(bytes.Skip(DataOffset).Take(DataAmt).ToArray(), Path.GetDirectoryName(inputFilePath) + "/savegameData/" + data1);
        }
        File.WriteAllBytes(Path.GetDirectoryName(inputFilePath) + "/savegameData/" + data1, bytes.Skip(DataOffset).Take(DataAmt).ToArray());
      }


    }
    public void CreatePS3UWorkingFile(string inputFilePath)
    {
      List<byte[]> filenameBytes = LoadFilenames(inputFilePath);
      byte[] bytes = File.ReadAllBytes(inputFilePath);
      
      foreach (byte[] data in filenameBytes)
      {
        string data1 = Encoding.Unicode.GetString(endianReverseUnicode(readBytes(144, data))).Split(new[] { (char)0x00 }, StringSplitOptions.None)[0];

        int DataAmt = BitConverter.ToInt32(data.Skip(128).Take(4).Reverse().ToArray(), 0);
        int DataOffset = BitConverter.ToInt32(data.Skip(132).Take(4).Reverse().ToArray(), 0);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetDirectoryName(inputFilePath) + "/savegameData/" + data1));/*
        if (data1.EndsWith(".mcr"))
        {
          PrintAllBlocks(bytes.Skip(DataOffset).Take(DataAmt).ToArray(), Path.GetDirectoryName(inputFilePath) + "/savegameData/" + data1);
        }*/
        File.WriteAllBytes(Path.GetDirectoryName(inputFilePath) + "/savegameData/" + data1, bytes.Skip(DataOffset).Take(DataAmt).ToArray());
      }


    }

    public static byte[] zLibStreamDecompress(byte[] inputBytes)
    {
      byte[] result;
      MemoryStream ms = new MemoryStream();

      ZlibStream zLibS = new ZlibStream(ms, CompressionMode.Decompress, true);

      zLibS.Write(inputBytes, 0, inputBytes.Length);

      return ms.ToArray();
    }

        #endregion

        #region WiiU Extract Files

        public List<byte[]> LoadFilenames(string inputFilePath)
    {
      List<byte[]> ls1 = new List<byte[]>();

      byte[] dat = File.ReadAllBytes(inputFilePath);

      int offset = BitConverter.ToInt32(dat.Skip(0).Take(4).Reverse().ToArray(), 0);
      int NumOfFiles = BitConverter.ToInt32(dat.Skip(4).Take(4).Reverse().ToArray(), 0);

      byte[] Filenames = dat.Skip(offset).Take(144 * NumOfFiles).ToArray();

      int i = 0;

      while (i < NumOfFiles)
      {
        ls1.Add(Filenames.Skip(144 * i).Take(144).ToArray());
        Console.WriteLine(Encoding.ASCII.GetString(Filenames.Skip(144 * i).Take(144).ToArray()));
        i++;
      }

      return ls1;

    }


    private static byte[] endianReverseUnicode(byte[] str)
    {
      byte[] newStr = new byte[str.Length];
      for (int i = 0; i < str.Length; i += 2)
      {
        newStr[i] = str[i + 1];
        newStr[i + 1] = str[i];
      }
      return newStr;
    }


    private int p;

    public int readByte(byte[] b)
    {
      return b[p++] & 0xFF;
    }

    public byte[] readBytes(int length, byte[] data)
    {
      List<byte> list = new List<byte>();
      p = 0;
      for (int i = 0; i < length; i++)
      {
        list.Add((byte)readByte(data));
      }
      return list.ToArray();
    }

    public static void PrintAllBlocks(byte[] bytes, string OutFile)
    {
      int x = 0, y = 0, z = 0;
      MemoryStream s = new MemoryStream(bytes);
      s.Position = 8192; // hex 0x2000
      StreamWriter writer = new StreamWriter(OutFile + ".txt");
      writer.AutoFlush = true;

      for (var i = 0; i < bytes.Length; i += 2)
      {
        int b1 = s.ReadByte();
        int b2 = s.ReadByte();
        byte specialBit = (byte)(b2 & 0xF);
        byte blockID = (byte)(((b2 & 0xF) << 4) + ((b1 >> 4) & 0xF));
        byte blockData = (byte)(b1 & 0xF);
        writer.Write($"Position: {x},{y},{z} | Flags: Aquatic:{(byte)((specialBit >> 0) & 1) == 1} WaterLogged:{(byte)((specialBit >> 3) & 1) == 1} | Block ID: {blockID} | BlockData: {blockData}\n");
        //File.AppendAllText(OutFile + ".txt", $"Position: {x},{y},{z} | Flags: Aquatic:{(byte)((specialBit >> 0) & 1) == 1} WaterLogged:{(byte)((specialBit >> 3) & 1) == 1} | Block ID: {blockID} | BlockData: {blockData}\n");
        y++;
        if (y > 255) { y = 0; z++; }
        if (z > 15) { z = 0; x++; }
      }

      writer.Close();
      writer.Dispose();
    }

    #endregion
  }
}