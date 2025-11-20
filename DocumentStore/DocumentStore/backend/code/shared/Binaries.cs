using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Processing;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities for working with binaries
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Re-usable HTTP client
        /// </summary>
        /// <returns></returns>
        private static HttpClient? _httpBinaryClient = null;

        /// <summary>
        /// Retrieves a binary as a byte array from an HTTP or location on the disk
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static async Task<byte[]> RetrieveBinary(string location)
        {
            if (location.StartsWith("http"))
            {
                _httpBinaryClient ??= CreateSimpleHttp2Client(location);
                using HttpResponseMessage result = await _httpBinaryClient.GetAsync(location);

                if (result.IsSuccessStatusCode)
                {
                    // Use ReadAsStreamAsync for efficient streaming
                    using Stream responseStream = await result.Content.ReadAsStreamAsync();

                    // Get content length if available to pre-size buffer
                    long? contentLength = result.Content.Headers.ContentLength;
                    int initialCapacity = contentLength.HasValue && contentLength.Value < int.MaxValue
                        ? (int)contentLength.Value
                        : 32 * 1024; // 32KB default initial capacity

                    using var memoryStream = new MemoryStream(initialCapacity);

                    // Stream in chunks for better memory efficiency
                    const int bufferSize = 16 * 1024; // 16KB chunks
                    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);

                    try
                    {
                        int bytesRead;
                        while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await memoryStream.WriteAsync(buffer, 0, bytesRead);
                        }

                        return memoryStream.ToArray();
                    }
                    finally
                    {
                        // Return buffer to pool
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (File.Exists(location))
                {
                    // Use stream-based approach with buffer pooling for files too
                    using FileStream fileStream = new FileStream(
                        location,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 16 * 1024, // 16KB buffer
                        useAsync: true);

                    // Get file length to pre-size buffer
                    long fileLength = fileStream.Length;
                    if (fileLength > int.MaxValue)
                    {
                        // For very large files, use chunked reading approach
                        const int chunkSize = 16 * 1024;
                        using var memoryStream = new MemoryStream();
                        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(chunkSize);

                        try
                        {
                            int bytesRead;
                            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await memoryStream.WriteAsync(buffer, 0, bytesRead);
                            }

                            return memoryStream.ToArray();
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                    else
                    {
                        // For typical file sizes, allocate once
                        byte[] buffer = new byte[fileLength];
                        int bytesRead = 0;
                        int totalBytesRead = 0;

                        // Handle inexact reads by reading until we get the full content or reach EOF
                        while (totalBytesRead < fileLength &&
                              (bytesRead = await fileStream.ReadAsync(buffer, totalBytesRead, (int)fileLength - totalBytesRead)) > 0)
                        {
                            totalBytesRead += bytesRead;
                        }

                        // Verify if we got all the expected bytes
                        if (totalBytesRead != fileLength)
                        {
                            // Log warning but still return the buffer with however many bytes we read
                            Console.WriteLine($"Only read {totalBytesRead} of {fileLength} bytes from file");
                        }
                        return buffer;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Retrieves a binary as a byte array from an HTTP endpoint using POST method
        /// </summary>
        /// <param name="url">The URL to post to</param>
        /// <param name="dataToPost">Dictionary of key-value pairs to send in the POST request</param>
        /// <returns>Byte array of the binary data or null if request failed</returns>
        public static async Task<byte[]?> RetrieveBinaryWithPost(string url, Dictionary<string, string> dataToPost)
        {
            try
            {
                _httpBinaryClient ??= CreateSimpleHttp2Client(url);

                // Create form content from dictionary
                var formContent = new FormUrlEncodedContent(dataToPost);

                // Send POST request
                using HttpResponseMessage result = await _httpBinaryClient.PostAsync(url, formContent);

                if (result.IsSuccessStatusCode)
                {
                    using Stream responseStream = await result.Content.ReadAsStreamAsync();

                    // Use the ConvertStreamToByteArray method from Framework for efficient streaming
                    return ConvertStreamToByteArray(responseStream);
                }
                else
                {
                    appLogger.LogWarning($"POST request to {url} failed with status code {result.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error retrieving binary data with POST from {url}");
                return null;
            }
        }

        /// <summary>
        /// Utility function to stream a binary file from the Document Store to the browser
        /// </summary>
        /// <param name="response"></param>
        /// <param name="location"></param>
        /// <param name="displayFileName"></param>
        /// <param name="forceDownload"></param>
        /// <returns></returns>
        public async static Task StreamBinary(HttpResponse response, string location, string displayFileName, bool forceDownload)
        {
            try
            {
                var byteArray = await RetrieveBinary(location);
                if (byteArray == null || byteArray.Length == 0)
                {
                    appLogger.LogError($"No binary data to stream. location: {location}");
                    // Return the broken image binary data
                    await StreamErrorImage();
                }
                else
                {
                    // Return the image as a binary to the client
                    string contentType = GetContentType(Path.GetExtension(location)[1..]);
                    SetHeaders(response, displayFileName, contentType, byteArray.Length, forceDownload);

                    await response.Body.WriteAsync(byteArray);
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error serving the image. location: {location}");
                await StreamErrorImage();
            }

            async Task StreamErrorImage()
            {
                response.ContentType = GetContentType("jpg");
                // Return the broken image binary data
                await response.Body.WriteAsync(BrokenImageBytes);
            }
        }




        /// <summary>
        /// Proxies an image from the disk or another URL to the client
        /// </summary>
        /// <param name="response"></param>
        /// <param name="location"></param>
        /// <param name="Convert"></param>
        /// <returns></returns>
        public async static Task StreamImage(HttpResponse response, string location, bool autoConvert = false)
        {
            try
            {
                string contentType = GetContentType(Path.GetExtension(location)[1..]);
                response.ContentType = contentType;

                if (location.StartsWith("http"))
                {
                    _httpBinaryClient ??= CreateSimpleHttp2Client(location);
                    using var httpResponse = await _httpBinaryClient.GetAsync(location, HttpCompletionOption.ResponseHeadersRead);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        await StreamErrorImage();
                        return;
                    }

                    if (autoConvert && contentType == "image/tiff")
                    {
                        // We need to buffer for conversion
                        using var responseStream = await httpResponse.Content.ReadAsStreamAsync();
                        await ConvertAndStreamTiff(responseStream, response.Body);
                    }
                    else
                    {
                        // Stream directly without buffering the entire content
                        using var responseStream = await httpResponse.Content.ReadAsStreamAsync();
                        await responseStream.CopyToAsync(response.Body);
                    }
                }
                else if (File.Exists(location))
                {
                    if (autoConvert && contentType == "image/tiff")
                    {
                        // Stream with conversion
                        using var fileStream = new FileStream(
                            location,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: 16 * 1024,
                            useAsync: true);

                        await ConvertAndStreamTiff(fileStream, response.Body);
                    }
                    else
                    {
                        // Stream directly from file to response
                        using var fileStream = new FileStream(
                            location,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: 16 * 1024,
                            useAsync: true);

                        await fileStream.CopyToAsync(response.Body);
                    }
                }
                else
                {
                    appLogger.LogError($"File not found: {location}");
                    await StreamErrorImage();
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"There was an error serving the image. location: {location}");
                await StreamErrorImage();
            }

            async Task StreamErrorImage()
            {
                response.ContentType = GetContentType("jpg");
                await response.Body.WriteAsync(BrokenImageBytes);
            }
        }

        /// <summary>
        /// Helper method for TIFF conversion
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="outputStream"></param>
        /// <returns></returns>
        private static async Task ConvertAndStreamTiff(Stream inputStream, Stream outputStream)
        {
            // Load image for conversion
            using var image = await Image.LoadAsync(inputStream);

            // Convert and write directly to output
            await image.SaveAsPngAsync(outputStream);
        }

        /// <summary>
        /// Proxies a thumbnail of an image located on the disk or another URL to the client
        /// </summary>
        /// <param name="response"></param>
        /// <param name="location"></param>
        /// <param name="thumbnailSize"></param>
        /// <returns></returns>
        public async static Task StreamImageThumbnail(HttpResponse response, string location, int thumbnailSize)
        {
            try
            {
                if (location.StartsWith("http"))
                {
                    if (_httpBinaryClient == null) _httpBinaryClient = CreateSimpleHttp2Client(location);
                    using HttpResponseMessage result = await _httpBinaryClient.GetAsync(location);

                    if (result.IsSuccessStatusCode)
                    {
                        using Stream returnStreaam = await result.Content.ReadAsStreamAsync();

                        using var memoryStream = new MemoryStream();
                        returnStreaam.CopyTo(memoryStream);
                        byte[] byteArray = memoryStream.ToArray();

                        // Render a byte array of the thumbnail image
                        var thumbnailBytes = RenderThumbnailImage(byteArray, thumbnailSize, Path.GetExtension(location));

                        // Return the image as a binary to the client
                        response.ContentType = GetContentType(Path.GetExtension(location)[1..]);
                        await response.Body.WriteAsync(thumbnailBytes);
                    }
                    else
                    {
                        HandleError("Unable to find resource", $"location: {location}");
                    }
                }
                else
                {
                    if (File.Exists(location))
                    {
                        // Read the image as a file array
                        byte[] byteArray = await File.ReadAllBytesAsync(location);

                        // Render a byte array of the thumbnail image
                        var thumbnailBytes = RenderThumbnailImage(byteArray, thumbnailSize, Path.GetExtension(location));

                        // Return the image as a binary to the client
                        response.ContentType = GetContentType(Path.GetExtension(location)[1..]);
                        await response.Body.WriteAsync(thumbnailBytes);
                    }
                    else
                    {
                        // TODO: Stream broken image binary?
                        HandleError("Unable to find resource", $"location: {location}");
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("There was an error serving the image", $"error: {ex}");
            }

        }

        /// <summary>
        /// Renders a smaller (thumbnail) version of an image
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <param name="thumbnailSize"></param>
        /// <param name="imageExtension"></param>
        /// <returns></returns>
        public static byte[] RenderThumbnailImage(byte[] imageBytes, int thumbnailSize, string imageExtension)
        {
            if (imageExtension == ".svg") return imageBytes;

            // Create thumbnail using ImageSharp
            using var image = Image.Load(imageBytes);

            if (image.Width > thumbnailSize || image.Height > thumbnailSize)
            {
                var resizeOptions = new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(thumbnailSize, thumbnailSize)
                };

                image.Mutate(x => x.Resize(resizeOptions));

                // Convert the ImageSharp object back to a byte array
                using var memoryStream = new MemoryStream();

                SixLabors.ImageSharp.Formats.IImageEncoder? imageEncoder = null;

                switch (imageExtension[1..])
                {
                    case "jpg":
                    case "jpeg":
                        imageEncoder = new JpegEncoder();
                        break;

                    case "png":
                        imageEncoder = new PngEncoder();
                        break;

                    case "gif":
                        imageEncoder = new GifEncoder();
                        break;

                    case "tif":
                    case "tiff":
                        imageEncoder = new PngEncoder();
                        break;

                    case "svg":
                        imageEncoder = new PngEncoder();
                        break;

                }
                image.Save(memoryStream, imageEncoder);

                return memoryStream.ToArray();
            }

            // No need to scale the image, so we return the original one
            return imageBytes;
        }


        // Static encoders to avoid repeated allocations
        private static readonly JpegEncoder _jpegEncoder = new JpegEncoder();
        private static readonly PngEncoder _pngEncoder = new PngEncoder();
        private static readonly GifEncoder _gifEncoder = new GifEncoder();

        /// <summary>
        /// Lowers the resulution of an image to "compress" it
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <param name="factor"></param>
        /// <param name="imageExtension"></param>
        /// <returns></returns>
        public static byte[] DownSampleImage(byte[] imageBytes, double factor, string imageExtension)
        {
            try
            {
                if (factor >= 1)
                {
                    appLogger.LogWarning($"Unable to downsample image as the factor is {factor}.");
                    return imageBytes;
                }

                // Create thumbnail using ImageSharp
                using var image = Image.Load(imageBytes);

                // Determin the original size of the image
                var originalWidth = image.Width;
                var originalHeight = image.Height;

                var temporaryWidth = (int)Math.Ceiling(originalWidth * factor);
                var temporaryHeight = (int)Math.Ceiling(originalHeight * factor);

                // Sample it down by lowering the width and height of the image
                var resizeOptions = new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(temporaryWidth, temporaryHeight)
                };

                image.Mutate(x => x.Resize(resizeOptions));

                // Adjust DPI to maintain physical dimensions after downsampling
                // When we reduce pixel dimensions by 'factor', we need to increase DPI by 1/factor
                // to maintain the same physical print size
                if (image.Metadata.ResolutionUnits == PixelResolutionUnit.PixelsPerInch)
                {
                    // Image has DPI metadata - adjust it
                    image.Metadata.HorizontalResolution = image.Metadata.HorizontalResolution / factor;
                    image.Metadata.VerticalResolution = image.Metadata.VerticalResolution / factor;
                }
                else
                {
                    // No DPI metadata - set default 96 DPI adjusted by factor
                    image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
                    image.Metadata.HorizontalResolution = 96.0 / factor;
                    image.Metadata.VerticalResolution = 96.0 / factor;
                }

                // XBRL usually only accepts JFIF-encoded images which do not contain Exif and XMP metadata
                image.Metadata.ExifProfile = null;
                image.Metadata.XmpProfile = null;

                // Pre-size the MemoryStream based on input size
                // Estimate that downsampled/upsampled image will be similar or smaller than original
                int estimatedSize = imageBytes.Length;
                using var memoryStream = new MemoryStream(estimatedSize);

                // Use cached encoder instances to avoid repeated allocations
                SixLabors.ImageSharp.Formats.IImageEncoder? imageEncoder = null;

                switch (imageExtension.Substring(1))
                {
                    case "jpg":
                    case "jpeg":
                        imageEncoder = _jpegEncoder;
                        break;

                    case "png":
                        // Use maximum compression for PNG files
                        imageEncoder = new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression };
                        break;

                    case "gif":
                        imageEncoder = _gifEncoder;
                        break;

                }
                image.Save(memoryStream, imageEncoder);

                // Check if the downsampled image is actually smaller than the original
                if (memoryStream.Length >= imageBytes.Length)
                {
                    appLogger.LogInformation($"Downsampled image ({memoryStream.Length} bytes) is not smaller than original ({imageBytes.Length} bytes). Returning original.");
                    return imageBytes;
                }

                // For large images, use buffer pooling for the final byte array
                if (memoryStream.Length > 85000) // Large Object Heap threshold
                {
                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent((int)memoryStream.Length);
                    try
                    {
                        memoryStream.Position = 0;
                        int bytesRead = memoryStream.Read(buffer, 0, (int)memoryStream.Length);
                        var result = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, result, 0, bytesRead);
                        return result;
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                else
                {
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, $"Error downsampling image. factor: {factor}, imageExtension: {imageExtension}");
                appLogger.LogError("Returning original image as downsampling is not supported.");
                return imageBytes;
            }

        }


        /// <summary>
        /// Routine for transforming transparent PNG or GIF images to JPG
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <returns></returns>
        public static byte[] ConvertPngOrGifToJpeg(byte[] imageBytes)
        {
            SixLabors.ImageSharp.Formats.IImageEncoder imageEncoder = new JpegEncoder();

            using var image = Image.Load(imageBytes);

            // Figure out how to deal with transparency

            using var memoryStream = new MemoryStream();
            image.Save(memoryStream, imageEncoder);

            return memoryStream.ToArray();
        }

        public static byte[] ConvertTiffToPng(byte[] imageBytes)
        {
            SixLabors.ImageSharp.Formats.IImageEncoder imageEncoder = new PngEncoder();

            using var image = Image.Load(imageBytes);

            // Figure out how to deal with transparency

            using var memoryStream = new MemoryStream();
            image.Save(memoryStream, imageEncoder);

            return memoryStream.ToArray();
        }

    }
}