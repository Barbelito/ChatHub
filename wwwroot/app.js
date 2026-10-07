// =========================
// HTML-element
// =========================

// Hämtar ett obligatoriskt element från HTML
function getRequiredElement(id) {
  const element = document.getElementById(id);

  if (!element) {
    throw new Error(`Saknar HTML-element med id="${id}".`);
  }

  return element;
}

// Autentisering
const authPanel = getRequiredElement("auth-panel");
const authForm = getRequiredElement("auth-form");

const usernameInput = getRequiredElement("username");
const passwordInput = getRequiredElement("password");

const loginButton = getRequiredElement("login-button");
const registerButton = getRequiredElement("register-button");

const authMessage = getRequiredElement("auth-message");

// Inloggad användare
const userPanel = getRequiredElement("user-panel");
const currentUser = getRequiredElement("current-user");
const userAvatar = getRequiredElement("user-avatar");
const logoutButton = getRequiredElement("logout-button");

// Chattrum
const roomList = getRequiredElement("room-list");
const privateRoomList = getRequiredElement("private-room-list");

const createRoomButton = getRequiredElement("create-room-button");

const roomName = getRequiredElement("room-name");
const roomDescription = getRequiredElement("room-description");

// SignalR-status
const connectionStatus = getRequiredElement("connection-status");
const statusText = getRequiredElement("status-text");

// Meddelanden
const emptyState = getRequiredElement("empty-state");

const messages = getRequiredElement("messages");

const messagesWrapper = document.querySelector(".messages-wrapper");

if (!messagesWrapper) {
  throw new Error('Saknar HTML-element med class="messages-wrapper".');
}

const sendForm = getRequiredElement("send-form");

const messageInput = getRequiredElement("message");
const sendButton = getRequiredElement("send-button");

const characterCount = getRequiredElement("character-count");

// Rum-modal
const roomModal = getRequiredElement("room-modal");

const createRoomForm = getRequiredElement("create-room-form");

const newRoomNameInput = getRequiredElement("new-room-name");

const privateRoomCheckbox = getRequiredElement("private-room-checkbox");

const closeRoomModalButton = getRequiredElement("close-room-modal-button");

const cancelRoomButton = getRequiredElement("cancel-room-button");

// Inbjudningar
const inviteUserButton = getRequiredElement("invite-user-button");

const inviteModal = getRequiredElement("invite-modal");

const inviteForm = getRequiredElement("invite-form");

const inviteUsernameInput = getRequiredElement("invite-username");

const closeInviteModalButton = getRequiredElement("close-invite-modal-button");

const cancelInviteButton = getRequiredElement("cancel-invite-button");

const sendInviteButton = getRequiredElement("send-invite-button");

// Notifieringar
const notificationContainer = getRequiredElement("notification-container");

// General-rummet
const generalRoomButton = roomList.querySelector('[data-room="general"]');

// =========================
// State
// =========================

// Inloggad användare
let username = "";

// JWT sparas endast i minnet
let accessToken = null;

// null betyder General
let selectedRoomId = null;

// Rum som användaren får se
let availableRooms = [];

// =========================
// SignalR
// =========================

// Skapar SignalR-anslutningen med JWT
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/chatHub", {
    accessTokenFactory: () => accessToken ?? "",
  })
  .withAutomaticReconnect()
  .build();

// =========================
// Globala meddelanden
// =========================

// Tar emot meddelanden från General
connection.on("ReceiveMessage", (sender, message) => {
  // Visa bara General-meddelanden i General
  if (selectedRoomId !== null) {
    return;
  }

  const ownMessage = sender === username;

  addMessage(sender, message, ownMessage);
});

// Tar emot systemmeddelanden från General
connection.on("ReceiveSystemMessage", (message) => {
  if (selectedRoomId !== null) {
    return;
  }

  addSystemMessage(message);
});

// =========================
// Rumsmeddelanden
// =========================

// Tar emot meddelanden från ett chattrum
connection.on("ReceiveRoomMessage", (roomId, sender, message) => {
  // Visa bara meddelanden från aktivt rum
  if (roomId !== selectedRoomId) {
    return;
  }

  const ownMessage = sender === username;

  addMessage(sender, message, ownMessage);
});

