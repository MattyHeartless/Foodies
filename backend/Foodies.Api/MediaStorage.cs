using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;

namespace Foodies.Api;

public sealed class MediaStorage(IConfiguration config, IWebHostEnvironment environment) {
    public const long MaxBytes = 5 * 1024 * 1024;
    readonly string root = Path.GetFullPath(config["Media:StoragePath"] ?? "App_Data/media", environment.ContentRootPath);
    static readonly Regex FileName = new(@"^[a-f0-9]{32}\.(jpg|png|webp)$", RegexOptions.CultureInvariant);
    static readonly Regex Reference = new(@"^/api/v1/media/stores/([a-f0-9]{32})/([a-f0-9]{32}\.(?:jpg|png|webp))$", RegexOptions.CultureInvariant);

    public static bool IsManagedUrl(string url) => Reference.IsMatch(url);
    public static void ValidateReference(string? url, Guid storeId) {
        if (url is null || !url.Trim().StartsWith('/')) return;
        var match = Reference.Match(url.Trim());
        if (!match.Success || match.Groups[1].Value != storeId.ToString("N"))
            throw new ApiError(400, "invalid_cover", "La imagen subida no pertenece a esta tienda.");
    }
    string DirectoryFor(Guid storeId) => Path.Combine(root, "stores", storeId.ToString("N"));

    public async Task<object> Upload(Guid storeId, HttpRequest request, CancellationToken ct) {
        if (request.ContentLength > MaxBytes + 1024 * 1024) throw new ApiError(413, "image_too_large", "La imagen debe pesar como máximo 5 MB.");
        var limit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = MaxBytes + 1024 * 1024;
        if (!request.HasFormContentType) throw new ApiError(400, "image_required", "Selecciona una imagen JPG, PNG o WebP.");
        IFormCollection form;
        try { form = await request.ReadFormAsync(ct); }
        catch (InvalidDataException) { throw new ApiError(413, "image_too_large", "La imagen debe pesar como máximo 5 MB."); }
        if (form.Files.Count != 1 || form.Files.GetFile("file") is not { } file || file.Length == 0)
            throw new ApiError(400, "image_required", "Selecciona una sola imagen.");
        if (file.Length > MaxBytes) throw new ApiError(413, "image_too_large", "La imagen debe pesar como máximo 5 MB.");
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var extension = DetectImage(bytes);
        var name = Guid.NewGuid().ToString("N") + extension;
        var target = Path.Combine(DirectoryFor(storeId), name);
        try {
            Directory.CreateDirectory(DirectoryFor(storeId));
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await output.WriteAsync(bytes, ct);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            TryRemove(target);
            throw new ApiError(503,"image_storage_unavailable","No se pudo guardar la imagen. Revisa el almacenamiento de imágenes o intenta de nuevo.");
        } catch { TryRemove(target); throw; }
        return new { url = $"/api/v1/media/stores/{storeId:N}/{name}" };
    }
    static void TryRemove(string path) {
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    static string DetectImage(byte[] data) {
        if (data.Length >= 33 && data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) && data.AsSpan(12,4).SequenceEqual("IHDR"u8)) {
            var width = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16,4));
            var height = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20,4));
            if (width is 0 or > 8000 || height is 0 or > 8000 || (long)width * height > 32000000)
                throw new ApiError(400,"image_dimensions","Usa una imagen de hasta 8000 px por lado y 32 megapíxeles.");
            if (data.Length >= 45 && data.AsSpan(data.Length-8,4).SequenceEqual("IEND"u8)) return ".png";
        }
        if (data.Length >= 32 && data[0]==255 && data[1]==216 && data[2]==255 && data[^2]==255 && data[^1]==217) return ".jpg";
        if (data.Length >= 30 && data.AsSpan(0,4).SequenceEqual("RIFF"u8) && data.AsSpan(8,4).SequenceEqual("WEBP"u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4,4)) == data.Length-8
            && (data.AsSpan(12,4).SequenceEqual("VP8 "u8) || data.AsSpan(12,4).SequenceEqual("VP8L"u8) || data.AsSpan(12,4).SequenceEqual("VP8X"u8))) return ".webp";
        throw new ApiError(415,"invalid_image","El archivo debe ser una imagen JPG, PNG o WebP. No se permiten SVG.");
    }

    public IResult Read(Guid storeId, string fileName, HttpContext ctx) {
        if (!FileName.IsMatch(fileName)) return Results.NotFound();
        var path = Path.Combine(DirectoryFor(storeId), fileName);
        if (!File.Exists(path)) return Results.NotFound();
        ctx.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        ctx.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        var contentType = Path.GetExtension(fileName) switch { ".jpg"=>"image/jpeg", ".png"=>"image/png", _=>"image/webp" };
        return Results.File(path, contentType, enableRangeProcessing:true);
    }
}
