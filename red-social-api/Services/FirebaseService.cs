using System.Text;
using System.Text.Json;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using RedSocialApi.Models;

namespace RedSocialApi.Services
{
    public class FirebaseService
    {
        private readonly FirestoreDb _firestore;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public FirebaseService(IConfiguration configuration, HttpClient httpClient)
        {
            var projectId = configuration["Firebase:ProjectId"]
                ?? throw new Exception("Firebase:ProjectId no configurado");

            _apiKey = configuration["Firebase:ApiKey"]
                ?? throw new Exception("Firebase:ApiKey no configurado");

            var dbBuilder = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                Credential = GoogleCredential.FromFile("Firebase/firebase-key.json")
            };

            _firestore = dbBuilder.Build();
            _httpClient = httpClient;
        }

        public async Task<AuthResponseDto> RegistrarUsuarioAsync(UsuarioRegistroDto dto)
        {
            try
            {
                var userRecordArgs = new UserRecordArgs
                {
                    Email = dto.Email,
                    Password = dto.Password,
                    DisplayName = $"{dto.Nombre} {dto.Apellido}"
                };

                var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userRecordArgs);

                var usuario = new Dictionary<string, object>
                {
                    { "uid", userRecord.Uid },
                    { "nombre", dto.Nombre },
                    { "apellido", dto.Apellido },
                    { "email", dto.Email },
                    { "carrera", dto.Carrera },
                    { "fechaCreacion", Timestamp.GetCurrentTimestamp() }
                };

                await _firestore.Collection("usuarios")
                    .Document(userRecord.Uid)
                    .SetAsync(usuario);

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Cuenta creada correctamente",
                    UserId = userRecord.Uid,
                    Email = dto.Email
                };
            }
            catch (Exception ex)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = $"Error al registrar usuario: {ex.Message}"
                };
            }
        }

        public async Task<AuthResponseDto> LoginAsync(UsuarioLoginDto dto)
        {
            try
            {
                var endpoint =
                    $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";

                var payload = new
                {
                    email = dto.Email,
                    password = dto.Password,
                    returnSecureToken = true
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = $"Error Firebase Auth: {responseBody}"
                    };
                }

                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Login exitoso",
                    Token = root.GetProperty("idToken").GetString(),
                    UserId = root.GetProperty("localId").GetString(),
                    Email = root.GetProperty("email").GetString()
                };
            }
            catch (Exception ex)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = $"Error en login: {ex.Message}"
                };
            }
        }

        public async Task<UsuarioPerfilDto?> ObtenerPerfilAsync(string userId)
        {
            var document = await _firestore.Collection("usuarios")
                .Document(userId)
                .GetSnapshotAsync();

            if (!document.Exists)
            {
                return null;
            }

            var data = document.ToDictionary();

            return new UsuarioPerfilDto
            {
                Uid = data.ContainsKey("uid") ? data["uid"]?.ToString() ?? string.Empty : string.Empty,
                Nombre = data.ContainsKey("nombre") ? data["nombre"]?.ToString() ?? string.Empty : string.Empty,
                Apellido = data.ContainsKey("apellido") ? data["apellido"]?.ToString() ?? string.Empty : string.Empty,
                Email = data.ContainsKey("email") ? data["email"]?.ToString() ?? string.Empty : string.Empty,
                Carrera = data.ContainsKey("carrera") ? data["carrera"]?.ToString() ?? string.Empty : string.Empty
            };
        }

       public async Task<bool> CrearPostAsync(PostCrearDto dto)
{
    var perfil = await ObtenerPerfilAsync(dto.UserId);

    if (perfil is null)
    {
        return false;
    }

    var post = new Dictionary<string, object>
    {
        { "userId", dto.UserId },
        { "nombre", perfil.Nombre },
        { "apellido", perfil.Apellido },
        { "contenido", dto.Contenido },
        { "fechaCreacion", Timestamp.GetCurrentTimestamp() },
        { "likesCount", 0 }
    };

    await _firestore.Collection("posts").AddAsync(post);

    return true;
}

       public async Task<List<PostDto>> ObtenerPostsAsync()
{
    var snapshot = await _firestore.Collection("posts")
        .OrderByDescending("fechaCreacion")
        .GetSnapshotAsync();

    var posts = new List<PostDto>();

    foreach (var document in snapshot.Documents)
    {
        var data = document.ToDictionary();

        string fechaTexto = string.Empty;

        if (data.ContainsKey("fechaCreacion") && data["fechaCreacion"] is Timestamp timestamp)
        {
            fechaTexto = timestamp.ToDateTime().ToString("yyyy-MM-dd HH:mm");
        }

        int likesCount = 0;
        if (data.ContainsKey("likesCount") && int.TryParse(data["likesCount"]?.ToString(), out var parsedLikes))
        {
            likesCount = parsedLikes;
        }

        posts.Add(new PostDto
        {
            Id = document.Id,
            UserId = data.ContainsKey("userId") ? data["userId"]?.ToString() ?? string.Empty : string.Empty,
            Nombre = data.ContainsKey("nombre") ? data["nombre"]?.ToString() ?? string.Empty : string.Empty,
            Apellido = data.ContainsKey("apellido") ? data["apellido"]?.ToString() ?? string.Empty : string.Empty,
            Contenido = data.ContainsKey("contenido") ? data["contenido"]?.ToString() ?? string.Empty : string.Empty,
            FechaCreacion = fechaTexto,
            LikesCount = likesCount
        });
    }

    return posts;
}
public async Task<bool> DarLikeAsync(LikeCrearDto dto)
{
    var postRef = _firestore.Collection("posts").Document(dto.PostId);
    var postSnapshot = await postRef.GetSnapshotAsync();

    if (!postSnapshot.Exists)
    {
        return false;
    }

    var likesSnapshot = await _firestore.Collection("likes")
        .WhereEqualTo("postId", dto.PostId)
        .WhereEqualTo("userId", dto.UserId)
        .GetSnapshotAsync();

    if (likesSnapshot.Documents.Count > 0)
    {
        return true;
    }

    var like = new Dictionary<string, object>
    {
        { "postId", dto.PostId },
        { "userId", dto.UserId },
        { "fechaCreacion", Timestamp.GetCurrentTimestamp() }
    };

    await _firestore.Collection("likes").AddAsync(like);

    int likesCount = 0;
    var postData = postSnapshot.ToDictionary();

    if (postData.ContainsKey("likesCount") && int.TryParse(postData["likesCount"]?.ToString(), out var parsedLikes))
    {
        likesCount = parsedLikes;
    }

    await postRef.UpdateAsync("likesCount", likesCount + 1);

    return true;
}
public async Task<bool> CrearComentarioAsync(ComentarioCrearDto dto)
{
    var perfil = await ObtenerPerfilAsync(dto.UserId);

    if (perfil is null)
    {
        return false;
    }

    var postRef = _firestore.Collection("posts").Document(dto.PostId);
    var postSnapshot = await postRef.GetSnapshotAsync();

    if (!postSnapshot.Exists)
    {
        return false;
    }

    var comentario = new Dictionary<string, object>
    {
        { "postId", dto.PostId },
        { "userId", dto.UserId },
        { "nombre", perfil.Nombre },
        { "apellido", perfil.Apellido },
        { "contenido", dto.Contenido },
        { "fechaCreacion", Timestamp.GetCurrentTimestamp() }
    };

    await _firestore.Collection("comentarios").AddAsync(comentario);

    return true;
}
public async Task<List<ComentarioDto>> ObtenerComentariosPorPostAsync(string postId)
{
    var snapshot = await _firestore.Collection("comentarios")
        .WhereEqualTo("postId", postId)
        .GetSnapshotAsync();

    var comentarios = new List<ComentarioDto>();

    foreach (var document in snapshot.Documents)
    {
        var data = document.ToDictionary();

        DateTime fecha = DateTime.MinValue;
        string fechaTexto = string.Empty;

        if (data.ContainsKey("fechaCreacion") && data["fechaCreacion"] is Timestamp timestamp)
        {
            fecha = timestamp.ToDateTime();
            fechaTexto = fecha.ToString("yyyy-MM-dd HH:mm");
        }

        comentarios.Add(new ComentarioDto
        {
            Id = document.Id,
            PostId = data.ContainsKey("postId") ? data["postId"]?.ToString() ?? string.Empty : string.Empty,
            UserId = data.ContainsKey("userId") ? data["userId"]?.ToString() ?? string.Empty : string.Empty,
            Nombre = data.ContainsKey("nombre") ? data["nombre"]?.ToString() ?? string.Empty : string.Empty,
            Apellido = data.ContainsKey("apellido") ? data["apellido"]?.ToString() ?? string.Empty : string.Empty,
            Contenido = data.ContainsKey("contenido") ? data["contenido"]?.ToString() ?? string.Empty : string.Empty,
            FechaCreacion = fechaTexto
        });
    }

    return comentarios
        .OrderBy(c => DateTime.TryParse(c.FechaCreacion, out var f) ? f : DateTime.MinValue)
        .ToList();
}
public async Task<bool> ToggleLikeAsync(ToggleLikeDto dto)
{
    var postRef = _firestore.Collection("posts").Document(dto.PostId);
    var postSnapshot = await postRef.GetSnapshotAsync();

    if (!postSnapshot.Exists)
    {
        return false;
    }

    var likesSnapshot = await _firestore.Collection("likes")
        .WhereEqualTo("postId", dto.PostId)
        .WhereEqualTo("userId", dto.UserId)
        .GetSnapshotAsync();

    var postData = postSnapshot.ToDictionary();
    int likesCount = 0;

    if (postData.ContainsKey("likesCount") &&
        int.TryParse(postData["likesCount"]?.ToString(), out var parsedLikes))
    {
        likesCount = parsedLikes;
    }

    if (likesSnapshot.Documents.Count > 0)
    {
        foreach (var likeDoc in likesSnapshot.Documents)
        {
            await likeDoc.Reference.DeleteAsync();
        }

        likesCount = Math.Max(0, likesCount - 1);
        await postRef.UpdateAsync("likesCount", likesCount);
        return true;
    }

    var like = new Dictionary<string, object>
    {
        { "postId", dto.PostId },
        { "userId", dto.UserId },
        { "fechaCreacion", Timestamp.GetCurrentTimestamp() }
    };

    await _firestore.Collection("likes").AddAsync(like);
    await postRef.UpdateAsync("likesCount", likesCount + 1);

    return true;
}

public async Task<List<string>> ObtenerPostsConLikePorUsuarioAsync(string userId)
{
    var snapshot = await _firestore.Collection("likes")
        .WhereEqualTo("userId", userId)
        .GetSnapshotAsync();

    var likedPostIds = new List<string>();

    foreach (var document in snapshot.Documents)
    {
        var data = document.ToDictionary();

        if (data.ContainsKey("postId"))
        {
            var postId = data["postId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(postId))
            {
                likedPostIds.Add(postId);
            }
        }
    }

    return likedPostIds;
}

    }
    
}