// Tar emot systemmeddelanden från ett rum
connection.on("ReceiveRoomSystemMessage", (roomId, message) => {
  if (roomId !== selectedRoomId) {
    return;
  }

  addSystemMessage(message);
});

// =========================
// Rumsinbjudningar
// =========================

// Tar emot en inbjudan till ett privat rum
connection.on("ReceiveRoomInvitation", async (room) => {
  // Läs om rummen från servern
  await loadRooms();

  showNotification(`Du har blivit inbjuden till "${room.name}".`, "success");
});

// =========================
// Authentication events
// =========================

// Logga in när formuläret skickas
authForm.addEventListener("submit", async (event) => {
  // Stoppar sidan från att laddas om
  event.preventDefault();

  await login();
});

// Skapa konto
registerButton.addEventListener("click", async () => {
  await register();
});

// Logga ut
logoutButton.addEventListener("click", async () => {
  await logout();
});

// =========================
// Registrering
// =========================

async function register() {
  const enteredUsername = usernameInput.value.trim();

  const password = passwordInput.value;

  clearAuthMessage();

  // Enkel validering
  if (!enteredUsername || !password) {
    showAuthMessage("Användarnamn och lösenord krävs.", "error");

    return;
  }

  setAuthLoading(true, "register");

  try {
    const response = await fetch("/api/register", {
      method: "POST",

      headers: {
        "Content-Type": "application/json",
      },

      body: JSON.stringify({
        username: enteredUsername,
        password: password,
      }),
    });

    const responseMessage = await readResponseMessage(response);

    if (!response.ok) {
      showAuthMessage(
        responseMessage || "Registreringen misslyckades.",
        "error",
      );

      return;
    }

    showAuthMessage(
      responseMessage || "Kontot skapades. Du kan nu logga in.",
      "success",
    );

    passwordInput.value = "";
    passwordInput.focus();
  } catch (error) {
    console.error("Register error:", error);

    showAuthMessage("Kunde inte kontakta servern.", "error");
  } finally {
    setAuthLoading(false);
  }
}

// =========================
// Login
// =========================

async function login() {
  const enteredUsername = usernameInput.value.trim();

  const password = passwordInput.value;

  clearAuthMessage();

  // Enkel validering
  if (!enteredUsername || !password) {
    showAuthMessage("Användarnamn och lösenord krävs.", "error");

    return;
  }

  setAuthLoading(true, "login");

  try {
    const response = await fetch("/api/login", {
      method: "POST",

      headers: {
        "Content-Type": "application/json",
      },

      body: JSON.stringify({
        username: enteredUsername,
        password: password,
      }),
    });

    if (!response.ok) {
      const responseMessage = await readResponseMessage(response);

      showAuthMessage(
        responseMessage || "Fel användarnamn eller lösenord.",
        "error",
      );

      return;
    }

    const data = await response.json();

    // Kontrollera att JWT finns
    if (!data.token) {
      showAuthMessage("Servern returnerade ingen token.", "error");

      return;
    }

    // JWT sparas endast i minnet
    accessToken = data.token;

    // Namnet används endast för UI
    username = enteredUsername;

    // Starta SignalR med JWT
    await connectSignalR();

    // Visa inloggat UI
    showLoggedInUser();

    // Hämta användarens rum
    await loadRooms();

    // Börja alltid i General
    await selectGeneralRoom();

    passwordInput.value = "";

    showNotification(`Välkommen ${username}!`, "success");
  } catch (error) {
    console.error("Login error:", error);

    accessToken = null;
    username = "";

    showAuthMessage("Inloggningen eller anslutningen misslyckades.", "error");
  } finally {
    setAuthLoading(false);
  }
}

// =========================
// Logout
// =========================

async function logout() {
  try {
    // Stoppa SignalR
    if (connection.state !== signalR.HubConnectionState.Disconnected) {
      await connection.stop();
    }
  } catch (error) {
    console.error("Logout error:", error);
  }

  // Rensa autentisering
  accessToken = null;
  username = "";

  // Rensa rum
  selectedRoomId = null;
  availableRooms = [];

  // Rensa meddelanden
  messages.replaceChildren();

  // Rensa dynamiska rum
  clearDynamicRooms();

  // Dölj inbjudningsknappen
  inviteUserButton.classList.add("hidden");

  // Stäng modaler
  closeRoomModal();
  closeInviteModal();

  // Återställ UI
  showLoggedOutUser();

  showNotification("Du har loggats ut.", "success");
}

