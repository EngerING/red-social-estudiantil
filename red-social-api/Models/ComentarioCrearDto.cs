namespace RedSocialApi.Models
{
    public class ComentarioCrearDto
    {
        public string PostId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
    }
}