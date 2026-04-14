using Microsoft.AspNetCore.Http;

namespace RedSocialApi.Models
{
    public class PostCrearFormDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public List<IFormFile>? Archivos { get; set; }
    }
}
