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

            return Ok(new
            {
                success = true,
                data = perfil
            });
        }
    }
}
