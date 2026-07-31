using System.Collections.Concurrent;
using System.Security.Cryptography;
using RemoteControlLAN.Agent.Security;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Agent.Transfers;

public sealed class FileTransferService(PathGuard paths)
{
    private const int ChunkSize = 64 * 1024;
    private readonly ConcurrentDictionary<string, UploadState> _uploads = new();
    public async IAsyncEnumerable<MessageEnvelope> DownloadAsync(DownloadFilePayload request, string sessionId, string agentId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var path = paths.ResolveAllowedPath(request.Path); var info = new FileInfo(path); if (!info.Exists) throw new FileNotFoundException("Không tìm thấy file.");
        var total = (int)Math.Ceiling(info.Length / (double)ChunkSize); var index = 0; using var sha = SHA256.Create(); await using var stream = File.OpenRead(path); var buffer = new byte[ChunkSize];
        int read; while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0) { sha.TransformBlock(buffer, 0, read, null, 0); yield return MessageEnvelope.Create("FILE_CHUNK", "FILE_CHUNK", new FileChunkPayload { TransferId = request.TransferId, ChunkIndex = index++, TotalChunks = total, DataBase64 = Convert.ToBase64String(buffer, 0, read) }, sessionId, agentId); }
        sha.TransformFinalBlock([], 0, 0); yield return MessageEnvelope.Create("RESPONSE", "FILE_TRANSFER_COMPLETE", new FileTransferCompletePayload { TransferId = request.TransferId, Success = true, Sha256 = Convert.ToHexString(sha.Hash!), Message = "Tải file hoàn tất." }, sessionId, agentId);
    }
    public Task<UploadFileInitResultPayload> BeginUploadAsync(UploadFileInitPayload request)
    {
        try { var path = paths.ResolveAllowedChild(request.TargetPath, request.FileName); if (request.TotalChunks <= 0 || request.TotalChunks > 100_000) throw new InvalidOperationException("Số chunk không hợp lệ."); var temp = path + ".part"; _uploads[request.TransferId] = new UploadState(path, temp, request.TotalChunks, request.Sha256); return Task.FromResult(new UploadFileInitResultPayload { TransferId = request.TransferId, Accepted = true, Message = "Sẵn sàng nhận file." }); }
        catch (Exception) { return Task.FromResult(new UploadFileInitResultPayload { TransferId = request.TransferId, Accepted = false, Message = "Đường dẫn hoặc yêu cầu upload không hợp lệ." }); }
    }
    public async Task<UploadFileResultPayload?> WriteChunkAsync(UploadFileChunkPayload request, CancellationToken cancellationToken)
    {
        if (!_uploads.TryGetValue(request.TransferId, out var state) || request.ChunkIndex != state.NextChunk) throw new InvalidOperationException("Chunk không hợp lệ hoặc sai thứ tự.");
        var bytes = Convert.FromBase64String(request.DataBase64); await using (var stream = new FileStream(state.TempPath, FileMode.Append, FileAccess.Write, FileShare.None, 64 * 1024, true)) await stream.WriteAsync(bytes, cancellationToken); state.NextChunk++;
        if (state.NextChunk < state.TotalChunks) return null;
        _uploads.TryRemove(request.TransferId, out _); await using var completedStream = File.OpenRead(state.TempPath); var actual = Convert.ToHexString(await SHA256.HashDataAsync(completedStream, cancellationToken));
        if (!actual.Equals(state.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) { File.Delete(state.TempPath); return new UploadFileResultPayload { TransferId = request.TransferId, Success = false, Message = "Checksum không khớp; file đã bị hủy." }; }
        File.Move(state.TempPath, state.FinalPath, true); return new UploadFileResultPayload { TransferId = request.TransferId, Success = true, Message = "Upload hoàn tất." };
    }
    private sealed class UploadState(string finalPath, string tempPath, int totalChunks, string expectedSha256) { public string FinalPath { get; } = finalPath; public string TempPath { get; } = tempPath; public int TotalChunks { get; } = totalChunks; public string ExpectedSha256 { get; } = expectedSha256; public int NextChunk { get; set; } }
}
