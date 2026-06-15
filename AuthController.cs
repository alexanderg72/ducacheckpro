using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace LectorDocumentosIA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_ValidarLogin", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@NombreUsuario", request.Username);
                    cmd.Parameters.AddWithValue("@PasswordHash", request.Password);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string nombreUsuarioDb = reader.GetString(1);
                            string rolDb = reader.GetString(2);

                            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
                            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                            var token = new JwtSecurityToken(
                                issuer: _configuration["Jwt:Issuer"],
                                audience: _configuration["Jwt:Audience"],
                                claims: new[] {
                                    // ========================================================
                                    // SOLUCIÓN: Agregamos las etiquetas exactas que el sistema busca
                                    // ========================================================
                                    new Claim(ClaimTypes.Name, nombreUsuarioDb),
                                    new Claim("unique_name", nombreUsuarioDb),
                                    new Claim(ClaimTypes.Role, rolDb)
                                },
                                expires: DateTime.Now.AddHours(2),
                                signingCredentials: creds
                            );

                            return Ok(new
                            {
                                Token = new JwtSecurityTokenHandler().WriteToken(token),
                                Rol = rolDb
                            });
                        }
                        return Unauthorized(new { Mensaje = "Credenciales incorrectas" });
                    }
                }
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}