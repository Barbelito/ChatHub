# ChatHub

ChatHub (ChatBook i UI) är en realtidschatt byggd med ASP.NET Core och SignalR.

Projektet är utvecklat med fokus på nätverkskommunikation och säkerhet. Användare kan skapa konto, logga in, chatta i realtid, skapa rum och använda privata rum med behörighetskontroll.

Meddelanden krypteras i klienten innan de skickas genom SignalR.

---

## Funktioner

- Registrering och inloggning
- Lösenord lagras hashade
- JWT-baserad autentisering
- SignalR för realtidskommunikation
- Global chatt
- Offentliga chattrum
- Privata chattrum
- Inbjudningar till privata rum
- Behörighetskontroll på servern
- Krypterade chattmeddelanden
- ECDH för nyckelutbyte
- HKDF för generering av sessionsnycklar
- AES-256-GCM för kryptering
- Rate limiting
- SQLite-databas

---

# Säkerhet

Säkerhet är en central del av projektet.

## Lösenord

Lösenord sparas aldrig i klartext.

ASP.NET Core `PasswordHasher<User>` används för att hasha lösenord innan användaren sparas i databasen.

Vid inloggning jämförs det angivna lösenordet med den sparade hashen.

---

## JWT-autentisering

Efter en lyckad inloggning skapas en JWT-token.

Token innehåller bland annat:

- användarens ID
- användarnamn
- issuer
- audience
- giltighetstid

JWT-token signeras med en hemlig nyckel.

Den hemliga nyckeln sparas inte direkt i projektets `appsettings.json`. Vid lokal utveckling används istället .NET User Secrets.

SignalR-hubben använder:

```csharp
[Authorize]
```

Det innebär att endast autentiserade användare kan ansluta till chatten.

---

## SignalR och behörighet

SignalR används för kommunikationen mellan klient och server.

Privata rum skyddas inte enbart genom SignalR Groups.

Servern kontrollerar användarens ID från den verifierade JWT-token och jämför det med informationen i databasen.

Servern kontrollerar bland annat behörighet när en användare:

- ansluter till ett privat rum
- skickar meddelanden till ett rum
- hämtar rummets krypteringsnyckel
- bjuder in andra användare

Det innebär att en användare inte får tillgång till ett privat rum genom att endast manipulera JavaScript-koden i webbläsaren.

---

# Kryptering

ChatBook använder kryptering på applikationsnivå.

Teknikerna som används är:

- ECDH P-256
- HKDF-SHA256
- AES-256-GCM

## ECDH

När en SignalR-anslutning skapas genererar klienten ett ECDH-nyckelpar.

Klientens privata nyckel stannar i webbläsaren.

Klienten skickar sin publika nyckel till servern och servern skapar sitt eget ECDH-nyckelpar.

Klienten och servern kan därefter skapa samma gemensamma hemlighet utan att skicka den över nätverket.

---

## HKDF

Den gemensamma hemligheten från ECDH används tillsammans med HKDF-SHA256.

HKDF skapar en 256-bitars sessionsnyckel som används mellan den aktuella klienten och servern.

Varje SignalR-anslutning får sin egen sessionsnyckel.

---

## AES-256-GCM

Varje chattkanal har en AES-256-nyckel.

Det gäller både:

- General
- offentliga rum
- privata rum

När klienten behöver en kanalnyckel skickar servern den krypterad med klientens sessionsnyckel.

Klienten dekrypterar kanalnyckeln och sparar den tillfälligt i webbläsarens minne.

När användaren skickar ett meddelande krypteras meddelandet i webbläsaren med AES-256-GCM.

SignalR skickar därför ett krypterat meddelande istället för klartext.

Exempel på data som skickas:

```json
{
  "iv": "24Ep+3G6CR3KqGrH",
  "ciphertext": "feqjQGm3UefPmslM4gTXnT52hcQa..."
}
```

Servern behöver inte dekryptera själva chattmeddelandet innan det vidarebefordras.

AES-GCM ger både kryptering och integritetskontroll. Ett manipulerat krypterat meddelande kan därför inte dekrypteras korrekt.

