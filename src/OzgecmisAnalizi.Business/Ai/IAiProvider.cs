namespace OzgecmisAnalizi.Business.Ai;

public interface IAiProvider
{
    bool IsEnabled { get; }

    /// <summary>Görünen ad, örn. "Gemini (gemini-2.0-flash)".</summary>
    string Name { get; }

    /// <summary>Hata durumunda null döner; iş akışı yapay zeka olmadan devam eder.</summary>
    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, bool jsonOutput, CancellationToken ct = default);
}

public sealed class NullAiProvider : IAiProvider
{
    public bool IsEnabled => false;
    public string Name => "Kural motoru";
    public Task<string?> CompleteAsync(string systemPrompt, string userPrompt, bool jsonOutput, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);
}
