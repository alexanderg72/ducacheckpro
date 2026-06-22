using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System;
using System.Collections.Generic;

namespace LectorDocumentosIA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Administrador")] // <-- IMPORTANTE: Protege TODO el controlador
    public class AdminController : ControllerBase
    {
        private readonly string _connectionString;

        public AdminController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        }

        [HttpGet("usuarios")]
        public IActionResult GetUsers()
        {
            try
            {
                var usuarios = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ObtenerUsuarios", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                usuarios.Add(new
                                {
                                    Id = reader["Id"] != DBNull.Value ? reader["Id"] : 0,
                                    Nombre = reader["NombreUsuario"] != DBNull.Value ? reader["NombreUsuario"].ToString() : "Desconocido",
                                    Rol = reader["Rol"] != DBNull.Value ? reader["Rol"].ToString() : "Sin Rol",
                                    Estado = reader["Estado"] != DBNull.Value ? Convert.ToBoolean(reader["Estado"]) : false,
                                    Creado = reader["FechaCreacion"] != DBNull.Value ? Convert.ToDateTime(reader["FechaCreacion"]).ToString("yyyy-MM-dd HH:mm") : "N/A"
                                });
                            }
                        }
                    }
                }
                return Ok(usuarios);
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { Mensaje = "Error de SQL al obtener usuarios", Detalle = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Mensaje = "Error interno", Detalle = ex.Message });
            }
        }

        [HttpPost("crear-usuario")]
        public IActionResult CreateUser([FromBody] UserCreateRequest req)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_CrearUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@NombreUsuario", req.Username ?? "");
                        cmd.Parameters.AddWithValue("@PasswordHash", req.Password ?? "");
                        cmd.Parameters.AddWithValue("@Rol", req.Role ?? "Usuario");

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok(new { Mensaje = "Usuario creado exitosamente" });
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { Mensaje = "Error de SQL al crear usuario", Detalle = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Mensaje = "Error interno", Detalle = ex.Message });
            }
        }

        // ==========================================
        // FUNCIONES DE ADMINISTRACIÓN DE ESTADO/ROL/CLAVE
        // ==========================================

        [HttpPut("cambiar-estado")]
        public IActionResult CambiarEstado([FromBody] UserStatusRequest req)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_CambiarEstadoUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Id", req.Id);
                        cmd.Parameters.AddWithValue("@Estado", req.Estado);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok(new { Mensaje = "Estado del usuario actualizado." });
            }
            catch (SqlException sqlEx) { return StatusCode(500, new { Mensaje = "Error SQL", Detalle = sqlEx.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Mensaje = "Error interno", Detalle = ex.Message }); }
        }

        [HttpPut("editar-rol")]
        public IActionResult EditarRol([FromBody] UserRoleRequest req)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_EditarRolUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Id", req.Id);
                        cmd.Parameters.AddWithValue("@Rol", req.Role ?? "Usuario");

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok(new { Mensaje = "Rol del usuario actualizado." });
            }
            catch (SqlException sqlEx) { return StatusCode(500, new { Mensaje = "Error SQL", Detalle = sqlEx.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Mensaje = "Error interno", Detalle = ex.Message }); }
        }

        [HttpPut("cambiar-clave")]
        public IActionResult CambiarClave([FromBody] UserPasswordRequest req)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    string query = "UPDATE Usuarios SET PasswordHash = @Password WHERE Id = @Id";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", req.Id);
                        cmd.Parameters.AddWithValue("@Password", req.NuevaClave ?? "");

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok(new { Mensaje = "Contraseña del usuario actualizada." });
            }
            catch (SqlException sqlEx) { return StatusCode(500, new { Mensaje = "Error SQL", Detalle = sqlEx.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Mensaje = "Error interno", Detalle = ex.Message }); }
        }

        // ==========================================
        // FUNCIÓN PARA REPORTE DE TOKENS
        // ==========================================

        [HttpGet("reporte-tokens")]
        public IActionResult ObtenerReporteTokens()
        {
            try
            {
                var reporte = new List<object>();
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ObtenerReporteTokens", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                reporte.Add(new
                                {
                                    Usuario = reader["NombreUsuario"] != DBNull.Value ? reader["NombreUsuario"].ToString() : "Desconocido",
                                    TotalTokens = reader["TotalTokens"] != DBNull.Value ? Convert.ToInt32(reader["TotalTokens"]) : 0,
                                    Consultas = reader["CantidadConsultas"] != DBNull.Value ? Convert.ToInt32(reader["CantidadConsultas"]) : 0,
                                    UltimaActividad = reader["UltimaConsulta"] != DBNull.Value ? Convert.ToDateTime(reader["UltimaConsulta"]).ToString("yyyy-MM-dd HH:mm") : "Sin actividad"
                                });
                            }
                        }
                    }
                }
                return Ok(reporte);
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { Mensaje = "Error de SQL al obtener reporte", Detalle = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Mensaje = "Error interno", Detalle = ex.Message });
            }
        }

        // ==========================================
        // NUEVO: HISTORIAL DE CONSULTAS DE LA IA
        // ==========================================

        [HttpGet("historial-usuarios")]
        public IActionResult ObtenerHistorialUsuarios()
        {
            var historial = new List<object>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    string query = @"
                        SELECT Id, UsuarioId, Pregunta, RespuestaIA, NombresArchivos, FechaConsulta 
                        FROM HistorialConsultas 
                        ORDER BY FechaConsulta DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                historial.Add(new
                                {
                                    Id = reader.GetInt32(0),
                                    UsuarioId = reader.GetString(1),
                                    Pregunta = reader.GetString(2),
                                    RespuestaIA = reader.GetString(3),
                                    NombresArchivos = reader.IsDBNull(4) ? "Ninguno" : reader.GetString(4),
                                    FechaConsulta = reader.GetDateTime(5)
                                });
                            }
                        }
                    }
                }
                return Ok(historial);
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { Mensaje = "Error de SQL al obtener el historial", Detalle = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Mensaje = "Error interno", Detalle = ex.Message });
            }
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