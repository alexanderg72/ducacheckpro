using ImageMagick;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text;
using System.Linq; 
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims; 
using Microsoft.Data.SqlClient; 
using Microsoft.Extensions.Configuration; 

namespace LectorDocumentosIA
{
    // Modelo de datos para la petición
    public class FileUploadModel
    {
        public List<IFormFile>? Files { get; set; }
        public string Question { get; set; } = string.Empty;
        public string? History { get; set; } // Recibe el historial del navegador
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IAIService _aiService;
        private readonly string _connectionString; // Variable para la conexión SQL

        // Inyectamos la configuración para leer la base de datos
        public DocumentController(IAIService aiService, IConfiguration config)
        {
            _aiService = aiService;
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        [HttpPost("upload-and-query")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndQuery([FromForm] FileUploadModel model)
        {
            StringBuilder contextoTotal = new StringBuilder();

            // 1. Recuperar el historial enviado desde el navegador
            if (!string.IsNullOrEmpty(model.History))
            {
                contextoTotal.AppendLine("--- CONTEXTO DE LA CONVERSACIÓN ANTERIOR ---");
                contextoTotal.AppendLine(model.History);
                contextoTotal.AppendLine("--------------------------------------------\n");
            }

            bool hasFiles = model.Files != null && model.Files.Count > 0;

            // Validamos que haya archivos nuevos O contexto previo para seguir preguntando
            if (!hasFiles && string.IsNullOrEmpty(model.History))
                return BadRequest("No se han seleccionado archivos ni hay un historial previo.");

            if (hasFiles)
            {
                foreach (var file in model.Files!)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    byte[] fileData = ms.ToArray();

                    string textoExtraido = ExtractTextFromPdf(fileData);

                    if (textoExtraido.Contains("ERROR_OCR"))
                    {
                        try
                        {
                            // Configuración de Ghostscript para OCR visual
                            var gsBaseDir = @"C:\Program Files\gs";
                            var gsPath = Directory.GetDirectories(gsBaseDir, "gs*")
                                                  .OrderByDescending(x => x)
                                                  .Select(x => Path.Combine(x, "bin"))
                                                  .FirstOrDefault();

                            if (gsPath != null) MagickNET.SetGhostscriptDirectory(gsPath);

                            using (var images = new MagickImageCollection())
                            {
                                var settings = new MagickReadSettings { Density = new Density(150), FrameIndex = 0, FrameCount = 1 };
                                ms.Position = 0;
                                images.Read(ms, settings);

                                using (var firstPage = images[0])
                                {
                                    firstPage.Format = MagickFormat.Png;
                                    byte[] pngBytes = firstPage.ToByteArray();
                                    var visionText = await _aiService.GetAnswerWithVisionAsync(pngBytes, "Extrae todo el texto legible de este documento.");

                                    contextoTotal.AppendLine($"--- Contenido Visual de: {file.FileName} ---");
                                    contextoTotal.AppendLine(visionText);
                                    contextoTotal.AppendLine("--- Fin del archivo --- \n");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            contextoTotal.AppendLine($"[Error en OCR visual {file.FileName}: {ex.Message}]");
                        }
                    }
                    else
                    {
                        contextoTotal.AppendLine($"--- Contenido de: {file.FileName} ---");
                        contextoTotal.AppendLine(textoExtraido);
                        contextoTotal.AppendLine("--- Fin del archivo --- \n");
                    }
                }
            }

            // 2. Consultar a la IA con todo el contexto acumulado
            var respuestaFinal = await _aiService.GetAnswerAsync(contextoTotal.ToString(), model.Question);

            // ========================================================
            // 3. REGISTRO DE CONSUMO DE TOKENS E HISTORIAL EN SQL
            // ========================================================
            string errorDeBaseDatos = string.Empty;
            try
            {
                string usuarioLogueado = "Usuario Anónimo";

                // Buscamos el nombre de usuario exacto en el token
                var claimUsuario = User.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == ClaimTypes.Name);

                if (claimUsuario != null && !string.IsNullOrEmpty(claimUsuario.Value))
                {
                    usuarioLogueado = claimUsuario.Value;
                }
                else if (User.Identity != null && !string.IsNullOrEmpty(User.Identity.Name))
                {
                    usuarioLogueado = User.Identity.Name;
                }

                // Evitar que guarde el "Rol" por error
                if (usuarioLogueado == "Administrador" || usuarioLogueado == "Usuario")
                {
                    var claimAlternativo = User.Claims.FirstOrDefault(c => c.Type != ClaimTypes.Role && c.Value != usuarioLogueado);
                    if (claimAlternativo != null) usuarioLogueado = claimAlternativo.Value;
                }

                // Calcular tokens usados
                int totalCaracteres = contextoTotal.Length + model.Question.Length + respuestaFinal.Length;
                int tokensEstimados = totalCaracteres / 4;

                // Guardar en la base de datos
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open(); // Abrimos la conexión una sola vez para ambos comandos

                    // 3.1 Registrar consumo de tokens (Tu código original)
                    using (SqlCommand cmdTokens = new SqlCommand("sp_RegistrarConsumo", conn))
                    {
                        cmdTokens.CommandType = System.Data.CommandType.StoredProcedure;
                        cmdTokens.Parameters.AddWithValue("@NombreUsuario", usuarioLogueado);
                        cmdTokens.Parameters.AddWithValue("@Tokens", tokensEstimados);
                        cmdTokens.ExecuteNonQuery();
                    }

                    // 3.2 NUEVO: Registrar el historial de la consulta
                    string queryHistorial = @"
                        INSERT INTO HistorialConsultas (UsuarioId, Pregunta, RespuestaIA, NombresArchivos, FechaConsulta)
                        VALUES (@Usuario, @Pregunta, @Respuesta, @Archivos, GETDATE())";

                    using (SqlCommand cmdHistorial = new SqlCommand(queryHistorial, conn))
                    {
                        cmdHistorial.Parameters.AddWithValue("@Usuario", usuarioLogueado);
                        cmdHistorial.Parameters.AddWithValue("@Pregunta", string.IsNullOrEmpty(model.Question) ? "Análisis de documento sin instrucción específica" : model.Question);
                        cmdHistorial.Parameters.AddWithValue("@Respuesta", respuestaFinal);
                        
                        // Extraer nombres de archivos separados por comas si existen
                        string nombresArchivos = hasFiles ? string.Join(", ", model.Files!.Select(f => f.FileName)) : "Ninguno";
                        cmdHistorial.Parameters.AddWithValue("@Archivos", nombresArchivos);

                        cmdHistorial.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                errorDeBaseDatos = ex.Message;
            }

            // Si hay un error de SQL, lo mostramos al final de la respuesta de la IA
            if (!string.IsNullOrEmpty(errorDeBaseDatos))
            {
                respuestaFinal += $"\n\n\n---\n⚠️ **Nota del Sistema:** El documento se procesó correctamente, pero no se pudo registrar el consumo/historial en SQL Server. El error devuelto fue:\n*\"{errorDeBaseDatos}\"*";
            }

            return Ok(new { answer = respuestaFinal });
        }

        private string ExtractTextFromPdf(byte[] data)
        {
            StringBuilder text = new StringBuilder();
            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(ms)))
                {
                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        text.Append(PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i)));
                    }
                }
                string result = text.ToString();
                return (string.IsNullOrWhiteSpace(result) || result.Length < 10) ? "ERROR_OCR" : result;
            }
            catch { return "ERROR_OCR"; }
        }
    }
}