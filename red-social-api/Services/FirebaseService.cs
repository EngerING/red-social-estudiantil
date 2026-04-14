using System.Text;
using System.Text.Json;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using RedSocialApi.Models;

namespace RedSocialApi.Services
{
    public class FirebaseService
    {
        private readonly FirestoreDb _firestore;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _storageBucket;

        public FirebaseService(IConfiguration configuration, HttpClient httpClient)
        {
            var projectId = configuration["Firebase:ProjectId"]
                ?? throw new Exception("Firebase:ProjectId no configurado");

            _apiKey = configuration["Firebase:ApiKey"]
                ?? throw new Exception("Firebase:ApiKey no configurado");

            _storageBucket = configuration["Firebase:StorageBucket"]
                ?? $"{projectId}.appspot.com";

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
                    { "fotoPerfilUrl", string.Empty },
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
                Carrera = data.ContainsKey("carrera") ? data["carrera"]?.ToString() ?? string.Empty : string.Empty,
                FotoPerfilUrl = data.ContainsKey("fotoPerfilUrl") ? data["fotoPerfilUrl"]?.ToString() ?? string.Empty : string.Empty
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

        public async Task<bool> ActualizarPostAsync(string postId, ActualizarPostDto dto)
        {
            var postRef = _firestore.Collection("posts").Document(postId);
            var snapshot = await postRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return false;
            }

            var data = snapshot.ToDictionary();
            if (!data.ContainsKey("userId") || data["userId"]?.ToString() != dto.UserId)
            {
                return false;
            }

            await postRef.UpdateAsync(new Dictionary<string, object>
            {
                { "contenido", dto.Contenido },
                { "fechaActualizacion", Timestamp.GetCurrentTimestamp() }
            });

            return true;
        }

        public async Task<bool> EliminarPostAsync(string postId, string userId)
        {
            var postRef = _firestore.Collection("posts").Document(postId);
            var snapshot = await postRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return false;
            }

            var data = snapshot.ToDictionary();
            if (!data.ContainsKey("userId") || data["userId"]?.ToString() != userId)
            {
                return false;
            }

            await postRef.DeleteAsync();

            var likesSnapshot = await _firestore.Collection("likes")
                .WhereEqualTo("postId", postId)
                .GetSnapshotAsync();

            foreach (var like in likesSnapshot.Documents)
            {
                await like.Reference.DeleteAsync();
            }

            var comentariosSnapshot = await _firestore.Collection("comentarios")
                .WhereEqualTo("postId", postId)
                .GetSnapshotAsync();

            foreach (var comentario in comentariosSnapshot.Documents)
            {
                await comentario.Reference.DeleteAsync();
            }

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

        public async Task<bool> ActualizarComentarioAsync(string comentarioId, ActualizarComentarioDto dto)
        {
            var comentarioRef = _firestore.Collection("comentarios").Document(comentarioId);
            var snapshot = await comentarioRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return false;
            }

            var data = snapshot.ToDictionary();
            if (!data.ContainsKey("userId") || data["userId"]?.ToString() != dto.UserId)
            {
                return false;
            }

            await comentarioRef.UpdateAsync(new Dictionary<string, object>
            {
                { "contenido", dto.Contenido },
                { "fechaActualizacion", Timestamp.GetCurrentTimestamp() }
            });

            return true;
        }

        public async Task<bool> EliminarComentarioAsync(string comentarioId, string userId)
        {
            var comentarioRef = _firestore.Collection("comentarios").Document(comentarioId);
            var snapshot = await comentarioRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return false;
            }

            var data = snapshot.ToDictionary();
            if (!data.ContainsKey("userId") || data["userId"]?.ToString() != userId)
            {
                return false;
            }

            await comentarioRef.DeleteAsync();
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
                string fechaTexto = string.Empty;

