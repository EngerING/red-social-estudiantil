namespace RedSocialApi.Models
{
    public class PostDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string FotoPerfilUrl { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public string FechaCreacion { get; set; } = string.Empty;
        public int LikesCount { get; set; }
        public List<PostMediaDto> Media { get; set; } = new();
    }
}
