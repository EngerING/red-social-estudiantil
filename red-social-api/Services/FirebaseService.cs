using System.Text;
using System.Text.Json;
using FirebaseAdmin.Auth;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using RedSocialApi.Models;

namespace RedSocialApi.Services
{
    public class FirebaseService
    {
        private static readonly List<StudyGroupDto> DefaultStudyGroups =
        [
            new() { Id = "programacion", Nombre = "Programacion Colaborativa", Tema = "Tecnologia", Descripcion = "Practica algoritmos, desarrollo web y proyectos en equipo." },
            new() { Id = "matematicas", Nombre = "Matematicas Sin Miedo", Tema = "Matematicas", Descripcion = "Resuelve ejercicios, comparte apuntes y prepara parciales." },
            new() { Id = "diseno", Nombre = "Diseno y Creatividad", Tema = "Diseno", Descripcion = "Habla de UX, interfaces, branding y herramientas visuales." },
            new() { Id = "idiomas", Nombre = "Idiomas en Accion", Tema = "Idiomas", Descripcion = "Practica ingles y otros idiomas con retos semanales." },
            new() { Id = "ciencias", Nombre = "Ciencias y Laboratorio", Tema = "Ciencias", Descripcion = "Comparte recursos de biologia, quimica y experimentacion." },
            new() { Id = "emprendimiento", Nombre = "Ideas y Emprendimiento", Tema = "Negocios", Descripcion = "Conecta con estudiantes que quieren lanzar proyectos o startups." }
        ];

        private readonly FirestoreDb _firestore;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _storageBucket;
        private readonly GoogleCredential _googleCredential;
        private readonly IWebHostEnvironment _environment;

        public FirebaseService(IConfiguration configuration, HttpClient httpClient, IWebHostEnvironment environment)
        {
            var projectId = configuration["Firebase:ProjectId"]
                ?? throw new Exception("Firebase:ProjectId no configurado");

            _apiKey = configuration["Firebase:ApiKey"]
                ?? throw new Exception("Firebase:ApiKey no configurado");

            _storageBucket = configuration["Firebase:StorageBucket"]
                ?? $"{projectId}.appspot.com";

            _googleCredential = GoogleCredential.FromFile("Firebase/firebase-key.json");

            var dbBuilder = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                Credential = _googleCredential
            };

            _firestore = dbBuilder.Build();
            _httpClient = httpClient;
            _environment = environment;
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
                    { "studyGroupIds", new List<string>() },
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
                        Message = ObtenerMensajeLogin(responseBody)
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

        private static string ObtenerMensajeLogin(string responseBody)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                if (root.TryGetProperty("error", out var errorNode) &&
                    errorNode.TryGetProperty("message", out var messageNode))
                {
                    var firebaseCode = messageNode.GetString() ?? string.Empty;

                    if (firebaseCode.Contains("INVALID_LOGIN_CREDENTIALS", StringComparison.OrdinalIgnoreCase) ||
                        firebaseCode.Contains("INVALID_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
                        firebaseCode.Contains("EMAIL_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Credenciales incorrectas";
                    }
                }
            }
            catch
            {
            }

            return "No se pudo iniciar sesion";
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

            var studyGroupIds = data.ContainsKey("studyGroupIds") && data["studyGroupIds"] is IEnumerable<object> rawGroups
                ? rawGroups.Select(group => group?.ToString() ?? string.Empty).Where(group => !string.IsNullOrWhiteSpace(group)).ToList()
                : new List<string>();

            var studyGroupNames = await ObtenerNombresGruposAsync(studyGroupIds);

            return new UsuarioPerfilDto
            {
                Uid = data.ContainsKey("uid") ? data["uid"]?.ToString() ?? string.Empty : string.Empty,
                Nombre = data.ContainsKey("nombre") ? data["nombre"]?.ToString() ?? string.Empty : string.Empty,
                Apellido = data.ContainsKey("apellido") ? data["apellido"]?.ToString() ?? string.Empty : string.Empty,
                Email = data.ContainsKey("email") ? data["email"]?.ToString() ?? string.Empty : string.Empty,
                Carrera = data.ContainsKey("carrera") ? data["carrera"]?.ToString() ?? string.Empty : string.Empty,
                FotoPerfilUrl = data.ContainsKey("fotoPerfilUrl") ? data["fotoPerfilUrl"]?.ToString() ?? string.Empty : string.Empty,
                StudyGroupIds = studyGroupIds,
                StudyGroupNames = studyGroupNames
            };
        }

