namespace CccdCheckIn.Reader.Serial;

/// <summary>
/// Ghi log chẩn đoán ra Data\reader.log cạnh exe — dùng để gỡ lỗi tại máy khách:
/// cổng nào được mở, có byte tới không, dòng nào đã phát. Không ném — log hỏng
/// không được chặn reader. Không ghi nội dung payload (PII), chỉ độ dài.
/// </summary>
internal static class DiagnosticLog
{
    private static readonly object Lock = new();

    private static string LogPath => Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
        "Data", "reader.log");

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Log phụ — hỏng không chặn.
        }
    }
}