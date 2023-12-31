# CSPackedStream

Simple bit packing class for compressing data in C#. Works well with delta compressed values. 

## Pre-existing buffer example
```
    static void Main()
    {

        PackedStream packedStream = new PackedStream();

        byte[] myBuffer = new byte[65536];
        // Buffer size MUST be multiple of 8 otherwise SetBuffer will throw.
        packedStream.SetBuffer(myBuffer);
        // Sets the amount of decimal places when packing floats.
        // In this example, all values in floats past the third decimal place will be truncated.
        packedStream.SetPrecision(3);

        // Reset buffer positions before initial read/write to start from the beginning.
        packedStream.Reset();

        packedStream.Write(12);
        packedStream.Write(345.6789f);
        packedStream.Write(true);

        Console.WriteLine(packedStream.ReadInt());
        Console.WriteLine(packedStream.ReadFloat());
        Console.WriteLine(packedStream.ReadBool());

        // Output:
        // 12
        // 345.678
        // True

    }
```

## Create internal buffer example

```
    static void Main()
    {

        PackedStream packedStream = new PackedStream();

        // If buffer size is not a multiple of 8, it will be automatically increased. Will not throw.
        packedStream.CreateBuffer(65536);
        // In this example, all values in floats past the second decimal place will be truncated.
        packedStream.SetPrecision(2);

        // Reset buffer positions before initial read/write to start from the beginning.
        packedStream.Reset();

        packedStream.Write(12);
        packedStream.Write(345.6789f);
        packedStream.Write(false);

        Console.WriteLine(packedStream.ReadInt());
        Console.WriteLine(packedStream.ReadFloat());
        Console.WriteLine(packedStream.ReadBool());

        // Obtain internal byte buffer.
        byte[] myBuffer = packedStream.GetBuffer();
        // Get total amount of bytes written since the last reset.
        uint bytesWritten = packedStream.GetByteCount();

        // Output:
        // 12
        // 345.67
        // False

    }
```
