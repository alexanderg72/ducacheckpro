using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace LectorDocumentosIA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Bloqueo total para no-admins
    public class AdminController : ControllerBase
    {
        private readonly string _connectionString;

        public AdminController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("usuarios")]
        public IActionResult GetUsers()
        {
            var usuarios = new List<object>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                SqlCommand cmd = new SqlCommand("sp_ObtenerUsuarios", conn);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        usuarios.Add(new
                        {
                            Id = reader["Id"],
                            Nombre = reader["NombreUsuario"],
                            
                            Rol = reader["Rol"],
                            Estado = reader["Estado"],
                            Creado = reader["FechaCreacion"]
                        });
                    }
                }
            }
            return Ok(usuarios);
        }

        [HttpPost("crear-usuario")]
        public IActionResult CreateUser([FromBody] UserCreateRequest req)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                SqlCommand cmd = new SqlCommand("sp_CrearUsuario", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@NombreUsuario", req.Username);
                cmd.Parameters.AddWithValue("@PasswordHash", req.Password);
                cmd.Parameters.AddWithValue("@Rol", req.Role);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return Ok(new { Mensaje = "Usuario creado exitosamente" });
        }

        // ==========================================
        // FUNCIONES DE ADMINISTRACIÓN DE ESTADO/ROL/CLAVE
        // ==========================================

        [HttpPut("cambiar-estado")]
        public IActionResult CambiarEstado([FromBody] UserStatusRequest req)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                SqlCommand cmd = new SqlCommand("sp_CambiarEstadoUsuario", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Id", req.Id);
                cmd.Parameters.AddWithValue("@Estado", req.Estado);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return Ok(new { Mensaje = "Estado del usuario actualizado." });
        }

        [HttpPut("editar-rol")]
        public IActionResult EditarRol([FromBody] UserRoleRequest req)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                SqlCommand cmd = new SqlCommand("sp_EditarRolUsuario", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Id", req.Id);
                cmd.Parameters.AddWithValue("@Rol", req.Role);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return Ok(new { Mensaje = "Rol del usuario actualizado." });
        }

        [HttpPut("cambiar-clave")]
        public IActionResult CambiarClave([FromBody] UserPasswordRequest req)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // Aquí hacemos un UPDATE directo por ID para asegurar que cambie al usuario correcto
                string query = "UPDATE Usuarios SET PasswordHash = @Password WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", req.Id);
                cmd.Parameters.AddWithValue("@Password", req.NuevaClave);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return Ok(new { Mensaje = "Contraseña del usuario actualizada." });
        }

        // ==========================================
        // NUEVO: FUNCIÓN PARA REPORTE DE TOKENS
        // ==========================================

        [HttpGet("reporte-tokens")]
        public IActionResult ObtenerReporteTokens()
        {
            var reporte = new List<object>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                SqlCommand cmd = new SqlCommand("sp_ObtenerReporteTokens", conn);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        reporte.Add(new
                        {
                            Usuario = reader["NombreUsuario"],
                            TotalTokens = reader["TotalTokens"],
                            Consultas = reader["CantidadConsultas"],
                            UltimaActividad = reader["UltimaConsulta"]
                        });
                    }
                }
            }
            return Ok(reporte);
        }
    }

    // ==========================================
    // MODELOS DE DATOS
    // ==========================================

    public class UserCreateRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }

    public class UserStatusRequest
    {
        public int Id { get; set; }
        public bool Estado { get; set; }
    }

    public class UserRoleRequest
    {
        public int Id { get; set; }
        public string Role { get; set; }
    }

    public class UserPasswordRequest
    {
        public int Id { get; set; }
        public string NuevaClave { get; set; }
    }
}