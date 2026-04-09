namespace RedSocialApi.Models
{
    public class ComentarioDto
    {
        public string Id { get; set; } = string.Empty;
        public string PostId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public string FechaCreacion { get; set; } = string.Empty;
    }
}