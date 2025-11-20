using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Utilities related to Stream handling and conversion
/// </summary>
public abstract partial class Framework
{

    /// <summary>
    /// Converts a base64 string to bytes
    /// </summary>
    /// <param name="base64Content"></param>
    /// <returns></returns>
    public static byte[] Base64DecodeToBytes(string base64Content)
    {
        using (var memoryStream = new MemoryStream())
        {
            // Process in manageable chunks
            int base64Length = base64Content.Length;
            int chunkSize = 64 * 1024; // 64KB chunks

            // Get a pooled buffer once, reuse for each chunk
            byte[] buffer = ArrayPool<byte>.Shared.Rent(48 * 1024); // Base64 decode buffer

            try
            {
                for (int i = 0; i < base64Length; i += chunkSize)
                {
                    int currentChunkSize = Math.Min(chunkSize, base64Length - i);
                    string base64Chunk = base64Content.Substring(i, currentChunkSize);

                    // Convert chunk to bytes (reuse the same buffer)
                    int bytesCount = Convert.TryFromBase64String(
                        base64Chunk,
                        buffer,
                        out int bytesWritten) ? bytesWritten : 0;

                    // Write only the valid bytes
                    if (bytesCount > 0)
                        memoryStream.Write(buffer, 0, bytesCount);
                }

                return memoryStream.ToArray();
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
    /// <summary>
    /// Copies a stream object efficiently using buffer pooling
    /// </summary>
    /// <param name="input">Source stream</param>
    /// <param name="output">Destination stream</param>
    public static void CopyStream(Stream input, Stream output)
    {
        // Use a larger buffer size for better throughput
        const int bufferSize = 81920; // 80KB buffer for optimal performance
        
        // Rent buffer from pool instead of allocating
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        try
        {
            int bytesRead;
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, bytesRead);
            }
        }
        finally
        {
            // Always return buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Copies any stream into a memory stream object that is seekable and can therefore be read more than once
    /// </summary>
    /// <param name="inputStream">Input stream to copy</param>
    /// <returns>Seekable Stream object</returns>
    public static Stream CopyStreamAndClose(Stream inputStream)
    {
        // Try to get content length to pre-size the MemoryStream
        long? contentLength = null;
        try
        {
            if (inputStream.CanSeek)
            {
                contentLength = inputStream.Length - inputStream.Position;
            }
        }
        catch (NotSupportedException)
        {
            // Some streams don't support seeking or getting length
        }
        
        // Use optimal buffer size
        const int bufferSize = 81920; // 80KB buffer for better throughput
        
        // Create a pre-sized memory stream if possible
        MemoryStream ms = contentLength.HasValue && contentLength.Value <= int.MaxValue
            ? new MemoryStream((int)contentLength.Value)
            : new MemoryStream();
            
        // Rent buffer from pool instead of allocating
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        try
        {
            int bytesRead;
            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }
            
            // Rewind the stream to beginning
            ms.Position = 0;
            
            // Close the input stream as requested by method contract
            inputStream.Close();
            
            return ms;
        }
        finally
        {
            // Always return buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }



    /// <summary>
    /// Generic function that converts a Stream object into a string (assumes an utf-8 encoded string to return)
    /// </summary>
    /// <param name="input">Stream to convert</param>
    /// <returns></returns>
    public static string ConvertStreamToString(Stream input)
    {
        return ConvertStreamToString(input, "utf-8");
    }

    /// <summary>
    /// Generic function that converts a Stream object into a string
    /// </summary>
    /// <param name="input">Stream to convert</param>
    /// <param name="encoding">The encoding of the string that needs to be returned</param>
    /// <returns></returns>
    public static string ConvertStreamToString(Stream input, string encoding)
    {
        // Get encoding
        Encoding encodingToReturn = Encoding.GetEncoding(encoding);
        
        // Try to get stream length to pre-size the StringBuilder
        int initialCapacity = 4096; // Default capacity
        try
        {
            if (input.CanSeek)
            {
                long length = input.Length - input.Position;
                if (length > 0 && length <= int.MaxValue)
                {
                    // Add 10% margin to account for encoding expansion
                    initialCapacity = (int)Math.Min(int.MaxValue, (long)(length * 1.1));
                }
            }
        }
        catch (NotSupportedException)
        {
            // Some streams don't support seeking or getting length
        }
        
        // Create an optimally sized StringBuilder
        var stringBuilder = new StringBuilder(initialCapacity);
        
        // Use a larger buffer for better performance
        const int bufferSize = 16384; // 16KB
        
        // Rent buffer from pool instead of allocating
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        try
        {
            int bytesRead;
            
            // Read in chunks until end of stream
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Decode bytes to string using the specified encoding
                string decoded = encodingToReturn.GetString(buffer, 0, bytesRead);
                stringBuilder.Append(decoded);
            }
            
            return stringBuilder.ToString();
        }
        finally
        {
            // Always return buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Converts a stream to a byte array that you can stream to the browser,
    /// using pooled buffers for better memory efficiency
    /// </summary>
    /// <returns>Byte array containing stream data</returns>
    /// <param name="input">Input stream</param>
    public static byte[] ConvertStreamToByteArray(Stream input)
    {
        // Try to get stream length to pre-size the MemoryStream if possible
        long? streamLength = null;
        try
        {
            if (input.CanSeek)
            {
                streamLength = input.Length - input.Position;
            }
        }
        catch (NotSupportedException)
        {
            // Some streams don't support seeking or length - that's OK
        }
        
        // Create right-sized memory stream if possible
        using var ms = streamLength.HasValue && streamLength.Value <= int.MaxValue
            ? new MemoryStream((int)streamLength.Value)
            : new MemoryStream();
            
        // Rent a buffer from the shared pool instead of allocating
        const int bufferSize = 32 * 1024; // 32KB buffer for better performance
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        try
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
        finally
        {
            // Always return the buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    /// <summary>
    /// Asynchronously converts a stream to a byte array,
    /// using pooled buffers for better memory efficiency
    /// </summary>
    /// <returns>Byte array containing stream data</returns>
    /// <param name="input">Input stream</param>
    public static async Task<byte[]> ConvertStreamToByteArrayAsync(Stream input)
    {
        // Try to get stream length to pre-size the MemoryStream if possible
        long? streamLength = null;
        try
        {
            if (input.CanSeek)
            {
                streamLength = input.Length - input.Position;
            }
        }
        catch (NotSupportedException)
        {
            // Some streams don't support seeking or length - that's OK
        }
        
        // Create right-sized memory stream if possible
        using var ms = streamLength.HasValue && streamLength.Value <= int.MaxValue
            ? new MemoryStream((int)streamLength.Value)
            : new MemoryStream();
            
        // Rent a buffer from the shared pool instead of allocating
        const int bufferSize = 32 * 1024; // 32KB buffer for better performance
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await ms.WriteAsync(buffer, 0, read);
            }
            return ms.ToArray();
        }
        finally
        {
            // Always return the buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Utility function renders a hash based on the contents of a (file) stream
    /// </summary>
    /// <param name="stream"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string RenderStreamHash<T>(Stream stream) where T : HashAlgorithm
    {
        StringBuilder sb = new StringBuilder();

        MethodInfo? create = typeof(T).GetMethod("Create", new Type[] { });
        using (T crypt = (T)create.Invoke(null, null))
        {
            byte[] hashBytes = crypt.ComputeHash(stream);
            foreach (byte bt in hashBytes)
            {
                sb.Append(bt.ToString("x2"));
            }
        }
        return sb.ToString();
    }


    /// <summary>
    /// Reads all bytes from a stream into a buffer.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    /// <exception cref="EndOfStreamException"></exception> 
    public static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < count)
        {
            int bytesRead = await stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException();
            }
            totalBytesRead += bytesRead;
        }
    }

}