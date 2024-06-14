using System;
using System.Text;

public enum STRING_TYPE
{
    TYPE_ASCII,
    TYPE_UNICODE,
    TYPE_UNDETERMINED
}

public enum EXTRACT_TYPE
{
    EXTRACT_RAW,
    EXTRACT_ASM
}

public class StringParser
{
    private const int MAX_STRING_SIZE = 1024;
    private const int BLOCK_SIZE = 0x50000;
    private static readonly bool[] isAscii = new bool[256];

    static StringParser()
    {
        for (int i = 0; i < 256; i++)
        {
            isAscii[i] = (i >= 0x20 && i <= 0x7E);
        }
    }

    public int ExtractImmediate(char[] immediate, int immediateSize, ref STRING_TYPE stringType, byte[] outputString)
    {
        int i = 0;
        switch (stringType)
        {
            case STRING_TYPE.TYPE_ASCII:
                while (i < immediateSize && isAscii[immediate[i]])
                {
                    outputString[i] = (byte)immediate[i];
                    i++;
                }
                return i;

            case STRING_TYPE.TYPE_UNICODE:
                while (i + 1 < immediateSize && isAscii[immediate[i]] && immediate[i + 1] == 0)
                {
                    outputString[i / 2] = (byte)immediate[i];
                    i += 2;
                }
                return i / 2;

            case STRING_TYPE.TYPE_UNDETERMINED:
                if (!isAscii[immediate[0]])
                {
                    return 0;
                }
                else if (immediateSize > 1 && immediate[1] == 0)
                {
                    stringType = STRING_TYPE.TYPE_UNICODE;
                    return ExtractImmediate(immediate, immediateSize, ref stringType, outputString);
                }
                else
                {
                    stringType = STRING_TYPE.TYPE_ASCII;
                    return ExtractImmediate(immediate, immediateSize, ref stringType, outputString);
                }

            default:
                return 0;
        }
    }

    public int ExtractString(byte[] buffer, long bufferSize, long offset, byte[] outputString, int outputStringSize, ref int outputStringLength, ref EXTRACT_TYPE extractType, ref STRING_TYPE stringType)
    {
        extractType = EXTRACT_TYPE.EXTRACT_RAW;
        outputStringLength = 0;
        int i = 0;

        ushort value = BitConverter.ToUInt16(buffer, (int)offset);
        switch (value)
        {
            case 0x45C6:
                int instSize = 4;
                int immSize = 1;
                int immOffset = instSize - immSize;
                int maxStringSize = 1;
                while (offset + i + instSize < bufferSize && outputStringLength + maxStringSize < outputStringSize &&
                       buffer[offset + i] == 0xC6 && buffer[offset + i + 1] == 0x45)
                {
                    char[] immediate = { (char)buffer[offset + immOffset + i] };
                    int size = ExtractImmediate(immediate, immSize, ref stringType, outputString);
                    Array.Copy(buffer, (int)(offset + immOffset + i), outputString, 0, size);
                    outputStringLength += size;
                    i += instSize;

                    if ((stringType == STRING_TYPE.TYPE_UNICODE && size < ((immSize + 1) / 2)) ||
                        (stringType == STRING_TYPE.TYPE_ASCII && size < immSize))
                        break;
                }
                extractType = EXTRACT_TYPE.EXTRACT_ASM;
                return i;

            default:
                if (isAscii[buffer[offset]])
                {
                    if (buffer[offset + 1] == 0)
                    {
                        while (offset + i + 1 < bufferSize && i / 2 < outputStringSize && isAscii[buffer[offset + i]] && buffer[offset + i + 1] == 0 && i / 2 + 1 < outputStringSize)
                        {
                            outputString[i / 2] = buffer[offset + i];
                            i += 2;
                        }
                        outputStringLength = i / 2;
                        stringType = STRING_TYPE.TYPE_UNICODE;
                        return i;
                    }
                    else
                    {
                        i = (int)offset;
                        while (i < bufferSize && isAscii[buffer[i]])
                            i++;
                        outputStringLength = i - (int)offset;
                        if (outputStringLength > outputStringSize)
                            outputStringLength = outputStringSize;

                        Array.Copy(buffer, (int)offset, outputString, 0, outputStringLength);
                        stringType = STRING_TYPE.TYPE_ASCII;
                        return outputStringLength;
                    }
                }
                break;
        }

        outputStringLength = 0;
        return 0;
    }

    public bool ProcessContents(byte[] fileContents, long bufferSize, string filename)
    {
        byte[] outputString = new byte[MAX_STRING_SIZE + 1];
        int outputStringSize = 0;

        long offset = 0;
        EXTRACT_TYPE extractType = EXTRACT_TYPE.EXTRACT_RAW;
        STRING_TYPE stringType = STRING_TYPE.TYPE_UNDETERMINED;
        while (offset < bufferSize)
        {
            int stringDiskSpace = ExtractString(fileContents, bufferSize, offset, outputString, MAX_STRING_SIZE, ref outputStringSize, ref extractType, ref stringType);

            if (outputStringSize >= 1)
            {
                string output = Encoding.UTF8.GetString(outputString, 0, outputStringSize);
                Console.WriteLine(output);
            }

            offset += Math.Max(stringDiskSpace, 1);
        }

        return true;
    }

    public bool ParseBlock(byte[] buffer, int bufferLength, string datasource)
    {
        if (buffer != null && bufferLength > 0)
        {
            return ProcessContents(buffer, bufferLength, datasource);
        }
        return false;
    }
}