---

# Rate limiting

Projektet använder rate limiting för att minska risken för spam och automatiserade attacker.

Rate limiting används på flera nivåer.

## Login och registrering

Login och registrering är begränsade per IP-adress.

```text
Max 5 försök per minut
```

Det minskar möjligheten att snabbt göra många automatiserade inloggningsförsök.

## SignalR

Antalet nya SignalR-anslutningar begränsas.

```text
Max 30 anslutningsförsök per minut
```

## Chattmeddelanden

Varje autentiserad användare har en separat begränsning för chattmeddelanden.

```text
Max 10 meddelanden på 10 sekunder
```

Begränsningen baseras på användarens ID från JWT-token.

---

# Databas

Projektet använder:

```text
SQLite
```

Entity Framework Core används för kommunikation med databasen.

Databasen innehåller bland annat:

- Users
- ChatRooms
- ChatRoomMembers

Databasen används för att kontrollera medlemskap och behörighet till privata rum.

---

# Tekniker

Projektet använder:

- C#
- .NET 8
- ASP.NET Core
- SignalR
- Entity Framework Core
- SQLite
- JWT Bearer Authentication
- PasswordHasher
- Web Crypto API
- ECDH P-256
- HKDF-SHA256
- AES-256-GCM
- Rate Limiting
- HTML
- CSS
- JavaScript

---

# Installation

## 1. Förutsättningar

För att köra projektet behöver du:

- .NET 8 SDK
- Git
- Entity Framework Core CLI

Kontrollera .NET:

```bash
dotnet --version
```

Om `dotnet-ef` inte redan finns installerat:

```bash
dotnet tool install --global dotnet-ef
```

Kontrollera installationen:

```bash
dotnet ef --version
```

---

## 2. Klona projektet

```bash
git clone https://github.com/Barbelito/ChatHub.git
```

Gå sedan in i projektet:

```bash
cd ChatHub
```

---

## 3. Återställ NuGet-paket

Kör:

```bash
dotnet restore
```

---

# JWT-konfiguration

Projektet behöver en hemlig JWT-nyckel för att kunna skapa och verifiera tokens.

Nyckeln ska inte läggas direkt i Git eller sparas i repositoryt.

Projektet använder .NET User Secrets.

## 4. Initiera User Secrets

Kör från mappen där `ChatStart.csproj` ligger:

```bash
dotnet user-secrets init
```

Lägg sedan till JWT-konfigurationen:

```bash
dotnet user-secrets set "Jwt:Key" "KJH87sd98ASDkjh98ASDkjh98ASDkjh98ASDkjh98ASDkjh98ASDkjh98ASDkjh"
dotnet user-secrets set "Jwt:Issuer" "ChatBook"
dotnet user-secrets set "Jwt:Audience" "ChatBook"
```

Det motsvarar följande konfiguration:

```json
{
  "Jwt:Key": "KJH87sd98ASDkjh98ASDkjh98ASDkjh98ASDkjh98ASDkjh98ASDkjh98ASDkjh",
  "Jwt:Issuer": "ChatBook",
  "Jwt:Audience": "ChatBook"
}
```

Värdena kan kontrolleras med:

```bash
dotnet user-secrets list
```

JWT-nyckeln ovan är endast ett exempel för lokal användning. En riktig produktionsmiljö ska använda en egen stark och hemlig nyckel.

---

# Skapa databasen

## 5. Kör migrations

Kör följande från mappen där `ChatStart.csproj` ligger:

```bash
dotnet ef database update
```

Det skapar SQLite-databasen och applicerar projektets migrations.

Efter detta ska bland annat tabellerna för användare och chattrum finnas.

---

# Starta projektet

## 6. Kör applikationen

Kör:

```bash
dotnet run
```

Terminalen visar vilken HTTPS-adress applikationen använder.

Exempel:

```text
https://localhost:7000
```

Öppna adressen i webbläsaren.

Eftersom projektet använder HTTPS kan webbläsaren första gången fråga om det lokala utvecklingscertifikatet.

Om HTTPS-certifikatet behöver installeras eller godkännas kan följande köras:

