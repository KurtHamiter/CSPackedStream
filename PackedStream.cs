using System;
using System.Runtime.CompilerServices;
using System.Numerics;

public class PackedStream
{
    private byte[]  dataBuffer8;
    private ulong[] dataBuffer64;
    private uint    totalSegments;

    private int  segmentBitsWritten;
    private uint segmentsWritten;

    private int  segmentBitsRead;
    private uint segmentsRead;

    private uint quantizationFactor;

    // --- Private ---

    private uint ZigZagEncode(int value)
    {
        return (uint)((value << 1) ^ (value >> 31));
    }

    private int ZigZagDecode(uint value)
    {
        return (int)(value >> 1) ^ (-(int)(value & 1));
    }

    private void WritePackedBit(ulong bit)
    {
        ulong mask = ~(ulong.MaxValue << 1);

        dataBuffer64[segmentsWritten] &= ~(mask << segmentBitsWritten);
        dataBuffer64[segmentsWritten] |= bit << segmentBitsWritten;

        segmentBitsWritten += 1;

        if (segmentBitsWritten >= 64) {

            segmentsWritten++;
            segmentBitsWritten = 0;

            if (segmentsWritten == totalSegments)
            {
                throw new InvalidOperationException("Trying to write beyond buffer");
            }
        }
    }

    private uint ReadPackedBit()
    {
        ulong mask        = ~(ulong.MaxValue << 1);
        ulong returnValue = (dataBuffer64[segmentsRead] >> segmentBitsRead) & mask;

        segmentBitsRead += 1;

        if (segmentBitsRead >= 64) {

            segmentsRead++;
            segmentBitsRead = 0;

            if (segmentsRead == totalSegments)
            {
                throw new InvalidOperationException("Trying to read beyond buffer");
            }
        }

        return (uint)returnValue;
    }

    private void WritePackedBits(ulong bits, int length)
    {
        // First pass
        ulong mask = ~(ulong.MaxValue << length);

        dataBuffer64[segmentsWritten] &= ~(mask << segmentBitsWritten);
        dataBuffer64[segmentsWritten] |= bits << segmentBitsWritten;

        segmentBitsWritten += length;

        // Second pass
        if (segmentBitsWritten >= 64)
        {
            int overflowAmount  = segmentBitsWritten - 64;
            int underflowAmount = length - overflowAmount;
                mask            = ~(ulong.MaxValue << overflowAmount);

            segmentsWritten++;
            segmentBitsWritten -= 64;

            if (segmentsWritten == totalSegments)
            {
                throw new InvalidOperationException("Trying to write beyond buffer");
            }

            dataBuffer64[segmentsWritten] &= ~mask;
            dataBuffer64[segmentsWritten] |= bits >> underflowAmount;

        }
    }

    private uint ReadPackedBits(int length)
    {
        // First pass
        ulong mask        = ~(ulong.MaxValue << length);
        ulong returnValue = (dataBuffer64[segmentsRead] >> segmentBitsRead) & mask;

        segmentBitsRead += length;

        // Second pass
        if (segmentBitsRead >= 64)
        {
            int overflowAmount  = segmentBitsRead - 64;
            int underflowAmount = length - overflowAmount;
                mask            = ~(ulong.MaxValue << overflowAmount);

            segmentsRead++;
            segmentBitsRead -= 64;

            if (segmentsRead == totalSegments)
            {
                throw new InvalidOperationException("Trying to read beyond buffer");
            }

            ulong tempValue    = (dataBuffer64[segmentsRead] & mask) << underflowAmount;
                  returnValue |= tempValue;
        }

        return (uint)returnValue;
    }

    // --- Public ---

    public byte[] GetBuffer() { return dataBuffer8; }

    public uint GetByteCount()
    {
        uint returnValue  = segmentsWritten * 8;
             returnValue += (uint)(segmentBitsWritten / 8) + 1;
        return returnValue;
    }

    public void SetBuffer(byte[] buffer)
    {
        if (buffer.Length > 0)
        {
            if ((buffer.Length % 8) == 0)
            {
                dataBuffer8   = buffer;
                totalSegments = (uint)buffer.Length / 8;
                dataBuffer64  = Unsafe.As<byte[], ulong[]>(ref dataBuffer8);
            }
            else
            {
                throw new InvalidOperationException("Buffer length must be multiple of 8");
            }
        } else
        {
            throw new InvalidOperationException("Buffer length must greater than 0");
        }
    }

    public void SetPrecision(ushort places)
    {
        quantizationFactor = 1;
        for (int i = 0; i < places; i++) { quantizationFactor *= 10; }
    }

    public void Reset()
    {
        segmentBitsWritten = 0;
        segmentsWritten    = 0;
        segmentBitsRead    = 0;
        segmentsRead       = 0;
    }

    public void CreateBuffer(uint byteCapacity)
    {
        uint mod = byteCapacity % 8;

        uint trueCapacity = 0;
        if (mod > 0)
        {
            uint difference   = 8 - mod;
                 trueCapacity = byteCapacity + difference;
        } 
        else { trueCapacity = byteCapacity; }

        dataBuffer8     = new byte[trueCapacity];
        totalSegments   = trueCapacity / 8;
        dataBuffer64    = Unsafe.As<byte[], ulong[]>(ref dataBuffer8);
    }

    public int Quantize(float value)
    {
        return (int)(value * quantizationFactor);
    }

    public float Dequantize(int value)
    {
        return (float)value / quantizationFactor;
    }
    
    // --- Write methods ---

    public void Write(bool value)
    {
        ulong uValue = Convert.ToUInt64(value);
        WritePackedBit(uValue);
    }

    public void Write(int value)
    {
        uint encodedValue = ZigZagEncode(value);
        int  zeroCount    = BitOperations.LeadingZeroCount(encodedValue);
        int  bitLength    = 32 - zeroCount;

        if (bitLength == 0)
        {
            WritePackedBit(0);
            return;
        } else
        {
            WritePackedBit(1);
            WritePackedBits((ulong)bitLength - 1, 5);
            WritePackedBits(encodedValue, bitLength);
        }
    }

    public void Write(uint value)
    {
        int zeroCount = BitOperations.LeadingZeroCount(value);
        int bitLength = 32 - zeroCount;

        if (bitLength == 0)
        {
            WritePackedBit(0);
            return;
        }
        else
        {
            WritePackedBit(1);
            WritePackedBits((ulong)bitLength - 1, 5);
            WritePackedBits(value, bitLength);
        }
    }

    public void Write(float value)
    {
        int signedValue = Quantize(value);
        Write(signedValue);
    }

    // --- Read methods ---

    public bool ReadBool()
    {
        ulong returnValue = ReadPackedBit();
        return Convert.ToBoolean(returnValue);
    }

    public int ReadInt()
    {
        ulong flag = ReadPackedBit();

        if (flag == 0) { return 0; } 
        else {
            ulong bitLength = ReadPackedBits(5) + 1;
            ulong value     = ReadPackedBits((int)bitLength);
            return ZigZagDecode((uint)value);
        }
    }

    public uint ReadUInt()
    {
        ulong flag = ReadPackedBit();

        if (flag == 0) { return 0; }
        else
        {
            ulong bitLength = ReadPackedBits(5) + 1;
            ulong value     = ReadPackedBits((int)bitLength);
            return (uint)value;
        }
    }

    public float ReadFloat()
    {
        int returnValue = ReadInt();
        return Dequantize(returnValue);
    }

}
