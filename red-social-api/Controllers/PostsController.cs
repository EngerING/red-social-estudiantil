using Microsoft.AspNetCore.Mvc;
using RedSocialApi.Models;
using RedSocialApi.Services;

namespace RedSocialApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly FirebaseService _firebaseService;

        public PostsController(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CrearPost([FromForm] PostCrearFormDto dto)
        {
            var hasFiles = dto.Archivos?.Count > 0;

            if (string.IsNullOrWhiteSpace(dto.UserId) || (!hasFiles && string.IsNullOrWhiteSpace(dto.Contenido)))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "UserId y contenido o archivos son requeridos"
                });
            }

            var creado = await _firebaseService.CrearPostAsync(new PostCrearDto
            {
                UserId = dto.UserId,
                Contenido = dto.Contenido,
                Archivos = dto.Archivos
            });

            if (!creado)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No se pudo crear el post"
                });
            }

            return Ok(new
            {
                success = true,
                message = "Post creado correctamente"
            });
        }

        [HttpPut("{postId}")]
        public async Task<IActionResult> EditarPost(string postId, [FromBody] ActualizarPostDto dto)
        {
            if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.Contenido))
            {
                return BadRequest(new { success = false, message = "postId, userId y contenido son requeridos" });
            }

            var actualizado = await _firebaseService.ActualizarPostAsync(postId, dto);
            if (!actualizado)
            {
                return BadRequest(new { success = false, message = "No se pudo editar el post" });
            }

            return Ok(new { success = true, message = "Post editado correctamente" });
        }

        [HttpDelete("{postId}")]
        public async Task<IActionResult> EliminarPost(string postId, [FromQuery] string? userId, [FromBody] EliminarRecursoDto? dto)
        {
            var resolvedUserId = !string.IsNullOrWhiteSpace(userId) ? userId : dto?.UserId;

            if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(resolvedUserId))
            {
                return BadRequest(new { success = false, message = "postId y userId son requeridos" });
            }

            var eliminado = await _firebaseService.EliminarPostAsync(postId, resolvedUserId);
            if (!eliminado)
            {
                return BadRequest(new { success = false, message = "No se pudo eliminar el post" });
            }

            return Ok(new { success = true, message = "Post eliminado correctamente" });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPosts()
        {
            var posts = await _firebaseService.ObtenerPostsAsync();
            var normalizedPosts = posts.Select(post =>
            {
                post.FotoPerfilUrl = NormalizeUrl(post.FotoPerfilUrl);
                post.Media = post.Media.Select(media => new PostMediaDto
                {
                    Url = NormalizeUrl(media.Url),
                    Tipo = media.Tipo
                }).ToList();

                return post;
            }).ToList();

            return Ok(new
            {
                success = true,
                data = normalizedPosts
            });
        }

        private string NormalizeUrl(string? url)
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
