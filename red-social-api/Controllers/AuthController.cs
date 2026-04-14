using Microsoft.AspNetCore.Mvc;
using RedSocialApi.Models;
using RedSocialApi.Services;

namespace RedSocialApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly FirebaseService _firebaseService;

        public AuthController(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UsuarioRegistroDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre) ||
                string.IsNullOrWhiteSpace(dto.Apellido) ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Faltan campos obligatorios"
                });
            }

            var result = await _firebaseService.RegistrarUsuarioAsync(dto);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UsuarioLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Email y contraseña son requeridos"
                });
            }

            var result = await _firebaseService.LoginAsync(dto);

            if (!result.Success)
            {
                return Unauthorized(result);
            }

            return Ok(result);
        }

        [HttpGet("profile/{userId}")]
        public async Task<IActionResult> GetProfile(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El userId es requerido"
                });
            }

            var perfil = await _firebaseService.ObtenerPerfilAsync(userId);

            if (perfil is null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Usuario no encontrado"
                });
            }

            perfil.FotoPerfilUrl = NormalizePhotoUrl(perfil.FotoPerfilUrl);

            return Ok(new
            {
                success = true,
                data = perfil
            });
        }

        [HttpPost("profile/{userId}/photo")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPhoto(string userId, [FromForm] UploadProfilePhotoDto request)
        {
            if (string.IsNullOrWhiteSpace(userId) || request.Foto is null)
            {
                return BadRequest(new { success = false, message = "userId y foto son requeridos" });
            }

            try
            {
                var url = await _firebaseService.ActualizarFotoPerfilAsync(userId, request.Foto);
                if (string.IsNullOrWhiteSpace(url))
                {
                    return BadRequest(new { success = false, message = "No se pudo actualizar la foto" });
                }

                var fotoPerfilUrl = NormalizePhotoUrl(url);

                return Ok(new { success = true, message = "Foto actualizada", fotoPerfilUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = $"Error al actualizar la foto: {ex.Message}"
                });
            }
        }

        private string NormalizePhotoUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return url.StartsWith("/")
                ? $"{Request.Scheme}://{Request.Host}{url}"
                : url;
        }
    }
}
