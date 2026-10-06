// =========================
// Hämta element från HTML
// =========================

// Hämtar ett obligatoriskt element och ger ett tydligt fel
// om HTML och JavaScript inte matchar
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

const sendForm = getRequiredElement("send-form");
const messageInput = getRequiredElement("message");
const sendButton = getRequiredElement("send-button");
const characterCount = getRequiredElement("character-count");

if (!messagesWrapper) {
  throw new Error('Saknar HTML-element med class="messages-wrapper".');
}

// Rum-modal
const roomModal = getRequiredElement("room-modal");
const createRoomForm = getRequiredElement("create-room-form");

const newRoomNameInput = getRequiredElement("new-room-name");
const privateRoomCheckbox = getRequiredElement("private-room-checkbox");

const closeRoomModalButton = getRequiredElement("close-room-modal-button");

const cancelRoomButton = getRequiredElement("cancel-room-button");

// Notifieringar
const notificationContainer = getRequiredElement("notification-container");

// =========================
// Applikationens state
// =========================

// Den inloggade användarens namn
let username = "";

// JWT hålls endast i minnet och skrivs inte till localStorage
let accessToken = null;

// =========================
// SignalR
// =========================

// SignalR hämtar aktuell JWT när anslutningen autentiseras
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/chatHub", {
    accessTokenFactory: () => accessToken ?? "",
  })
  .withAutomaticReconnect()
  .build();

// =========================
// SignalR - meddelanden
// =========================

// Tar emot vanliga chatmeddelanden från servern
connection.on("ReceiveMessage", (sender, message) => {
  const ownMessage = sender === username;

  addMessage(sender, message, ownMessage);
});

// Tar emot systemmeddelanden från servern
connection.on("ReceiveSystemMessage", (message) => {
  addSystemMessage(message);
});

// =========================
// Authentication events
// =========================

// Login när formuläret skickas.
// preventDefault stoppar webbläsaren från att refresha sidan.
authForm.addEventListener("submit", async (event) => {
  event.preventDefault();

  await login();
});

// Registrera nytt konto
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

  // Grundläggande validering i klienten
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

    // Rensa endast lösenordet efter registrering
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
// Inloggning
// =========================

async function login() {
  const enteredUsername = usernameInput.value.trim();
  const password = passwordInput.value;

  clearAuthMessage();

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

    // Kontrollera att servern faktiskt returnerade en JWT
    if (!data.token) {
      showAuthMessage("Servern returnerade ingen token.", "error");

      return;
    }

    // JWT sparas endast i minnet
    accessToken = data.token;

    // Namnet används bara i UI.
    // Servern hämtar identiteten från den verifierade JWT-token.
    username = enteredUsername;

    // Starta den autentiserade SignalR-anslutningen
    await connectSignalR();

    // Uppdatera UI först när login och SignalR har lyckats
    showLoggedInUser();

    passwordInput.value = "";

    showNotification(`Välkommen ${username}!`, "success");
  } catch (error) {
    console.error("Login error:", error);

    // Rensa autentiseringsdata om login eller SignalR misslyckas
    accessToken = null;
    username = "";

    showAuthMessage("Inloggningen eller anslutningen misslyckades.", "error");
  } finally {
    setAuthLoading(false);
  }
}

// =========================
// Logga ut
// =========================

async function logout() {
  try {
    // Stoppa SignalR innan JWT tas bort
    if (connection.state !== signalR.HubConnectionState.Disconnected) {
      await connection.stop();
    }
  } catch (error) {
    console.error("Logout error:", error);
  }

  // Ta bort autentiseringsinformation
  accessToken = null;
  username = "";

  // Rensa chatten
  messages.replaceChildren();

  // Återställ UI
  showLoggedOutUser();

  showNotification("Du har loggats ut.", "success");
}

// =========================
// SignalR - anslutning
// =========================

