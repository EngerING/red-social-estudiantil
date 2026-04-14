const API_BASE = "http://localhost:5000/api";
const HUB_URL = "http://localhost:5000/hubs/chat";

const token = localStorage.getItem("token");
const userId = localStorage.getItem("userId");

let groups = [];
let selectedGroupId = null;
let groupChatConnection = null;

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

function bindMenuNavigation() {
  document.querySelectorAll("[data-link]").forEach((button) => {
    button.addEventListener("click", () => {
      const link = button.dataset.link;
      if (link) {
        window.location.href = link;
      }
    });
  });
}

async function parseResponse(response) {
  try {
    return await response.json();
  } catch {
    return { success: false, message: "Respuesta no valida del servidor" };
  }
}

function getSelectedGroup() {
  return groups.find((group) => group.id === selectedGroupId) || null;
}

function updateJoinedCounter() {
  const joined = groups.filter((group) => group.isMember).length;
  document.getElementById("joinedGroupsCount").textContent = String(joined);
}

function setGroupStatus(message, isError = false) {
  const node = document.getElementById("groupStatus");
  if (!node) return;

  node.textContent = message;
  node.style.color = isError ? "#a33030" : "#646b85";
}

function updateGroupChatAvailability() {
  const selectedGroup = getSelectedGroup();
  const button = document.getElementById("btnSendGroupMessage");
  if (!button) return;

  const canChat = !!selectedGroup && selectedGroup.isMember && groupChatConnection?.state === signalR.HubConnectionState.Connected;
  button.disabled = !canChat;
}

async function cargarGrupos() {
  const response = await fetch(`${API_BASE}/StudyGroups?userId=${encodeURIComponent(userId)}`);
  const data = await parseResponse(response);
  groups = response.ok && data.success ? data.data || [] : [];

  if (!selectedGroupId && groups.length) {
    selectedGroupId = groups[0].id;
  }

  if (selectedGroupId && !groups.some((group) => group.id === selectedGroupId)) {
    selectedGroupId = groups[0]?.id || null;
  }

  renderGroups();
  renderSelectedGroup();
  updateJoinedCounter();
}

function renderGroups() {
  const container = document.getElementById("studyGroupsContainer");
  if (!container) return;

  if (!groups.length) {
    container.innerHTML = `<article class="group-card"><h4>Sin grupos disponibles</h4><p>Vuelve a intentarlo en unos minutos.</p></article>`;
    return;
  }

  container.innerHTML = groups.map((group) => `
    <article class="group-card ${selectedGroupId === group.id ? "selected" : ""}">
      <div class="group-card-top">
        <span class="group-topic">${escapeHtml(group.tema)}</span>
        <span class="member-count">${group.miembrosCount} miembros</span>
      </div>
      <h4>${escapeHtml(group.nombre)}</h4>
      <p>${escapeHtml(group.descripcion)}</p>
      <div class="group-actions">
        <button class="select-btn" type="button" data-select-group="${group.id}">Ver grupo</button>
        ${group.isMember
          ? `<button class="leave-btn" type="button" data-leave-group="${group.id}">Salir</button>`
          : `<button class="join-btn" type="button" data-join-group="${group.id}">Unirme</button>`}
      </div>
    </article>
  `).join("");

  container.querySelectorAll("[data-select-group]").forEach((button) => {
    button.addEventListener("click", () => seleccionarGrupo(button.dataset.selectGroup));
  });

  container.querySelectorAll("[data-join-group]").forEach((button) => {
    button.addEventListener("click", () => unirseAGrupo(button.dataset.joinGroup));
  });

  container.querySelectorAll("[data-leave-group]").forEach((button) => {
    button.addEventListener("click", () => salirDeGrupo(button.dataset.leaveGroup));
  });
}

async function seleccionarGrupo(groupId) {
  selectedGroupId = groupId;
  renderGroups();
  renderSelectedGroup();
  await joinSelectedGroupRoom();
}

function renderSelectedGroup() {
  const selectedGroup = getSelectedGroup();
  document.getElementById("selectedGroupName").textContent = selectedGroup?.nombre || "Selecciona un grupo";
  document.getElementById("selectedGroupDescription").textContent = selectedGroup?.descripcion || "Elige una temática para ver su chat exclusivo.";
  document.getElementById("selectedGroupMeta").textContent = selectedGroup ? `${selectedGroup.miembrosCount} miembros` : "0 miembros";

  if (!selectedGroup) {
    setGroupStatus("Debes seleccionar un grupo para ver su actividad.");
  } else if (!selectedGroup.isMember) {
    setGroupStatus("Únete a este grupo para entrar a su chat exclusivo.");
  } else {
    setGroupStatus(`Estás dentro de ${selectedGroup.nombre}.`);
  }

  updateGroupChatAvailability();
  cargarMensajesGrupo();
}

