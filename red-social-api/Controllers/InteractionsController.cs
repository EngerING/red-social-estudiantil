using Microsoft.AspNetCore.Mvc;
using RedSocialApi.Models;
using RedSocialApi.Services;

namespace RedSocialApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InteractionsController : ControllerBase
    {
        private readonly FirebaseService _firebaseService;

        public InteractionsController(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        [HttpPost("toggle-like")]
        public async Task<IActionResult> ToggleLike([FromBody] ToggleLikeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PostId) || string.IsNullOrWhiteSpace(dto.UserId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "PostId y UserId son requeridos"
                });
            }

            var result = await _firebaseService.ToggleLikeAsync(dto);

            if (!result)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No se pudo procesar el like"
                });
            }

            return Ok(new
            {
                success = true,
                message = "Like actualizado correctamente"
            });
        }

        [HttpGet("liked/{userId}")]
        public async Task<IActionResult> ObtenerLikesPorUsuario(string userId)
        {
            var likedPosts = await _firebaseService.ObtenerPostsConLikePorUsuarioAsync(userId);

            return Ok(new
            {
                success = true,
                data = likedPosts
            });
        }

        [HttpPost("comment")]
        public async Task<IActionResult> CrearComentario([FromBody] ComentarioCrearDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PostId) ||
                string.IsNullOrWhiteSpace(dto.UserId) ||
                string.IsNullOrWhiteSpace(dto.Contenido))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "PostId, UserId y contenido son requeridos"
                });
            }

            var result = await _firebaseService.CrearComentarioAsync(dto);

            if (!result)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No se pudo crear el comentario"
                });
            }

            return Ok(new
            {
                success = true,
                message = "Comentario creado correctamente"
            });
        }

        [HttpGet("comments/{postId}")]
        public async Task<IActionResult> ObtenerComentarios(string postId)
        {
            var comentarios = await _firebaseService.ObtenerComentariosPorPostAsync(postId);

            return Ok(new
            {
                success = true,
                data = comentarios
            });
        }
    }
}