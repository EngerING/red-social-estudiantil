using Microsoft.AspNetCore.Mvc;
using RedSocialApi.Models;
using RedSocialApi.Services;

namespace RedSocialApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudyGroupsController : ControllerBase
    {
        private readonly FirebaseService _firebaseService;

        public StudyGroupsController(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerGrupos([FromQuery] string? userId)
        {
            var groups = await _firebaseService.ObtenerStudyGroupsAsync(userId);
            return Ok(new { success = true, data = groups });
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> ObtenerGruposPorUsuario(string userId)
        {
            var groups = await _firebaseService.ObtenerStudyGroupsPorUsuarioAsync(userId);
            return Ok(new { success = true, data = groups });
        }

        [HttpPost("{groupId}/join")]
        public async Task<IActionResult> UnirseAGrupo(string groupId, [FromBody] StudyGroupMembershipDto dto)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(dto.UserId))
            {
                return BadRequest(new { success = false, message = "groupId y userId son requeridos" });
            }

            var joined = await _firebaseService.UnirseAStudyGroupAsync(groupId, dto.UserId);
            if (!joined)
            {
                return BadRequest(new { success = false, message = "No se pudo unir al grupo" });
            }

            return Ok(new { success = true, message = "Te uniste al grupo correctamente" });
        }

        [HttpDelete("{groupId}/join")]
        public async Task<IActionResult> SalirDelGrupo(string groupId, [FromQuery] string? userId, [FromBody] StudyGroupMembershipDto? dto)
        {
            var resolvedUserId = !string.IsNullOrWhiteSpace(userId) ? userId : dto?.UserId;
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(resolvedUserId))
            {
                return BadRequest(new { success = false, message = "groupId y userId son requeridos" });
            }

            var left = await _firebaseService.SalirDeStudyGroupAsync(groupId, resolvedUserId);
            if (!left)
            {
                return BadRequest(new { success = false, message = "No se pudo salir del grupo" });
            }

            return Ok(new { success = true, message = "Saliste del grupo correctamente" });
        }

        [HttpGet("{groupId}/messages")]
        public async Task<IActionResult> ObtenerMensajesGrupo(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return BadRequest(new { success = false, message = "groupId es requerido" });
            }

            var messages = await _firebaseService.ObtenerMensajesStudyGroupAsync(groupId);
            return Ok(new { success = true, data = messages });
        }
    }
}
