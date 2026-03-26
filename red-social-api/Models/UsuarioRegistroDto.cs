namespace RedSocialApi.Models
{
    public class UsuarioRegistroDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
    }
}