        public async Task<bool> CrearPostAsync(PostCrearDto dto)
        {
            var perfil = await ObtenerPerfilAsync(dto.UserId);

            if (perfil is null)
            {
                return false;
            }

            var media = await GuardarMediaPostAsync(dto.UserId, dto.Archivos);

            var post = new Dictionary<string, object>
            {
                { "userId", dto.UserId },
                { "nombre", perfil.Nombre },
                { "apellido", perfil.Apellido },
                { "fotoPerfilUrl", perfil.FotoPerfilUrl },
                { "contenido", dto.Contenido },
                { "media", media.Select(item => new Dictionary<string, object>
                    {
                        { "url", item.Url },
                        { "tipo", item.Tipo }
                    }).ToList()
                },
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
                    FotoPerfilUrl = data.ContainsKey("fotoPerfilUrl") ? data["fotoPerfilUrl"]?.ToString() ?? string.Empty : string.Empty,
                    Contenido = data.ContainsKey("contenido") ? data["contenido"]?.ToString() ?? string.Empty : string.Empty,
                    FechaCreacion = fechaTexto,
                    LikesCount = likesCount,
                    Media = ParseMedia(data)
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

            var extension = Path.GetExtension(foto.FileName);
            var fileName = $"profile-photos/{userId}/{Guid.NewGuid()}{extension}";
            var downloadToken = Guid.NewGuid().ToString();
            string fotoUrl;

            try
            {
                var storage = await StorageClient.CreateAsync(_googleCredential);
                await using var stream = foto.OpenReadStream();

                var obj = new Google.Apis.Storage.v1.Data.Object
                {
                    Bucket = _storageBucket,
                    Name = fileName,
                    ContentType = foto.ContentType,
                    Metadata = new Dictionary<string, string>
                    {
                        { "firebaseStorageDownloadTokens", downloadToken }
                    }
                };

                await storage.UploadObjectAsync(obj, stream);

                var encodedPath = Uri.EscapeDataString(fileName);
                fotoUrl = $"https://firebasestorage.googleapis.com/v0/b/{_storageBucket}/o/{encodedPath}?alt=media&token={downloadToken}";
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var relativePath = await GuardarFotoLocalAsync(userId, foto, extension);
                fotoUrl = $"/{relativePath.Replace("\\", "/")}";
            }

            await userRef.UpdateAsync("fotoPerfilUrl", fotoUrl);

            return fotoUrl;
        }

        private async Task<string> GuardarFotoLocalAsync(string userId, IFormFile foto, string extension)
        {
            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var relativeDirectory = Path.Combine("profile-photos", userId);
            var absoluteDirectory = Path.Combine(webRootPath, relativeDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            var localFileName = $"{Guid.NewGuid()}{extension}";
            var absolutePath = Path.Combine(absoluteDirectory, localFileName);

            await using var localStream = new FileStream(absolutePath, FileMode.Create);
            await using var uploadedStream = foto.OpenReadStream();
            await uploadedStream.CopyToAsync(localStream);

            return Path.Combine(relativeDirectory, localFileName);
        }

        private async Task<List<PostMediaDto>> GuardarMediaPostAsync(string userId, List<IFormFile>? archivos)
        {
            if (archivos is null || archivos.Count == 0)
            {
                return [];
            }

            var media = new List<PostMediaDto>();
            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var relativeDirectory = Path.Combine("post-media", userId, Guid.NewGuid().ToString());
            var absoluteDirectory = Path.Combine(webRootPath, relativeDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            foreach (var archivo in archivos.Where(file => file is not null && file.Length > 0).Take(4))
            {
                var extension = Path.GetExtension(archivo.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var absolutePath = Path.Combine(absoluteDirectory, fileName);

                await using var stream = new FileStream(absolutePath, FileMode.Create);
                await using var uploadedStream = archivo.OpenReadStream();
                await uploadedStream.CopyToAsync(stream);

                var relativePath = Path.Combine(relativeDirectory, fileName).Replace("\\", "/");
                media.Add(new PostMediaDto
                {
                    Url = $"/{relativePath}",
                    Tipo = archivo.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "video" : "image"
                });
            }

            return media;
        }

        private static List<PostMediaDto> ParseMedia(Dictionary<string, object> data)
        {
            if (!data.ContainsKey("media") || data["media"] is not IEnumerable<object> rawMedia)
            {
                return [];
            }

            var media = new List<PostMediaDto>();
            foreach (var item in rawMedia)
            {
                if (item is not Dictionary<string, object> mediaItem)
                {
                    continue;
                }

                media.Add(new PostMediaDto
                {
                    Url = mediaItem.ContainsKey("url") ? mediaItem["url"]?.ToString() ?? string.Empty : string.Empty,
                    Tipo = mediaItem.ContainsKey("tipo") ? mediaItem["tipo"]?.ToString() ?? string.Empty : string.Empty
                });
            }

            return media;
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

        public async Task EnsureStudyGroupsSeededAsync()
        {
            foreach (var group in DefaultStudyGroups)
            {
                var groupRef = _firestore.Collection("study_groups").Document(group.Id);
                var snapshot = await groupRef.GetSnapshotAsync();
                if (snapshot.Exists)
                {
                    continue;
                }

                await groupRef.SetAsync(new Dictionary<string, object>
                {
                    { "nombre", group.Nombre },
                    { "tema", group.Tema },
                    { "descripcion", group.Descripcion },
                    { "miembrosCount", 0 }
                });
            }
        }

        public async Task<List<StudyGroupDto>> ObtenerStudyGroupsAsync(string? userId = null)
        {
            var memberGroupIds = string.IsNullOrWhiteSpace(userId)
                ? new HashSet<string>()
                : new HashSet<string>(await ObtenerStudyGroupIdsPorUsuarioAsync(userId));

            var snapshot = await _firestore.Collection("study_groups").GetSnapshotAsync();

            return snapshot.Documents
                .Select(document =>
                {
                    var data = document.ToDictionary();
                    var count = data.ContainsKey("miembrosCount") && int.TryParse(data["miembrosCount"]?.ToString(), out var parsedCount)
                        ? parsedCount
                        : 0;

                    return new StudyGroupDto
                    {
                        Id = document.Id,
                        Nombre = data.ContainsKey("nombre") ? data["nombre"]?.ToString() ?? string.Empty : string.Empty,
                        Tema = data.ContainsKey("tema") ? data["tema"]?.ToString() ?? string.Empty : string.Empty,
                        Descripcion = data.ContainsKey("descripcion") ? data["descripcion"]?.ToString() ?? string.Empty : string.Empty,
                        MiembrosCount = count,
                        IsMember = memberGroupIds.Contains(document.Id)
                    };
                })
                .OrderBy(group => group.Tema)
                .ThenBy(group => group.Nombre)
                .ToList();
        }

        public async Task<List<StudyGroupDto>> ObtenerStudyGroupsPorUsuarioAsync(string userId)
        {
            var allGroups = await ObtenerStudyGroupsAsync(userId);
            return allGroups.Where(group => group.IsMember).ToList();
        }

        public async Task<bool> UnirseAStudyGroupAsync(string groupId, string userId)
        {
            var groupRef = _firestore.Collection("study_groups").Document(groupId);
            var userRef = _firestore.Collection("usuarios").Document(userId);

            var groupSnapshot = await groupRef.GetSnapshotAsync();
            var userSnapshot = await userRef.GetSnapshotAsync();

            if (!groupSnapshot.Exists || !userSnapshot.Exists)
            {
                return false;
            }

            var existingGroupIds = await ObtenerStudyGroupIdsPorUsuarioAsync(userId);
            if (existingGroupIds.Contains(groupId))
            {
                return true;
            }

            existingGroupIds.Add(groupId);

            await userRef.UpdateAsync("studyGroupIds", existingGroupIds);
            await groupRef.UpdateAsync("miembrosCount", FieldValue.Increment(1));
            return true;
        }

        public async Task<bool> SalirDeStudyGroupAsync(string groupId, string userId)
        {
            var groupRef = _firestore.Collection("study_groups").Document(groupId);
            var userRef = _firestore.Collection("usuarios").Document(userId);

            var groupSnapshot = await groupRef.GetSnapshotAsync();
            var userSnapshot = await userRef.GetSnapshotAsync();

            if (!groupSnapshot.Exists || !userSnapshot.Exists)
            {
                return false;
            }

            var existingGroupIds = await ObtenerStudyGroupIdsPorUsuarioAsync(userId);
            if (!existingGroupIds.Contains(groupId))
            {
                return true;
            }

            existingGroupIds.Remove(groupId);
            await userRef.UpdateAsync("studyGroupIds", existingGroupIds);

            var currentCount = groupSnapshot.TryGetValue<int>("miembrosCount", out var count) ? count : 0;
            await groupRef.UpdateAsync("miembrosCount", Math.Max(0, currentCount - 1));
            return true;
        }

        public async Task<StudyGroupMessageDto?> CrearStudyGroupMessageAsync(CrearStudyGroupMessageDto dto)
        {
            if (!await UsuarioPerteneceAStudyGroupAsync(dto.UserId, dto.GroupId))
            {
                return null;
            }

            var perfil = await ObtenerPerfilAsync(dto.UserId);
            if (perfil is null)
            {
                return null;
            }

            var data = new Dictionary<string, object>
            {
                { "groupId", dto.GroupId },
                { "userId", dto.UserId },
                { "nombreCompleto", $"{perfil.Nombre} {perfil.Apellido}" },
                { "contenido", dto.Contenido },
                { "fechaCreacion", Timestamp.GetCurrentTimestamp() }
            };

            var created = await _firestore.Collection("study_group_messages").AddAsync(data);
            var snapshot = await created.GetSnapshotAsync();
            var createdData = snapshot.ToDictionary();

            return new StudyGroupMessageDto
            {
                Id = created.Id,
                GroupId = dto.GroupId,
                UserId = dto.UserId,
                NombreCompleto = createdData.ContainsKey("nombreCompleto") ? createdData["nombreCompleto"]?.ToString() ?? string.Empty : string.Empty,
                Contenido = createdData.ContainsKey("contenido") ? createdData["contenido"]?.ToString() ?? string.Empty : string.Empty,
                FechaCreacion = createdData.ContainsKey("fechaCreacion") && createdData["fechaCreacion"] is Timestamp ts
                    ? ts.ToDateTime().ToString("yyyy-MM-dd HH:mm")
                    : string.Empty
            };
        }

        public async Task<List<StudyGroupMessageDto>> ObtenerMensajesStudyGroupAsync(string groupId, int limite = 100)
        {
            var snapshot = await _firestore.Collection("study_group_messages")
                .WhereEqualTo("groupId", groupId)
                .GetSnapshotAsync();

            return snapshot.Documents.Select(document =>
            {
                var data = document.ToDictionary();
                return new StudyGroupMessageDto
                {
                    Id = document.Id,
                    GroupId = groupId,
                    UserId = data.ContainsKey("userId") ? data["userId"]?.ToString() ?? string.Empty : string.Empty,
                    NombreCompleto = data.ContainsKey("nombreCompleto") ? data["nombreCompleto"]?.ToString() ?? string.Empty : string.Empty,
                    Contenido = data.ContainsKey("contenido") ? data["contenido"]?.ToString() ?? string.Empty : string.Empty,
                    FechaCreacion = data.ContainsKey("fechaCreacion") && data["fechaCreacion"] is Timestamp ts
                        ? ts.ToDateTime().ToString("yyyy-MM-dd HH:mm")
                        : string.Empty
                };
            })
            .OrderBy(message => DateTime.TryParse(message.FechaCreacion, out var parsedDate) ? parsedDate : DateTime.MinValue)
            .Take(limite)
            .ToList();
        }

        public async Task<bool> UsuarioPerteneceAStudyGroupAsync(string userId, string groupId)
        {
            var groupIds = await ObtenerStudyGroupIdsPorUsuarioAsync(userId);
            return groupIds.Contains(groupId);
        }

        private async Task<List<string>> ObtenerStudyGroupIdsPorUsuarioAsync(string userId)
        {
            var userSnapshot = await _firestore.Collection("usuarios").Document(userId).GetSnapshotAsync();
            if (!userSnapshot.Exists)
            {
                return [];
            }

            var data = userSnapshot.ToDictionary();
            return data.ContainsKey("studyGroupIds") && data["studyGroupIds"] is IEnumerable<object> rawGroups
                ? rawGroups.Select(group => group?.ToString() ?? string.Empty).Where(group => !string.IsNullOrWhiteSpace(group)).Distinct().ToList()
                : [];
        }

        private async Task<List<string>> ObtenerNombresGruposAsync(IEnumerable<string> groupIds)
        {
            var ids = groupIds.Where(groupId => !string.IsNullOrWhiteSpace(groupId)).Distinct().ToList();
            if (ids.Count == 0)
            {
                return [];
            }

            var names = new List<string>();
            foreach (var groupId in ids)
            {
                var snapshot = await _firestore.Collection("study_groups").Document(groupId).GetSnapshotAsync();
                if (!snapshot.Exists)
                {
                    continue;
                }

                var data = snapshot.ToDictionary();
                var name = data.ContainsKey("nombre") ? data["nombre"]?.ToString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }
    }
}
