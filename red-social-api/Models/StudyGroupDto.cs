namespace RedSocialApi.Models
{
    public class StudyGroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Tema { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int MiembrosCount { get; set; }
        public bool IsMember { get; set; }
    }
}
