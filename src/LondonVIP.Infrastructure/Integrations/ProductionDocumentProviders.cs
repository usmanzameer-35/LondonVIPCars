using System.Globalization;
using System.Text;
using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Integrations;
using LondonVIP.Shared.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LondonVIP.Infrastructure.Integrations;

public sealed class AzureBlobStorageProvider(IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger<AzureBlobStorageProvider> logger)
    : ProductionProviderBase("azure-blob", IntegrationCategory.Storage, configuration, clients, execution, db, company, logger), IFileStorageProvider
{
    protected override bool IsConfigured => Configured("ContainerUrl", "SasToken");
    private Uri Blob(string path)
    {
        var root = Secret("ContainerUrl")!.TrimEnd('/'); var token = Secret("SasToken")!.TrimStart('?');
        return new Uri($"{root}/{string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString))}?{token}");
    }
    public async Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken token = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Azure Blob Storage is not configured.");
        return await ObserveAsync("Blob.Save", async ct => { using var request = new HttpRequestMessage(HttpMethod.Put, Blob(path)) { Content = new StreamContent(content) }; request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob"); request.Content.Headers.ContentType = new(contentType); using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode(); return path; }, token);
    }
    public async Task<Stream?> OpenReadAsync(string path, CancellationToken token = default)
    {
        if (!IsConfigured) return null;
        return await ObserveAsync("Blob.Read", async ct => { using var response = await Client.GetAsync(Blob(path), HttpCompletionOption.ResponseHeadersRead, ct); if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null; response.EnsureSuccessStatusCode(); var memory = new MemoryStream(); await response.Content.CopyToAsync(memory, ct); memory.Position = 0; return (Stream?)memory; }, token);
    }
    public async Task DeleteAsync(string path, CancellationToken token = default)
    {
        if (!IsConfigured) return;
        await ObserveAsync("Blob.Delete", async ct => { using var response = await Client.DeleteAsync(Blob(path), ct); if (response.StatusCode != System.Net.HttpStatusCode.NotFound) response.EnsureSuccessStatusCode(); return true; }, token);
    }
}

public sealed class ProductionPdfProvider(IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger<ProductionPdfProvider> logger)
    : ProductionProviderBase("pdf", IntegrationCategory.Pdf, configuration, clients, execution, db, company, logger), IPdfGenerationProvider
{
    protected override bool IsConfigured => true;
    public Task<byte[]> GenerateAsync(PdfDocumentType documentType, object model, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var title = documentType switch { PdfDocumentType.Invoice => "INVOICE", PdfDocumentType.Receipt => "RECEIPT", PdfDocumentType.BookingConfirmation => "BOOKING CONFIRMATION", PdfDocumentType.CorporateStatement => "CORPORATE STATEMENT", PdfDocumentType.DriverSummary => "DRIVER MANIFEST", _ => documentType.ToString().ToUpperInvariant() };
        var lines = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }).Split('\n').Select(x => x.Trim().Trim(',', '{', '}', '[', ']', '"')).Where(x => x.Length > 0).Take(34).ToArray();
        return Task.FromResult(SimplePdf.Create(title, "London VIP Cars", lines));
    }

    private static class SimplePdf
    {
        public static byte[] Create(string title, string companyName, IReadOnlyList<string> lines)
        {
            var text = new StringBuilder("BT /F1 10 Tf 50 790 Td ");
            text.Append($"/F1 20 Tf ({Escape(companyName)}) Tj 0 -34 Td /F1 16 Tf ({Escape(title)}) Tj 0 -30 Td /F1 10 Tf ");
            text.Append($"(Generated {DateTimeOffset.UtcNow:dd MMM yyyy HH:mm} UTC) Tj 0 -28 Td ");
            foreach (var line in lines) text.Append($"({Escape(line)}) Tj 0 -16 Td ");
            text.Append("ET");
            var stream = Encoding.ASCII.GetBytes(text.ToString());
            var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>", $"<< /Length {stream.Length} >>\nstream\n{Encoding.ASCII.GetString(stream)}\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
            var output = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
            for (var i = 0; i < objects.Length; i++) { offsets.Add(Encoding.ASCII.GetByteCount(output.ToString())); output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
            var xref = Encoding.ASCII.GetByteCount(output.ToString()); output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n"); foreach (var offset in offsets.Skip(1)) output.Append(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n"); output.Append(CultureInfo.InvariantCulture, $"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
            return Encoding.ASCII.GetBytes(output.ToString());
        }
        private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    }
}