                if (data.ContainsKey("fechaCreacion") && data["fechaCreacion"] is Timestamp timestamp)
                {
                    fechaTexto = timestamp.ToDateTime().ToString("yyyy-MM-dd HH:mm");
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

        public async Task<string?> ActualizarFotoPerfilAsync(string userId, IFormFile foto)
        {
            var userRef = _firestore.Collection("usuarios").Document(userId);
            var userSnapshot = await userRef.GetSnapshotAsync();

            if (!userSnapshot.Exists)
            {
                return null;
            }

            var storage = await StorageClient.CreateAsync();
            var extension = Path.GetExtension(foto.FileName);
            var fileName = $"profile-photos/{userId}/{Guid.NewGuid()}{extension}";
            var downloadToken = Guid.NewGuid().ToString();

            await using var stream = foto.OpenReadStream();

            var obj = await storage.UploadObjectAsync(_storageBucket, fileName, foto.ContentType, stream,
                new UploadObjectOptions
                {
                    PredefinedAcl = PredefinedObjectAcl.Private
                });

            obj.Metadata ??= new Dictionary<string, string>();
            obj.Metadata["firebaseStorageDownloadTokens"] = downloadToken;
            await storage.UpdateObjectAsync(obj);

            var encodedPath = Uri.EscapeDataString(fileName);
            var fotoUrl = $"https://firebasestorage.googleapis.com/v0/b/{_storageBucket}/o/{encodedPath}?alt=media&token={downloadToken}";

            await userRef.UpdateAsync("fotoPerfilUrl", fotoUrl);

            return fotoUrl;
        }

        public async Task<ChatMessageDto?> CrearChatMessageAsync(CrearChatMessageDto dto)
        {
            var perfil = await ObtenerPerfilAsync(dto.UserId);
            if (perfil is null)
            {
                return null;
            }

            var data = new Dictionary<string, object>
            {
                { "userId", dto.UserId },
                { "nombreCompleto", $"{perfil.Nombre} {perfil.Apellido}" },
                { "contenido", dto.Contenido },
                { "fechaCreacion", Timestamp.GetCurrentTimestamp() }
            };

            var created = await _firestore.Collection("chat_global").AddAsync(data);
            var snapshot = await created.GetSnapshotAsync();
            var createdData = snapshot.ToDictionary();

            return new ChatMessageDto
            {
                Id = created.Id,
                UserId = dto.UserId,
                NombreCompleto = createdData.ContainsKey("nombreCompleto") ? createdData["nombreCompleto"]?.ToString() ?? string.Empty : string.Empty,
                Contenido = createdData.ContainsKey("contenido") ? createdData["contenido"]?.ToString() ?? string.Empty : string.Empty,
                FechaCreacion = createdData.ContainsKey("fechaCreacion") && createdData["fechaCreacion"] is Timestamp ts
                    ? ts.ToDateTime().ToString("yyyy-MM-dd HH:mm")
                    : string.Empty
            };
        }

        public async Task<List<ChatMessageDto>> ObtenerMensajesChatAsync(int limite = 100)
        {
            var snapshot = await _firestore.Collection("chat_global")
                .OrderBy("fechaCreacion")
                .Limit(limite)
                .GetSnapshotAsync();

            return snapshot.Documents.Select(document =>
            {
                var data = document.ToDictionary();

                return new ChatMessageDto
                {
                    Id = document.Id,
                    UserId = data.ContainsKey("userId") ? data["userId"]?.ToString() ?? string.Empty : string.Empty,
                    NombreCompleto = data.ContainsKey("nombreCompleto") ? data["nombreCompleto"]?.ToString() ?? string.Empty : string.Empty,
                    Contenido = data.ContainsKey("contenido") ? data["contenido"]?.ToString() ?? string.Empty : string.Empty,
                    FechaCreacion = data.ContainsKey("fechaCreacion") && data["fechaCreacion"] is Timestamp ts
                        ? ts.ToDateTime().ToString("yyyy-MM-dd HH:mm")
                        : string.Empty
                };
            }).ToList();
        }
    }
}
