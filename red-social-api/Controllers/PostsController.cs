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
        public async Task<IActionResult> CrearPost([FromBody] PostCrearDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.Contenido))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "UserId y contenido son requeridos"
                });
            }

            var creado = await _firebaseService.CrearPostAsync(dto);

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

        [HttpGet]
        public async Task<IActionResult> ObtenerPosts()
        {
            var posts = await _firebaseService.ObtenerPostsAsync();

            return Ok(new
            {
                success = true,
                data = posts
            });
        }
    }
}