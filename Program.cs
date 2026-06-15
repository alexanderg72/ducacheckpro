using LectorDocumentosIA;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurar opciones para archivos por defecto
DefaultFilesOptions options = new DefaultFilesOptions();
options.DefaultFileNames.Clear();
options.DefaultFileNames.Add("login.html"); // Establece login.html como prioridad

// 1. REGISTRO DE SERVICIOS (Dependency Injection)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Autenticación JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwtOptions =>
    {
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? ""))
        };
    });

// Registro de servicios propios
builder.Services.AddHttpClient<IAIService, AIService>();
builder.Services.AddMemoryCache();

// Habilitar CORS
builder.Services.AddCors(corsOptions => {
    corsOptions.AddPolicy("AllowFrontend", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// 2. CONFIGURACIÓN DEL MIDDLEWARE (El orden es vital)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 2.1 Primero servimos el Frontend (HTML, CSS, JS)
app.UseDefaultFiles(options);
app.UseStaticFiles();

// 2.2 Aplicamos política de seguridad de red
app.UseCors("AllowFrontend");

// ==========================================
// 2.3 EL ORDEN DE ESTOS 3 ES OBLIGATORIO
// ==========================================
app.UseAuthentication(); // PASO 1: "Saca tu pase" (Lee el JWT)
app.UseAuthorization();  // PASO 2: "Revisa los permisos"
app.MapControllers();    // PASO 3: "Entra a la función"

app.Run();