// =========================
// SignalR-anslutning
// =========================

async function connectSignalR() {
  if (!accessToken) {
    throw new Error("JWT saknas.");
  }

  // Starta inte en redan aktiv anslutning
  if (connection.state === signalR.HubConnectionState.Connected) {
    return;
  }

  setConnectionStatus("Ansluter...", "connecting");

  await connection.start();

  setConnectionStatus("Ansluten", "online");
}

// =========================
// SignalR-status
// =========================

// Körs när SignalR försöker återansluta
connection.onreconnecting(() => {
  setConnectionStatus("Återansluter...", "connecting");

  messageInput.disabled = true;
  sendButton.disabled = true;
});

// Körs när SignalR har återanslutit
connection.onreconnected(async () => {
  setConnectionStatus("Ansluten", "online");

  messageInput.disabled = false;
  sendButton.disabled = false;

  /*
    SignalR-grupper är kopplade till anslutningen.
    Därför går vi med i rummet igen efter reconnect.
  */
  if (selectedRoomId !== null) {
    try {
      await connection.invoke("JoinRoom", selectedRoomId);
    } catch (error) {
      console.error("Rejoin room error:", error);

      await selectGeneralRoom();
    }
  }

  await loadRooms();

  showNotification("Anslutningen återställdes.", "success");
});

// Körs när SignalR kopplas bort
connection.onclose(() => {
  setConnectionStatus("Ej ansluten", "offline");

  messageInput.disabled = true;
  sendButton.disabled = true;
});

// =========================
// Skicka meddelande
// =========================

sendForm.addEventListener("submit", async (event) => {
  event.preventDefault();

  const text = messageInput.value.trim();

  if (!text) {
    return;
  }

  if (connection.state !== signalR.HubConnectionState.Connected) {
    showNotification("Du är inte ansluten till chatten.", "error");

    return;
  }

  try {
    // General använder den globala chatten
    if (selectedRoomId === null) {
      await connection.invoke("SendMessage", text);
    }

    // Rum använder SignalR-grupper
    else {
      await connection.invoke("SendRoomMessage", selectedRoomId, text);
    }

    messageInput.value = "";

    updateCharacterCount();

    messageInput.focus();
  } catch (error) {
    console.error("Send message error:", error);

    showNotification(
      getSignalRErrorMessage(error, "Meddelandet kunde inte skickas."),
      "error",
    );
  }
});

// =========================
// Teckenräknare
// =========================

messageInput.addEventListener("input", updateCharacterCount);

function updateCharacterCount() {
  characterCount.textContent = `${messageInput.value.length} / 500`;
}

// =========================
// Visa meddelande
// =========================

function addMessage(sender, text, ownMessage) {
  const li = document.createElement("li");

  li.className = ownMessage ? "message message--own" : "message";

  const header = document.createElement("div");

  header.className = "message__header";

  const usernameElement = document.createElement("span");

  usernameElement.className = "message__username";

  // textContent skyddar mot HTML i användarinput
  usernameElement.textContent = sender;

  const timeElement = document.createElement("span");

  timeElement.className = "message__time";

  timeElement.textContent = new Date().toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });

  header.append(usernameElement, timeElement);

  const content = document.createElement("div");

  content.className = "message__content";

  // Meddelandet visas som ren text
  content.textContent = text;

  li.append(header, content);

  messages.appendChild(li);

  scrollToBottom();
}

// =========================
// Systemmeddelande
// =========================

function addSystemMessage(text) {
  const li = document.createElement("li");

  li.className = "message message--system";

  li.textContent = text;

  messages.appendChild(li);

  scrollToBottom();
}

// =========================
// Scroll
// =========================

function scrollToBottom() {
  requestAnimationFrame(() => {
    messagesWrapper.scrollTop = messagesWrapper.scrollHeight;
  });
}

// =========================
// Inloggat UI
// =========================