```bash
dotnet dev-certs https --trust
```

Starta sedan projektet igen:

```bash
dotnet run
```

---

# Testa applikationen

När applikationen körs:

1. Skapa ett nytt konto.
2. Logga in.
3. Anslut till General-chatten.
4. Öppna en annan webbläsare eller ett inkognitofönster.
5. Skapa en andra användare.
6. Logga in med båda användarna.
7. Skicka meddelanden mellan användarna.
8. Skapa ett offentligt rum.
9. Skapa ett privat rum.
10. Bjud in den andra användaren till det privata rummet.
11. Kontrollera att användaren får tillgång till rummet efter inbjudan.

---

# Kontrollera krypteringen

Krypteringen kan kontrolleras genom webbläsarens Developer Tools.

Öppna:

```text
Developer Tools
→ Network
→ WS
→ chatHub
→ Messages
```

Skicka sedan exempelvis:

```text
SUPER_SECRET_MESSAGE
```

Texten ska inte visas i klartext i SignalR-meddelandet.

Istället ska data likna:

```json
{
  "iv": "...",
  "ciphertext": "..."
}
```

Det visar att meddelandet krypterades innan det skickades genom SignalR.

---

# Projektstruktur

En förenklad struktur för projektet:

```text
ChatHub/
│
├── Data/
│   └── ChatDbContext.cs
│
├── DTOs/
│
├── Hubs/
│   └── ChatMessageHub.cs
│
├── Models/
│   ├── User.cs
│   ├── ChatRoom.cs
│   └── ChatRoomMember.cs
│
├── Services/
│   ├── JwtService.cs
│   ├── ChatEncryptionService.cs
│   └── ChatRateLimiter.cs
│
├── Migrations/
│
├── wwwroot/
│   ├── index.html
│   ├── styles.css
│   └── app.js
│
├── Program.cs
└── ChatStart.csproj
```

---

# Säkerhetsöversikt

Projektets säkerhetsflöde kan sammanfattas så här:

```text
Registrering
    ↓
PasswordHasher
    ↓
Hashat lösenord sparas i SQLite

Inloggning
    ↓
Lösenord verifieras
    ↓
JWT skapas
    ↓
SignalR autentiseras med JWT

SignalR ansluter
    ↓
ECDH key exchange
    ↓
HKDF
    ↓
Sessionsnyckel
    ↓
Krypterad AES-kanalnyckel
    ↓
AES-256-GCM
    ↓
Krypterade chattmeddelanden
```

Utöver detta används:

```text
Server-side authorization
Rate limiting
HTTPS
JWT validation
Input validation
```

för att öka säkerheten i applikationen.

---

# AI-användning

AI har använts som stöd under utvecklingen av projektet. Det användes mer som en lärare än som en inbyggd agent. 

AI har bland annat använts för:

- felsökning av fel i backend, frontend och SignalR-kommunikationen

- Skapningen och förbättring av UI (HTML och CSS)

- förklaring av hur SignalR, ECDH, HKDF och AES-GCM samt hur de kan användas tillsammans

- hjälp med att förstå och implementera JWT-autentisering

- hjälp med att bygga behörighetskontroll för privata chattrum

- utökning av rate limiting för login, registrering, SignalR-anslutningar och meddelanden

- hjälp med att förstå säkerhetsrisker och hur servern kan validera användarens behörighet

- förbättring av dokumentation och README

Kod som tagits fram med hjälp av AI har granskats och anpassats till projektet innan den använts.

Målet har varit att använda AI som ett utvecklingsverktyg och samtidigt förstå hur de implementerade lösningarna fungerar.

---

# Sammanfattning

ChatBook kombinerar realtidskommunikation med flera säkerhetsmekanismer.

Projektet använder bland annat:

- hashade lösenord
- JWT-autentisering
- server-side authorization
- privata chattrum
- ECDH-nyckelutbyte
- HKDF
- AES-256-GCM
- rate limiting
- HTTPS

Resultatet är en realtidschatt där autentisering, behörighet, kryptering och skydd mot missbruk är det centrala med projektet.
