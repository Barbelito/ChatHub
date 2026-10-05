// =========================
// Hämta element från HTML
// =========================

const loginPanel = document.getElementById("login-panel");
const userPanel = document.getElementById("user-panel");

const usernameInput = document.getElementById("username");
const connectButton = document.getElementById("connect-button");

const currentUser = document.getElementById("current-user");
const userAvatar = document.getElementById("user-avatar");

const connectionStatus = document.getElementById("connection-status");
const statusText = document.getElementById("status-text");

const emptyState = document.getElementById("empty-state");

const sendForm = document.getElementById("send-form");
const messageInput = document.getElementById("message");
const sendButton = document.getElementById("send-button");

const messages = document.getElementById("messages");

// Skapar SignalR anslutningen
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/chatHub")
  .withAutomaticReconnect()
  .build();

// Sparar namnet på den aktuella användaren
let username = "";

// =========================
// Händelser
// =========================

// Anslut när användaren klickar på knappen
connectButton.addEventListener("click", connect);

// Tillåt Enter för att ansluta
usernameInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    connect();
  }
});

// =========================
// Anslut användaren
// =========================

async function connect() {
  // Hämtar användarnamnet och tar bort eventuella mellanslag
  const name = usernameInput.value.trim();

  // Kontrollera att användarnamn är ifyllt
  if (name === "") {
    alert("Ange ett användarnamn.");

    usernameInput.focus();

    return;
  }

  // Sparar användarnamnet
  username = name;

  // Uppdaterar användarpanelen
  currentUser.textContent = username;
  userAvatar.textContent = username.charAt(0).toUpperCase();

  // Dölj login och visa användarinformation
  loginPanel.classList.add("hidden");
  userPanel.classList.remove("hidden");

  // Uppdatera anslutningsstatus
  try {
    await connection.start();

    connectionStatus.classList.remove("status--offline");
    connectionStatus.classList.add("status--online");

    statusText.textContent = "Ansluten";

    messageInput.disabled = false;
    sendButton.disabled = false;

    emptyState.classList.add("hidden");

    addSystemMessage(`${username} anslöt till chatten.`);
  } catch (error) {
    console.error(error);

    connectionStatus.classList.remove("status--online");
    connectionStatus.classList.add("status--offline");

    statusText.textContent = "Anslutning misslyckades";
  }
}

// =========================
// Skicka meddelande
// =========================

sendForm.addEventListener("submit", (event) => {
  event.preventDefault();

  const text = messageInput.value.trim();

  // Skicka inte tomma meddelanden
  if (text === "") return;

  addOwnMessage(text);

  // Rensa textrutan
  messageInput.value = "";

  messageInput.focus();
});

// =========================
// Lägg till ett eget meddelande
// =========================

function addOwnMessage(text) {
  const li = document.createElement("li");

  li.className = "message message--own";

  // Hämtar aktuell tid
  const time = new Date().toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });

  // Skapar meddelandets HTML
  li.innerHTML = `
        <div class="message__header">
            <span class="message__username">${username}</span>
            <span class="message__time">${time}</span>
        </div>

        <div class="message__content">
            ${text}
        </div>
    `;

  // Lägger till meddelandet i chatten
  messages.appendChild(li);

  // Scrollar automatiskt längst ner
  messages.scrollTop = messages.scrollHeight;
}

// =========================
// Lägg till ett systemmeddelande
// =========================

function addSystemMessage(text) {
  const li = document.createElement("li");

  li.className = "message message--system";

  li.textContent = text;

  messages.appendChild(li);
}

connection.onclose(() => {
  connectionStatus.classList.remove("status--online");
  connectionStatus.classList.add("status--offline");

  statusText.textContent = "Ej ansluten";

  messageInput.disabled = true;
  sendButton.disabled = true;
});

connection.onreconnecting(() => {
  connectionStatus.classList.remove("status--online");
  connectionStatus.classList.add("status--offline");

  statusText.textContent = "Återansluter...";
});

connection.onreconnected(() => {
  connectionStatus.classList.remove("status--offline");
  connectionStatus.classList.add("status--online");

  statusText.textContent = "Ansluten";
});