async function unirseAGrupo(groupId) {
  const response = await fetch(`${API_BASE}/StudyGroups/${groupId}/join`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId })
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    setGroupStatus(data.message || "No se pudo unir al grupo.", true);
    return;
  }

  setGroupStatus(data.message || "Te uniste al grupo.");
  await cargarGrupos();
  selectedGroupId = groupId;
  renderSelectedGroup();
  await joinSelectedGroupRoom();
}

async function salirDeGrupo(groupId) {
  const response = await fetch(`${API_BASE}/StudyGroups/${groupId}/join?userId=${encodeURIComponent(userId)}`, {
    method: "DELETE"
  });

  const data = await parseResponse(response);
  if (!response.ok || !data.success) {
    setGroupStatus(data.message || "No se pudo salir del grupo.", true);
    return;
  }

  if (groupChatConnection?.state === signalR.HubConnectionState.Connected) {
    await groupChatConnection.invoke("LeaveStudyGroup", groupId);
  }

  setGroupStatus(data.message || "Saliste del grupo.");
  await cargarGrupos();
}

async function cargarMensajesGrupo() {
  const container = document.getElementById("groupMessages");
  const selectedGroup = getSelectedGroup();
  if (!container) return;

  if (!selectedGroup) {
    container.innerHTML = "";
    return;
  }

  const response = await fetch(`${API_BASE}/StudyGroups/${selectedGroup.id}/messages`);
  const data = await parseResponse(response);
  const messages = response.ok && data.success ? data.data || [] : [];

  if (!messages.length) {
    container.innerHTML = `<div class="group-message"><strong>Sin mensajes todavía</strong><p>Rompe el hielo en este grupo.</p></div>`;
    return;
  }

  container.innerHTML = "";
  messages.forEach(appendGroupMessage);
}

function appendGroupMessage(message) {
  if (!selectedGroupId || message.groupId !== selectedGroupId) {
    return;
  }

  const container = document.getElementById("groupMessages");
  if (!container) return;

  const item = document.createElement("div");
  item.className = "group-message";
  item.innerHTML = `
    <strong>${escapeHtml(message.nombreCompleto || "")}</strong>
    <p>${escapeHtml(message.contenido || "")}</p>
    <span>${escapeHtml(message.fechaCreacion || "")}</span>
  `;

  container.appendChild(item);
  container.scrollTop = container.scrollHeight;
}

async function iniciarChatDeGrupos() {
  groupChatConnection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, { withCredentials: true })
    .withAutomaticReconnect()
    .build();

  groupChatConnection.on("ReceiveStudyGroupMessage", (message) => {
    appendGroupMessage(message);
  });

  groupChatConnection.onreconnected(async () => {
    await joinSelectedGroupRoom();
    updateGroupChatAvailability();
  });

  groupChatConnection.onclose(() => {
    updateGroupChatAvailability();
  });

  try {
    await groupChatConnection.start();
    await joinSelectedGroupRoom();
  } catch (error) {
    console.error(error);
    setGroupStatus("No se pudo conectar el chat del grupo.", true);
  }

  updateGroupChatAvailability();
}

async function joinSelectedGroupRoom() {
  const selectedGroup = getSelectedGroup();
  if (!selectedGroup || !selectedGroup.isMember || !groupChatConnection || groupChatConnection.state !== signalR.HubConnectionState.Connected) {
    updateGroupChatAvailability();
    return;
  }

  try {
    await groupChatConnection.invoke("JoinStudyGroup", selectedGroup.id, userId);
  } catch (error) {
    console.error(error);
  }

  updateGroupChatAvailability();
}

async function enviarMensajeGrupo() {
  const selectedGroup = getSelectedGroup();
  const input = document.getElementById("groupChatInput");
  const contenido = input?.value.trim() || "";

  if (!selectedGroup || !selectedGroup.isMember || !contenido || !groupChatConnection) {
    return;
  }

  if (groupChatConnection.state !== signalR.HubConnectionState.Connected) {
    setGroupStatus("El chat del grupo no está conectado todavía.", true);
    updateGroupChatAvailability();
    return;
  }

  try {
    await groupChatConnection.invoke("SendStudyGroupMessage", {
      groupId: selectedGroup.id,
      userId,
      contenido
    });

    input.value = "";
  } catch (error) {
    console.error(error);
    setGroupStatus("No se pudo enviar el mensaje al grupo.", true);
  }
}

document.addEventListener("DOMContentLoaded", async () => {
  bindMenuNavigation();
  document.getElementById("btnSendGroupMessage")?.addEventListener("click", enviarMensajeGrupo);
  document.getElementById("groupChatInput")?.addEventListener("keydown", async (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      await enviarMensajeGrupo();
    }
  });

  await cargarGrupos();
  await iniciarChatDeGrupos();
});
