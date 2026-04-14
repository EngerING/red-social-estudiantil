const API_BASE = "http://localhost:5000/api";
const HUB_URL = "http://localhost:5000/hubs/chat";

const token = localStorage.getItem("token");
const userId = localStorage.getItem("userId");

let likedPosts = [];
let chatConnection = null;
let editingPostId = null;
let editingCommentId = null;
let deleteState = null;

if (!token || !userId) {
  window.location.href = "login.html";
}

function escapeHtml(value = "") {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function setPostMessage(message, isError = false) {
  const messageNode = document.getElementById("postMessage");
  if (!messageNode) return;

  messageNode.textContent = message;
  messageNode.style.color = isError ? "#b33b3b" : "#666";
}

function setChatStatus(message, type = "") {
  const statusNode = document.getElementById("chatStatus");
  if (!statusNode) return;

  statusNode.textContent = message;
  statusNode.className = `chat-status${type ? ` ${type}` : ""}`;
}

function getChatSendButton() {
  return document.getElementById("btnSendChat");
}

function updateChatSendState() {
  const button = getChatSendButton();
  if (!button) return;

  const isConnected = chatConnection?.state === signalR.HubConnectionState.Connected;
  button.disabled = !isConnected;
}

function togglePostEditor(postId) {
  editingPostId = editingPostId === postId ? null : postId;
  deleteState = null;
  cargarPosts();
}

function toggleCommentEditor(commentId) {
  editingCommentId = editingCommentId === commentId ? null : commentId;
  deleteState = null;
  cargarPosts();
}

function toggleDeleteConfirm(type, id, postId = null) {
  if (deleteState?.type === type && deleteState?.id === id) {
    deleteState = null;
  } else {
    deleteState = { type, id, postId };
  }

  cargarPosts();
}

async function parseResponse(response) {
  try {
    return await response.json();
  } catch {
    return { success: false, message: "Respuesta no valida del servidor" };
  }
}

function bindMenuNavigation() {
  document.querySelectorAll("[data-link]").forEach((button) => {
    button.addEventListener("click", () => {
      const link = button.dataset.link;
      if (link) {
        window.location.href = link;
      }
    });
  });

  document.querySelectorAll("[data-scroll-target]").forEach((button) => {
    button.addEventListener("click", () => {
      const targetId = button.dataset.scrollTarget;
      const target = targetId ? document.getElementById(targetId) : null;
      target?.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  });
}

async function cargarLikesDelUsuario() {
  const response = await fetch(`${API_BASE}/Interactions/liked/${userId}`);
  const data = await parseResponse(response);
  likedPosts = response.ok && data.success ? data.data || [] : [];
}

async function crearPost() {
  const contenidoInput = document.getElementById("postContenido");
  const mediaInput = document.getElementById("postMediaInput");
  const mediaInfo = document.getElementById("postMediaInfo");
  const contenido = contenidoInput.value.trim();
  const archivos = Array.from(mediaInput?.files || []).slice(0, 4);
  if (!contenido && !archivos.length) return;

  const formData = new FormData();
  formData.append("userId", userId);
  formData.append("contenido", contenido);
  archivos.forEach((archivo) => formData.append("archivos", archivo));

  const response = await fetch(`${API_BASE}/Posts`, {
    method: "POST",
    body: formData
  });

  const data = await parseResponse(response);
  setPostMessage(data.message || "Operacion completada", !response.ok || !data.success);

  if (response.ok && data.success) {
    contenidoInput.value = "";
    if (mediaInput) {
      mediaInput.value = "";
    }
    if (mediaInfo) {
      mediaInfo.textContent = "Puedes subir hasta 4 archivos.";
    }
    await cargarPosts();
  }
}

async function guardarEdicionPost(postId) {
  const input = document.getElementById(`editPostInput-${postId}`);
  const contenido = input?.value.trim() || "";
  if (!contenido) {
    alert("La publicacion no puede quedar vacia.");
    return;
  }

  const response = await fetch(`${API_BASE}/Posts/${postId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, contenido })
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    alert(data.message || "No se pudo actualizar la publicacion.");
    return;
  }

  editingPostId = null;
  setPostMessage("Publicacion actualizada correctamente.");
  await cargarPosts();
}

async function eliminarPost(postId) {
  const response = await fetch(`${API_BASE}/Posts/${postId}?userId=${encodeURIComponent(userId)}`, {
    method: "DELETE"
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    alert(data.message || "No se pudo eliminar la publicacion.");
    return;
  }

  deleteState = null;
  setPostMessage("Publicacion eliminada correctamente.");
  await cargarPosts();
}

async function toggleLike(postId) {
  const response = await fetch(`${API_BASE}/Interactions/toggle-like`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ postId, userId })
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    alert(data.message || "No se pudo actualizar el like.");
    return;
  }

  await cargarPosts();
}

async function crearComentario(postId) {
  const input = document.getElementById(`commentInput-${postId}`);
  const contenido = input?.value.trim() || "";
  if (!contenido) return;

  const response = await fetch(`${API_BASE}/Interactions/comment`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ postId, userId, contenido })
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    alert(data.message || "No se pudo crear el comentario.");
    return;
  }

  input.value = "";
  await cargarPosts();
}

async function guardarEdicionComentario(comentarioId) {
  const input = document.getElementById(`editCommentInput-${comentarioId}`);
  const contenido = input?.value.trim() || "";
  if (!contenido) {
    alert("El comentario no puede quedar vacio.");
    return;
  }

  const response = await fetch(`${API_BASE}/Interactions/comment/${comentarioId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, contenido })
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    alert(data.message || "No se pudo editar el comentario.");
    return;
  }

  editingCommentId = null;
  await cargarPosts();
}

async function eliminarComentario(comentarioId) {
  const response = await fetch(`${API_BASE}/Interactions/comment/${comentarioId}?userId=${encodeURIComponent(userId)}`, {
    method: "DELETE"
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    alert(data.message || "No se pudo eliminar el comentario.");
    return;
  }

  deleteState = null;
  await cargarPosts();
}

function renderDeletePanel(type, id) {
  const isOpen = deleteState?.type === type && deleteState?.id === id;
  const label = type === "post" ? "esta publicacion" : "este comentario";
  const confirmClass = type === "post" ? "confirm-delete-post-btn" : "confirm-delete-comment-btn";

  return `
    <div class="action-panel danger-panel" ${isOpen ? "" : "hidden"}>
      <p class="action-panel-title">Eliminar ${label}</p>
      <p class="action-panel-text">Esta accion no se puede deshacer. Confirma si deseas continuar.</p>
      <div class="edit-actions">
        <button class="mini-btn secondary cancel-delete-btn" data-type="${type}" data-id="${id}">Cancelar</button>
        <button class="mini-btn danger ${confirmClass}" data-id="${id}">Eliminar</button>
      </div>
    </div>
  `;
}

function renderPostEditor(post) {
  const isOpen = editingPostId === post.id;

  return `
    <div class="action-panel edit-panel" ${isOpen ? "" : "hidden"}>
      <label for="editPostInput-${post.id}">Modificar publicacion</label>
      <textarea id="editPostInput-${post.id}" class="edit-textarea">${escapeHtml(post.contenido)}</textarea>
      <div class="edit-actions">
        <button class="mini-btn secondary cancel-edit-post-btn" data-post-id="${post.id}">Cancelar</button>
        <button class="mini-btn save-edit-post-btn" data-post-id="${post.id}">Guardar cambios</button>
      </div>
    </div>
  `;
}

function renderPostMedia(media = []) {
  if (!media.length) {
    return "";
  }

  return `
    <div class="post-media-grid">
      ${media.map((item) => `
        <div class="post-media-item">
          ${item.tipo === "video"
            ? `<video controls preload="metadata" src="${escapeHtml(item.url)}"></video>`
            : `<img src="${escapeHtml(item.url)}" alt="Archivo multimedia de la publicación" />`}
        </div>
      `).join("")}
    </div>
  `;
}

function hasVideoMedia(media = []) {
  return media.some((item) => item.tipo === "video");
}

function renderCommentEditor(comentario) {
  const isOpen = editingCommentId === comentario.id;

  return `
    <div class="action-panel edit-panel" ${isOpen ? "" : "hidden"}>
      <label for="editCommentInput-${comentario.id}">Modificar comentario</label>
      <textarea id="editCommentInput-${comentario.id}" class="edit-textarea">${escapeHtml(comentario.contenido)}</textarea>
      <div class="edit-actions">
        <button class="mini-btn secondary cancel-edit-comment-btn" data-comment-id="${comentario.id}">Cancelar</button>
        <button class="mini-btn save-edit-comment-btn" data-comment-id="${comentario.id}">Guardar cambios</button>
      </div>
    </div>
  `;
}

function bindPostEvents() {
  document.querySelectorAll(".like-btn").forEach((button) => {
    button.addEventListener("click", () => toggleLike(button.dataset.postId));
  });

  document.querySelectorAll(".comment-btn").forEach((button) => {
    button.addEventListener("click", () => crearComentario(button.dataset.postId));
  });

  document.querySelectorAll(".edit-post-btn").forEach((button) => {
    button.addEventListener("click", () => togglePostEditor(button.dataset.postId));
  });

  document.querySelectorAll(".delete-post-btn").forEach((button) => {
    button.addEventListener("click", () => toggleDeleteConfirm("post", button.dataset.postId));
  });

  document.querySelectorAll(".save-edit-post-btn").forEach((button) => {
    button.addEventListener("click", () => guardarEdicionPost(button.dataset.postId));
  });

  document.querySelectorAll(".cancel-edit-post-btn").forEach((button) => {
    button.addEventListener("click", () => {
      editingPostId = null;
      cargarPosts();
    });
  });

  document.querySelectorAll(".confirm-delete-post-btn").forEach((button) => {
    button.addEventListener("click", () => eliminarPost(button.dataset.id));
  });

  document.querySelectorAll(".cancel-delete-btn").forEach((button) => {
    button.addEventListener("click", () => {
      if (deleteState?.type === button.dataset.type && deleteState?.id === button.dataset.id) {
        deleteState = null;
        cargarPosts();
      }
    });
  });
}

function bindCommentEvents() {
  document.querySelectorAll(".edit-comment-btn").forEach((button) => {
    button.addEventListener("click", () => toggleCommentEditor(button.dataset.commentId));
  });

  document.querySelectorAll(".delete-comment-btn").forEach((button) => {
    button.addEventListener("click", () => toggleDeleteConfirm("comment", button.dataset.commentId));
  });

  document.querySelectorAll(".save-edit-comment-btn").forEach((button) => {
    button.addEventListener("click", () => guardarEdicionComentario(button.dataset.commentId));
  });

  document.querySelectorAll(".cancel-edit-comment-btn").forEach((button) => {
    button.addEventListener("click", () => {
      editingCommentId = null;
      cargarPosts();
    });
  });

  document.querySelectorAll(".confirm-delete-comment-btn").forEach((button) => {
    button.addEventListener("click", () => eliminarComentario(button.dataset.id));
  });
}

async function cargarComentarios(postId) {
  const response = await fetch(`${API_BASE}/Interactions/comments/${postId}`);
  const data = await parseResponse(response);
  const container = document.getElementById(`comments-${postId}`);
  if (!container) return;

  container.innerHTML = "";

  const comentarios = response.ok && data.success ? data.data || [] : [];
  if (!comentarios.length) {
    container.innerHTML = `<p class="no-comments">No hay comentarios todavia.</p>`;
    return;
  }

  comentarios.forEach((comentario) => {
    const acciones = comentario.userId === userId
      ? `
        <div class="row-actions">
          <button class="mini-btn edit-comment-btn" data-comment-id="${comentario.id}">Editar</button>
          <button class="mini-btn danger delete-comment-btn" data-comment-id="${comentario.id}">Eliminar</button>
        </div>
        ${renderCommentEditor(comentario)}
        ${renderDeletePanel("comment", comentario.id)}
      `
      : "";

    const item = document.createElement("div");
    item.className = "comment-item";
    item.innerHTML = `
      <strong>${escapeHtml(comentario.nombre)} ${escapeHtml(comentario.apellido)}</strong>
      <p class="post-content">${escapeHtml(comentario.contenido)}</p>
      <p class="comment-date">${escapeHtml(comentario.fechaCreacion || "")}</p>
      ${acciones}
    `;

    container.appendChild(item);
  });
}

async function cargarPosts() {
  await cargarLikesDelUsuario();

  const response = await fetch(`${API_BASE}/Posts`);
  const data = await parseResponse(response);
  const container = document.getElementById("postsContainer");
  if (!container) return;

  container.innerHTML = "";

  const posts = response.ok && data.success ? data.data || [] : [];
  if (!posts.length) {
    container.innerHTML = `<article class="post-card"><p>No hay publicaciones todavia.</p></article>`;
    return;
  }

  posts.forEach((post) => {
    const yaDioLike = likedPosts.includes(post.id);
    const contieneVideo = hasVideoMedia(post.media || []);
    const accionesPost = post.userId === userId
      ? `
        <div class="row-actions">
          <button class="mini-btn edit-post-btn" data-post-id="${post.id}">Editar</button>
          <button class="mini-btn danger delete-post-btn" data-post-id="${post.id}">Eliminar</button>
        </div>
        ${renderPostEditor(post)}
        ${renderDeletePanel("post", post.id)}
      `
      : "";

    const article = document.createElement("article");
    article.className = "post-card";
    article.innerHTML = `
      <div class="post-header">
        <img class="post-avatar" src="${escapeHtml(post.fotoPerfilUrl || "https://via.placeholder.com/52?text=%F0%9F%91%A4")}" alt="Foto de perfil de ${escapeHtml(post.nombre || "")}" />
        <div class="post-author">
          <h3>${escapeHtml(post.nombre)} ${escapeHtml(post.apellido)}</h3>
          <p class="post-meta">${escapeHtml(post.fechaCreacion || "")}</p>
        </div>
      </div>
      <p class="post-content">${escapeHtml(post.contenido)}</p>
      ${renderPostMedia(post.media || [])}
      ${accionesPost}
      <div class="post-actions${contieneVideo ? " video-post-actions" : ""}">
        <button class="like-btn${yaDioLike ? " active" : ""}" data-post-id="${post.id}">${contieneVideo ? (yaDioLike ? "Quitar me gusta" : "Me gusta") : (yaDioLike ? "Quitar like" : "Like")}</button>
        <span class="likes-count">${contieneVideo ? `${post.likesCount} me gusta en este video` : `Likes: ${post.likesCount}`}</span>
      </div>
      <div class="comment-box">
        <input type="text" id="commentInput-${post.id}" class="comment-input" placeholder="Escribe un comentario..." />
        <button class="comment-btn" data-post-id="${post.id}">Comentar</button>
      </div>
      <div class="comments-list" id="comments-${post.id}"><p class="no-comments">Cargando comentarios...</p></div>
    `;

    container.appendChild(article);
  });

  bindPostEvents();

  for (const post of posts) {
    await cargarComentarios(post.id);
  }

  bindCommentEvents();
}

function appendChatMessage(message) {
  const container = document.getElementById("chatMessages");
  if (!container) return;

  const item = document.createElement("div");
  item.className = "chat-message";
  item.innerHTML = `
    <strong>${escapeHtml(message.nombreCompleto || "")}</strong>
    <p class="post-content">${escapeHtml(message.contenido || "")}</p>
    <span>${escapeHtml(message.fechaCreacion || "")}</span>
  `;

  container.appendChild(item);
  container.scrollTop = container.scrollHeight;
}

async function cargarMensajesChat() {
  const response = await fetch(`${API_BASE}/Interactions/chat/messages`);
  const data = await parseResponse(response);
  const mensajes = response.ok && data.success ? data.data || [] : [];

  const container = document.getElementById("chatMessages");
  if (!container) return;

  container.innerHTML = "";
  mensajes.forEach(appendChatMessage);
}

async function iniciarChat() {
  await cargarMensajesChat();
  setChatStatus("Conectando al chat academico...");

  chatConnection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, { withCredentials: true })
    .withAutomaticReconnect()
    .build();

  chatConnection.on("ReceiveMessage", (message) => {
    appendChatMessage(message);
  });

  chatConnection.onreconnecting(() => {
    setChatStatus("Reconectando chat academico...", "error");
    updateChatSendState();
  });

  chatConnection.onreconnected(() => {
    setChatStatus("Chat academico conectado en tiempo real.", "connected");
    updateChatSendState();
  });

  chatConnection.onclose(() => {
    setChatStatus("No se pudo mantener la conexion del chat academico.", "error");
    updateChatSendState();
  });

  try {
    await chatConnection.start();
    setChatStatus("Chat academico conectado en tiempo real.", "connected");
  } catch (error) {
    console.error(error);
    setChatStatus("No se pudo conectar el chat academico en tiempo real.", "error");
  }

  updateChatSendState();
}

async function enviarMensajeChat() {
  const input = document.getElementById("chatInput");
  const contenido = input?.value.trim() || "";
  if (!contenido || !chatConnection) return;

  if (chatConnection.state !== signalR.HubConnectionState.Connected) {
    setChatStatus("El chat academico aun no esta conectado. Intenta de nuevo.", "error");
    updateChatSendState();
    return;
  }

  try {
    await chatConnection.invoke("SendMessage", { userId, contenido });
    input.value = "";
  } catch (error) {
    console.error(error);
    setChatStatus("No se pudo enviar el mensaje.", "error");
  }
}

document.addEventListener("DOMContentLoaded", async () => {
  bindMenuNavigation();
  document.getElementById("btnCrearPost")?.addEventListener("click", crearPost);
  document.getElementById("postMediaInput")?.addEventListener("change", (event) => {
    const input = event.target;
    const files = Array.from(input?.files || []);
    const info = document.getElementById("postMediaInfo");
    if (!info) return;

    if (!files.length) {
      info.textContent = "Puedes subir hasta 4 archivos.";
      return;
    }

    info.textContent = `${Math.min(files.length, 4)} archivo(s) seleccionado(s).`;
  });
  document.getElementById("btnSendChat")?.addEventListener("click", enviarMensajeChat);
  document.getElementById("chatInput")?.addEventListener("keydown", async (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      await enviarMensajeChat();
    }
  });

  await cargarPosts();
  await iniciarChat();
});