async function connectSignalR() {
  if (!accessToken) {
    throw new Error("JWT saknas.");
  }

  // Undvik att starta en anslutning som redan är aktiv
  if (connection.state === signalR.HubConnectionState.Connected) {
    return;
  }

  setConnectionStatus("Ansluter...", "connecting");

  await connection.start();

  setConnectionStatus("Ansluten", "online");
}

// =========================
// SignalR - anslutningsstatus
// =========================

connection.onreconnecting(() => {
  setConnectionStatus("Återansluter...", "connecting");

  messageInput.disabled = true;
  sendButton.disabled = true;
});

connection.onreconnected(() => {
  setConnectionStatus("Ansluten", "online");

  messageInput.disabled = false;
  sendButton.disabled = false;

  showNotification("Anslutningen återställdes.", "success");
});

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
    /*
      Endast själva meddelandet skickas från klienten.
      Användarnamnet hämtas av servern från den
      verifierade JWT-token.
    */
    await connection.invoke("SendMessage", text);

    messageInput.value = "";

    updateCharacterCount();

    messageInput.focus();
  } catch (error) {
    console.error("SendMessage error:", error);

    showNotification("Meddelandet kunde inte skickas.", "error");
  }
});

// =========================
// Teckenräknare
// =========================

messageInput.addEventListener("input", () => {
  updateCharacterCount();
});

function updateCharacterCount() {
  characterCount.textContent = `${messageInput.value.length} / 500`;
}

// =========================
// Lägg till chatmeddelande
// =========================

function addMessage(sender, text, ownMessage) {
  const li = document.createElement("li");

  li.className = ownMessage ? "message message--own" : "message";

  const header = document.createElement("div");

  header.className = "message__header";

  const usernameElement = document.createElement("span");

  usernameElement.className = "message__username";

  /*
    textContent används istället för innerHTML.
    Det förhindrar att användarinput renderas
    som HTML eller JavaScript.
  */
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

  // Rendera meddelandet som ren text
  content.textContent = text;

  li.append(header, content);

  messages.appendChild(li);

  scrollToBottom();
}

// =========================
// Systemmeddelanden
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

  // Aktivera standardrummet
  const roomButtons = roomList.querySelectorAll(".room");

  roomButtons.forEach((button) => {
    button.disabled = false;
  });

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

  // Inaktivera rum
  const roomButtons = roomList.querySelectorAll(".room");

  roomButtons.forEach((button) => {
    button.disabled = true;
  });

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
// Chattrum
// =========================

// Öppna modal för nytt rum
createRoomButton.addEventListener("click", () => {
  roomModal.classList.remove("hidden");

  newRoomNameInput.focus();
});

// Stäng modal
closeRoomModalButton.addEventListener("click", closeRoomModal);

cancelRoomButton.addEventListener("click", closeRoomModal);

// Stäng modal genom att klicka på bakgrunden
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

createRoomForm.addEventListener("submit", (event) => {
  event.preventDefault();

  /*
      Backend för rum är inte implementerad ännu.
      Vi skapar därför inte ett falskt rum lokalt.
    */
  const newRoomName = newRoomNameInput.value.trim();

  if (!newRoomName) {
    return;
  }

  const roomType = privateRoomCheckbox.checked ? "privat" : "offentligt";

  const formattedRoomType =
    roomType.charAt(0).toUpperCase() + roomType.slice(1);

  showNotification(
    `${formattedRoomType} rum implementeras i nästa steg.`,
    "success",
  );

  closeRoomModal();
});

// =========================
// Notifieringar
// =========================

function showNotification(message, type = "success") {
  const notification = document.createElement("div");

  notification.className = `notification notification--${type}`;

  notification.textContent = message;

  notificationContainer.appendChild(notification);

  // Ta bort notifieringen automatiskt
  setTimeout(() => {
    notification.remove();
  }, 3500);
}

// =========================
// HTTP helpers
// =========================

// Läser både JSON och vanlig text från API-svar
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
// Startläge
// =========================

showLoggedOutUser();
