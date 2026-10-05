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

// Messages
const messages = document.getElementById("messages");
const messagesWrapper = document.querySelector(".messages-wrapper");

// =========================
// Skapar SignalR-anslutningen
// =========================

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/chatHub")
  .withAutomaticReconnect()
  .build();

// =========================
// Tar emot meddelanden från servern
// =========================

connection.on("ReceiveMessage", (username, message) => {
  addMessage(username, message);
});

let username = "";

// =========================
// Händelser
// =========================

connectButton.addEventListener("click", connect);

usernameInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    connect();
  }
});

// =========================
// Anslut användaren
// =========================

async function connect() {
  const name = usernameInput.value.trim();

  if (name === "") {
    alert("Ange ett användarnamn.");
    usernameInput.focus();
    return;
  }

  username = name;

  currentUser.textContent = username;
  userAvatar.textContent = username.charAt(0).toUpperCase();

  loginPanel.classList.add("hidden");
  userPanel.classList.remove("hidden");

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

sendForm.addEventListener("submit", async (event) => {
  event.preventDefault();

  const text = messageInput.value.trim();

  if (text === "") return;

  await connection.invoke("SendMessage", username, text);

  messageInput.value = "";
  messageInput.focus();
});

// =========================
// Lägg till eget meddelande
// =========================

function addOwnMessage(text) {
  const li = document.createElement("li");
  li.className = "message message--own";

  const time = new Date().toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });

  li.innerHTML = `
        <div class="message__header">
            <span class="message__username">${username}</span>
            <span class="message__time">${time}</span>
        </div>

        <div class="message__content">
            ${text}
        </div>
    `;

  messages.appendChild(li);

  requestAnimationFrame(() => {
    messagesWrapper.scrollTop = messagesWrapper.scrollHeight;
  });
}

// =========================
// Lägg till meddelande
// =========================

function addMessage(username, text) {
  const li = document.createElement("li");
  li.className = "message";

  const time = new Date().toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });

  li.innerHTML = `
      <div class="message__header">
          <span class="message__username">${username}</span>
          <span class="message__time">${time}</span>
      </div>

      <div class="message__content">
          ${text}
      </div>
  `;

  messages.appendChild(li);

  requestAnimationFrame(() => {
    messagesWrapper.scrollTop = messagesWrapper.scrollHeight;
  });
}

// =========================
// Systemmeddelanden
// =========================

function addSystemMessage(text) {
  const li = document.createElement("li");
  li.className = "message message--system";
  li.textContent = text;

  messages.appendChild(li);

  requestAnimationFrame(() => {
    messagesWrapper.scrollTop = messagesWrapper.scrollHeight;
  });
}

// =========================
// Hantera anslutningsstatus
// =========================

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
