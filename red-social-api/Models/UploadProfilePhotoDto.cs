using Microsoft.AspNetCore.Http;

namespace RedSocialApi.Models
{
    public class UploadProfilePhotoDto
    {
        public IFormFile? Foto { get; set; }
    }
}