function showLoggedInUser() {
  authPanel.classList.add("hidden");

  userPanel.classList.remove("hidden");

  currentUser.textContent = username;

  userAvatar.textContent = username.charAt(0).toUpperCase();

  emptyState.classList.add("hidden");

  messageInput.disabled = false;

  sendButton.disabled = false;

  messageInput.placeholder = "Skriv ett meddelande...";

  createRoomButton.disabled = false;

  // Inbjudningsknappen visas bara i privata rum som användaren äger
  inviteUserButton.classList.add("hidden");

  // Aktivera General
  if (generalRoomButton) {
    generalRoomButton.disabled = false;
  }

  messageInput.focus();
}

// =========================
// Utloggat UI
// =========================

function showLoggedOutUser() {
  authPanel.classList.remove("hidden");

  userPanel.classList.add("hidden");

  currentUser.textContent = "Okänd";

  userAvatar.textContent = "?";

  usernameInput.value = "";
  passwordInput.value = "";

  messageInput.value = "";

  messageInput.disabled = true;

  sendButton.disabled = true;

  messageInput.placeholder = "Logga in för att börja chatta...";

  createRoomButton.disabled = true;

  // Dölj inbjudningsknappen när användaren är utloggad
  inviteUserButton.classList.add("hidden");

  if (generalRoomButton) {
    generalRoomButton.disabled = true;

    generalRoomButton.classList.add("room--active");
  }

  roomName.textContent = "General";

  roomDescription.textContent = "Global chatt för alla användare.";

  emptyState.classList.remove("hidden");

  setConnectionStatus("Ej ansluten", "offline");

  updateCharacterCount();

  usernameInput.focus();
}

// =========================
// Authentication UI
// =========================

function setAuthLoading(loading, action = "") {
  loginButton.disabled = loading;

  registerButton.disabled = loading;

  usernameInput.disabled = loading;

  passwordInput.disabled = loading;

  if (!loading) {
    loginButton.textContent = "Logga in";

    registerButton.textContent = "Skapa konto";

    return;
  }

  if (action === "register") {
    registerButton.textContent = "Skapar konto...";
  } else {
    loginButton.textContent = "Loggar in...";
  }
}

function showAuthMessage(message, type) {
  authMessage.textContent = message;

  authMessage.classList.remove(
    "hidden",
    "auth-message--error",
    "auth-message--success",
  );

  if (type === "error") {
    authMessage.classList.add("auth-message--error");
  }

  if (type === "success") {
    authMessage.classList.add("auth-message--success");
  }
}

function clearAuthMessage() {
  authMessage.textContent = "";

  authMessage.classList.add("hidden");

  authMessage.classList.remove("auth-message--error", "auth-message--success");
}

// =========================
// Connection UI
// =========================

function setConnectionStatus(text, state) {
  statusText.textContent = text;

  connectionStatus.classList.remove(
    "status--online",
    "status--offline",
    "status--connecting",
  );

  if (state === "online") {
    connectionStatus.classList.add("status--online");
  } else if (state === "connecting") {
    connectionStatus.classList.add("status--connecting");
  } else {
    connectionStatus.classList.add("status--offline");
  }
}

// =========================
// General
// =========================

// Öppna General
if (generalRoomButton) {
  generalRoomButton.addEventListener("click", async () => {
    await selectGeneralRoom();
  });
}

async function selectGeneralRoom() {
  try {
    // Lämna tidigare rum
    if (selectedRoomId !== null) {
      await connection.invoke("LeaveRoom", selectedRoomId);
    }

    selectedRoomId = null;

    // Rensa gamla meddelanden
    messages.replaceChildren();

    roomName.textContent = "General";

    roomDescription.textContent = "Global chatt för alla användare.";

    // General har ingen inbjudningsknapp
    inviteUserButton.classList.add("hidden");

    closeInviteModal();

    setActiveRoomButton(null);

    messageInput.focus();
  } catch (error) {
    console.error("Select General error:", error);
  }
}

// =========================
// Hämta rum
// =========================

async function loadRooms() {
  try {
    // Servern bestämmer vilka rum användaren får se
    availableRooms = await connection.invoke("GetAvailableRooms");

    renderRooms();
  } catch (error) {
    console.error("GetAvailableRooms error:", error);

    showNotification("Kunde inte hämta chattrummen.", "error");
  }
}

