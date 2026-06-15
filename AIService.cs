using System.Net.Http.Json;

namespace LectorDocumentosIA; // Asegúrate de que este sea el mismo nombre de tu proyecto

// 1. LA INTERFAZ DEBE ESTAR AQUÍ
public interface IAIService
{
    Task<string> GetAnswerAsync(string context, string question);
    Task<string> GetAnswerWithVisionAsync(byte[] imageBytes, string question);
}

// 2. LA CLASE QUE IMPLEMENTA LA INTERFAZ
public class AIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public AIService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Anthropic:ApiKey"] ?? "";
    }

    // 1. MÉTODO NORMAL (TEXTO)
    public async Task<string> GetAnswerAsync(string context, string question)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model = "claude-haiku-4-5",
            max_tokens = 30024,
            messages = new[] {
                new { role = "user", content = $"Documento: {context}\n\nPregunta: {question}" }
            }
        };

        request.Content = JsonContent.Create(body);
        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            using var doc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }
        return "Error en Claude.";
    }

    // 2. MÉTODO DE VISIÓN (IMAGEN) - ESTO ES LO QUE TE FALTA AGREGAR
    public async Task<string> GetAnswerWithVisionAsync(byte[] imageBytes, string question)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        string base64Image = Convert.ToBase64String(imageBytes);

        var body = new
        {
            model = "claude-haiku-4-5",
            max_tokens = 30024,
            messages = new[] {
                new {
                    role = "user",
                    content = new object[] {
                        new {
                            type = "image",
                            source = new {
                                type = "base64",
                                media_type = "image/png",
                                data = base64Image
                            }
                        },
                        new {
                            type = "text",
                            text = $"Pregunta del usuario de ASIMEX sobre este documento visual: {question}"
                        }
                    }
                }
            }
        };

        request.Content = JsonContent.Create(body);
        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            using var doc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        }
        return "Error en Visión.";
    }
}