// =========================
// Visa rum
// =========================

function renderRooms() {
  // Ta bort tidigare dynamiska rum
  clearDynamicRooms();

  const privateRooms = availableRooms.filter((room) => room.isPrivate);

  for (const room of availableRooms) {
    const button = createRoomButtonElement(room);

    if (room.isPrivate) {
      privateRoomList.appendChild(button);
    } else {
      roomList.appendChild(button);
    }
  }

  // Visa information om inga privata rum finns
  if (privateRooms.length === 0) {
    const emptyPrivateRooms = document.createElement("p");

    emptyPrivateRooms.className = "rooms__empty";

    emptyPrivateRooms.textContent = "Inga privata rum ännu.";

    privateRoomList.appendChild(emptyPrivateRooms);
  }

  // Behåll markeringen på aktivt rum efter omladdning
  setActiveRoomButton(selectedRoomId);
}

// Skapar en knapp för ett rum
function createRoomButtonElement(room) {
  const button = document.createElement("button");

  button.type = "button";

  button.className = "room room--dynamic";

  button.dataset.roomId = room.id.toString();

  const name = document.createElement("span");

  // textContent skyddar mot HTML i rumsnamnet
  name.textContent = room.name;

  button.appendChild(name);

  // Visa markering för privata rum
  if (room.isPrivate) {
    const badge = document.createElement("span");

    badge.textContent = "Privat";

    badge.className = "room__badge";

    button.appendChild(badge);
  }

  button.addEventListener("click", async () => {
    await selectRoom(room);
  });

  return button;
}

// Tar bort dynamiska rum från UI
function clearDynamicRooms() {
  const publicRooms = roomList.querySelectorAll(".room--dynamic");

  publicRooms.forEach((button) => button.remove());

  privateRoomList.replaceChildren();
}

// =========================
// Byta rum
// =========================

async function selectRoom(room) {
  try {
    // Gör inget om rummet redan är aktivt
    if (selectedRoomId === room.id) {
      return;
    }

    const previousRoomId = selectedRoomId;

    /*
      Servern kontrollerar om användaren
      har behörighet till rummet.
    */
    await connection.invoke("JoinRoom", room.id);

    // Lämna tidigare rum efter att nya rummet godkänts
    if (previousRoomId !== null) {
      await connection.invoke("LeaveRoom", previousRoomId);
    }

    selectedRoomId = room.id;

    // Rensa meddelanden från tidigare rum
    messages.replaceChildren();

    roomName.textContent = room.name;

    roomDescription.textContent = room.isPrivate
      ? "Privat chattrum"
      : "Offentligt chattrum";

    // Endast ägaren till privata rum ser inbjudningsknappen
    if (room.isPrivate && room.isOwner) {
      inviteUserButton.classList.remove("hidden");
    } else {
      inviteUserButton.classList.add("hidden");
    }

    closeInviteModal();

    setActiveRoomButton(room.id);

    messageInput.focus();
  } catch (error) {
    console.error("JoinRoom error:", error);

    showNotification(
      getSignalRErrorMessage(error, "Du kunde inte gå med i rummet."),
      "error",
    );
  }
}

// =========================
// Aktivt rum
// =========================

function setActiveRoomButton(roomId) {
  const buttons = document.querySelectorAll(".room");

  buttons.forEach((button) => {
    button.classList.remove("room--active");
  });

  // null betyder General
  if (roomId === null) {
    if (generalRoomButton) {
      generalRoomButton.classList.add("room--active");
    }

    return;
  }

  const activeButton = document.querySelector(`[data-room-id="${roomId}"]`);

  if (activeButton) {
    activeButton.classList.add("room--active");
  }
}

// =========================
// Öppna rum-modal
// =========================

createRoomButton.addEventListener("click", () => {
  roomModal.classList.remove("hidden");

  newRoomNameInput.focus();
});

// =========================
// Stäng rum-modal
// =========================

closeRoomModalButton.addEventListener("click", closeRoomModal);

cancelRoomButton.addEventListener("click", closeRoomModal);

const modalBackdrop = roomModal.querySelector(".modal__backdrop");

if (modalBackdrop) {
  modalBackdrop.addEventListener("click", closeRoomModal);
}

function closeRoomModal() {
  roomModal.classList.add("hidden");

  createRoomForm.reset();
}

// =========================
// Skapa rum
// =========================

createRoomForm.addEventListener("submit", async (event) => {
  event.preventDefault();

  const newRoomName = newRoomNameInput.value.trim();

  if (!newRoomName) {
    showNotification("Du måste ange ett rumsnamn.", "error");

    return;
  }

  const isPrivate = privateRoomCheckbox.checked;

  try {
    /*
        UserId skickas inte från klienten.
        Servern hämtar användaren från JWT.
      */
    const room = await connection.invoke("CreateRoom", newRoomName, isPrivate);

    closeRoomModal();

    // Hämta rummen igen från databasen
    await loadRooms();

    // Öppna det nya rummet
    await selectRoom(room);

    showNotification(`Rummet "${room.name}" skapades.`, "success");
  } catch (error) {
    console.error("CreateRoom error:", error);

    showNotification(
      getSignalRErrorMessage(error, "Rummet kunde inte skapas."),
      "error",
    );
  }
});

// =========================
// Öppna inbjudnings-modal
// =========================

inviteUserButton.addEventListener("click", () => {
  // Inbjudningar gäller endast ett valt privat rum
  if (selectedRoomId === null) {
    return;
  }

  inviteModal.classList.remove("hidden");

  inviteUsernameInput.focus();
});

// =========================
// Stäng inbjudnings-modal
// =========================

closeInviteModalButton.addEventListener("click", closeInviteModal);

cancelInviteButton.addEventListener("click", closeInviteModal);

const inviteModalBackdrop = inviteModal.querySelector(".modal__backdrop");

if (inviteModalBackdrop) {
  inviteModalBackdrop.addEventListener("click", closeInviteModal);
}

function closeInviteModal() {
  inviteModal.classList.add("hidden");

  inviteForm.reset();
}

// =========================
// Bjud in användare
// =========================

inviteForm.addEventListener("submit", async (event) => {
  event.preventDefault();

  const invitedUsername = inviteUsernameInput.value.trim();

  if (!invitedUsername) {
    showNotification("Du måste ange ett användarnamn.", "error");

    return;
  }

  if (selectedRoomId === null) {
    showNotification("Du måste vara i ett privat rum.", "error");

    return;
  }

  sendInviteButton.disabled = true;

  sendInviteButton.textContent = "Bjuder in...";

  try {
    /*
        Servern kontrollerar att:
        - rummet är privat
        - användaren finns
        - den som bjuder in är ägare
        - användaren inte redan är medlem
      */
    await connection.invoke(
      "InviteUserToRoom",
      selectedRoomId,
      invitedUsername,
    );

    closeInviteModal();

    showNotification(`${invitedUsername} har lagts till i rummet.`, "success");
  } catch (error) {
    console.error("InviteUserToRoom error:", error);

    showNotification(
      getSignalRErrorMessage(error, "Användaren kunde inte bjudas in."),
      "error",
    );
  } finally {
    sendInviteButton.disabled = false;

    sendInviteButton.textContent = "Bjud in";
  }
});

// =========================
// Notifieringar
// =========================

function showNotification(message, type = "success") {
  const notification = document.createElement("div");

  notification.className = `notification notification--${type}`;

  notification.textContent = message;

  notificationContainer.appendChild(notification);

  // Ta bort notifieringen efter en stund
  setTimeout(() => {
    notification.remove();
  }, 3500);
}

// =========================
// HTTP helper
// =========================

// Läser text eller JSON från API-svar
async function readResponseMessage(response) {
  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    const data = await response.json();

    if (typeof data === "string") {
      return data;
    }

    return data.message || data.title || "";
  }

  return await response.text();
}

// =========================
// SignalR error helper
// =========================

// Hämtar ett felmeddelande från SignalR
function getSignalRErrorMessage(error, fallback) {
  if (error && typeof error.message === "string") {
    return error.message;
  }

  return fallback;
}

// =========================
// Startläge
// =========================

showLoggedOutUser();
