# Guía de Integración para el Dispositivo IoT (WebSockets/SignalR)

Esta guía detalla cómo configurar el microcontrolador (ej. **ESP32**) del prototipo IoT para comunicarse con el Backend de CORSYNC usando WebSockets (SignalR).

> **IMPORTANTE**: La comunicación es **exclusivamente por SignalR** (WebSockets). El endpoint HTTP POST fue eliminado. Todos los datos de telemetría se envían y reciben por el Hub `/telemetryHub`.

### Referencia rápida de endpoints para el ESP32

```
┌──────────────────────────────────────────────────────────────────┐
│            ENDPOINTS DEL BACKEND (CORSYNC API)                    │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  1. Negociación (HTTP POST):                                     │
│     POST http://<host>/telemetryHub/negotiate?negotiateVersion=1  │
│     → Obtiene el connectionToken                                  │
│                                                                   │
│  2. Conexión WebSocket:                                          │
│     ws://<host>/telemetryHub?id={connectionToken}                 │
│     → Canal bidireccional en tiempo real                          │
│                                                                   │
│  3. Métodos del Hub (por WebSocket):                              │
│     Enviar → SendTelemetry(lectura)      (datos del sensor)      │
│     Enviar → RegisterDevice(deviceId)    (registro inicial)       │
│     Recibir → ReceiveAura({aura})        (cálculo del backend)   │
│     Recibir → StartTelemetry             (orden de la móvil)     │
│     Recibir → StopTelemetry              (orden de la móvil)     │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## 0. Resumen del Flujo

```
ESP32 ─── SendTelemetry(lectura, aura:"") ──→ Backend
                                                  │
                                          ┌───────┴───────┐
                                          │  Validate()   │
                                          │  Smooth()     │
                                          │  CalculateAura│
                                          └───────┬───────┘
                                                  │
         ┌────────────────────────────────────────┤
         │                                        │
         ▼                                        ▼
  ReceiveAura                              ReceiveTelemetry
  {aura: "Verde"}                          (lectura completa)
         │                                        │
         ▼                                        ▼
     ESP32                                   App Móvil
  (cambia LED RGB)                    (muestra datos en tiempo real)
```

**Flujo en tiempo real (~5Hz)**:
1. ESP32 envía lectura con `aura: ""` (string vacío)
2. Backend valida, calcula `BPMPromedio` y `CalculateAura()` (Rojo/Naranja/Amarillo/Verde/Azul/Morado)
3. Backend retorna `ReceiveAura` al ESP32 con el color calculado
4. Backend envía `ReceiveTelemetry` a la App Móvil con la lectura completa
5. Cada 5 segundos, el backend consolida las lecturas en buffer y las guarda en BD

---

## 1. Requisitos para Conectarse a SignalR

### ¿El ESP32 necesita la App Móvil para conectarse?

**No.** El ESP32 se conecta al backend **de forma completamente independiente**. La app móvil es opcional y solo añade control remoto (iniciar/detener medición) y visualización de datos. El ESP32 puede:

- **Conectarse sin móvil**: El ESP32 inicia sesión, se autentica y puede empezar a enviar telemetría por su cuenta. El backend procesará los datos, calculará el aura y lo retornará al ESP32 aunque no haya ninguna app móvil conectada.
- **Con móvil**: La app envía comandos `StartTelemetry`/`StopTelemetry` al servidor, que los reenvía al ESP32 para controlar remotamente las mediciones.

### Requisitos técnicos del ESP32

| Componente | Propósito | Librería sugerida |
|------------|-----------|-------------------|
| **WiFi** | Conexión a internet | `WiFi.h` |
| **HTTP Client** | Negociación inicial de SignalR (GET/POST) | `HTTPClient.h` |
| **WebSocket** | Conexión persistente en tiempo real | `WebSocketsClient.h` (Links2004) |
| **JSON** | Serializar/deserializar mensajes | `ArduinoJson.h` |
| **TLS/SSL** | Conexiones seguras WSS (producción) | `WiFiClientSecure.h` |

### Diagrama de conexión independiente

```
ESP32
  │── WiFi conectado
  │── POST /telemetryHub/negotiate → obtiene connectionToken
  │── WS /telemetryHub?id=token → handshake SignalR
  │── RegisterDevice("ESP32_01")
  │── [opcional] Auto-start: comienza a enviar SendTelemetry
  │     └── ReceiveAura ← Backend (siempre responde)
  │     └── ReceiveTelemetry → solo si hay móvil conectada
  └── Si no hay móvil, el backend igual procesa, calcula aura,
      persiste en BD, y retorna ReceiveAura al ESP32
```

---

## 2. El Reto de SignalR en IoT y Cómo Resolverlo

ASP.NET Core SignalR utiliza un protocolo sobre WebSockets que requiere dos pasos de handshake (negociación):
1. **Negociación inicial (HTTP POST)** para obtener el `connectionToken`.
2. **Conexión WebSocket (WS/WSS)** enviando un mensaje de inicialización en JSON terminado en el caracter especial de control ASCII **0x1E** (Record Separator).

### Opciones de desarrollo para el Firmware (C++/Arduino):
* **Opción A (Recomendada)**: Utilizar una biblioteca que ya implemente el protocolo SignalR Core para ESP32, como la librería **[SignalR-Client-for-ESP32](https://github.com/Marcus-L/SignalR-Client-for-ESP32)** o **[vshymanskyy/TinyGSM](https://github.com/vshymanskyy/TinyGSM)**.
* **Opción B (Robusta y Directa)**: Utilizar una librería de WebSockets estándar (ej: `arduinoWebSockets` de Links2004) y realizar la negociación y el formateo de mensajes JSON manualmente.

---

## 3. Flujo del Protocolo SignalR (Formato Manual)

Si decides implementar la comunicación usando WebSockets directos, debes seguir este flujo exacto:

### Paso 1: Petición HTTP POST de Negociación
Antes de abrir el WebSocket, el ESP32 debe realizar una petición HTTP POST a:
`http://<tu-servidor>/telemetryHub/negotiate?negotiateVersion=1`

El servidor responderá con un JSON como este:
```json
{
  "connectionId": "L-RkK_7XJg3A7e3D",
  "connectionToken": "TOKEN_DE_CONEXION_LARGO",
  "negotiateVersion": 1,
  "availableTransports": [
    {
      "transport": "WebSockets",
      "transferFormats": ["Text", "Binary"]
    }
  ]
}
```
**Acción**: Extrae el valor de `connectionToken`.

### Paso 2: Conectar el WebSocket
Abre la conexión WebSocket a la siguiente URL pasando el token obtenido:
`ws://<tu-servidor>/telemetryHub?id=TOKEN_DE_CONEXION_LARGO`

### Paso 3: Handshake de SignalR
Inmediatamente al conectarse el WebSocket, el ESP32 **debe enviar** el siguiente mensaje de texto de saludo, terminado **obligatoriamente** con el byte `0x1E` (delimitador de mensajes en SignalR):
```json
{"protocol":"json","version":1}
```
*(Nota: El caracter especial al final `` es el byte ASCII `30` o `0x1E`).*

El servidor responderá con un mensaje de confirmación vacío:
```json
{}
```

---

## 4. Comandos e Invocación de Métodos (Formato JSON)

Una vez completado el handshake, la comunicación se realiza mediante mensajes estructurados. Todos los mensajes enviados y recibidos deben terminar con el byte `0x1E`.

### A. Registrar el Dispositivo en el Hub (Client-to-Server)
El ESP32 debe invocar el método `RegisterDevice` enviando su identificador:
* **Mensaje a enviar**:
```json
{"type":1,"target":"RegisterDevice","arguments":["ESP32_MAX30102_01"]}
```

### B. Escuchar Órdenes del Servidor (Server-to-Client)
El ESP32 debe procesar en su bucle de lectura de WebSocket los siguientes comandos enviados por la aplicación móvil (vía el Hub):

* **Comando de Inicio (`StartTelemetry`)**:
  Recibirás este JSON:
  ```json
  {"type":1,"target":"StartTelemetry","arguments":[]} 
  ```
  *Acción*: Activar el sensor `MAX30102` y comenzar el bucle de envío de datos.

* **Comando de Parada (`StopTelemetry`)**:
  Recibirás este JSON:
  ```json
  {"type":1,"target":"StopTelemetry","arguments":[]} 
  ```
  *Acción*: Detener las lecturas físicas del sensor y apagar su LED para ahorrar energía.

* **Comando de Aura Calculada (`ReceiveAura`)**:
  Recibirás este JSON **después de cada lectura que envíes**:
  ```json
  {"type":1,"target":"ReceiveAura","arguments":[{"aura":"Verde"}]}
  ```
  *Acción*: Leer el valor de `"aura"` y utilizarlo para cambiar el color de un indicador LED RGB en el prototipo físico. Los valores posibles son:
  - `"Rojo"` → Alta activación (estrés/enfado/intensidad)
  - `"Naranja"` → Activación moderada-alta (ansiedad/entusiasmo)
  - `"Amarillo"` → Enfoque/concentración
  - `"Verde"` → Estado neutro (calma/estabilidad)
  - `"Azul"` → Relajación (paz/tranquilidad)
  - `"Morado"` → Relajación profunda/meditación

### C. Enviar Datos de Telemetría (Client-to-Server)
Cuando el sensor esté activo y generando lecturas, el ESP32 debe enviarlas invocando el método `SendTelemetry`.

> **Nota**: El campo `aura` se envía como string vacío `""`. El backend calcula el aura basándose en `bpm` y `gsrVoltaje`, y lo retorna al ESP32 vía `ReceiveAura`.

* **Mensaje a enviar (enviar a frecuencia de ~5Hz)**:
```json
{
  "type": 1,
  "target": "SendTelemetry",
  "arguments": [
    {
      "dispositivoId": "ESP32_MAX30102",
      "ir": 87432,
      "bpm": 72.5,
      "bpmPromedio": 0,
      "gsrRaw": 1340,
      "gsrVoltaje": 1.079,
      "aura": ""
    }
  ]
}
```

### D. Respuesta del Backend

#### ReceiveAura (retornado al ESP32)
El backend calcula el aura y lo retorna al ESP32 que envió la lectura:
```json
{"type":1,"target":"ReceiveAura","arguments":[{"aura":"Verde"}]}
```

#### ReceiveTelemetry (enviado a la App Móvil)
El backend envía la lectura completa (con aura calculada) a la app móvil asociada al dispositivo:
```json
{
  "dispositivoId": "ESP32_MAX30102",
  "ir": 87432,
  "bpm": 72.5,
  "bpmPromedio": 73,
  "gsrRaw": 1340,
  "gsrVoltaje": 1.079,
  "aura": "Verde",
  "fechaHora": "2026-07-02T16:09:55.426Z"
}
```

#### Colores del Aura
| Aura | Condición | Significado |
|------|-----------|-------------|
| **Rojo** | bpm > 100 && gsrVoltaje > 2.0 | Alta activación (estrés/enfado) |
| **Naranja** | bpm > 85 && gsrVoltaje > 1.5 | Activación moderada-alta (ansiedad) |
| **Amarillo** | bpm > 75 && gsrVoltaje > 1.0 | Enfoque/concentración |
| **Verde** | bpm >= 65 && gsrVoltaje >= 0.5 | Estado neutro (calma) |
| **Azul** | bpm >= 55 && gsrVoltaje >= 0.2 | Relajación |
| **Morado** | Default | Relajación profunda/meditación |

---

## 5. Código de Ejemplo para ESP32 (Arduino IDE)

Este ejemplo básico ilustra el handshake manual y la gestión de flujo utilizando la biblioteca `arduinoWebSockets` y `ArduinoJson`.

```cpp
#include <WiFi.h>
#include <HTTPClient.h>
#include <WebSocketsClient.h>
#include <ArduinoJson.h>

const char* ssid = "TU_WIFI_SSID";
const char* password = "TU_WIFI_PASSWORD";

const char* serverHost = "api.corsync.com"; // Sin http://
const int serverPort = 80; // O 443 si usas HTTPS/WSS (requiere cliente seguro)
const char* deviceId = "ESP32_MAX30102_01";

WebSocketsClient webSocket;
bool isMeasuring = false;
unsigned long lastSendTime = 0;
const int sendInterval = 200; // 5Hz (cada 200ms)

// Caracter de control para SignalR (ASCII 30)
const char recordSeparator = 0x1E; 

// Obtener el ConnectionToken por HTTP
String negotiateSignalR() {
  HTTPClient http;
  String url = "http://" + String(serverHost) + ":" + String(serverPort) + "/telemetryHub/negotiate?negotiateVersion=1";
  
  http.begin(url);
  int httpCode = http.POST("");
  
  String token = "";
  if (httpCode == 200) {
    String payload = http.getString();
    JsonDocument doc;
    deserializeJson(doc, payload);
    token = doc["connectionToken"].as<String>();
  }
  http.end();
  return token;
}

// Procesar eventos del WebSocket
void webSocketEvent(WStype_t type, uint8_t * payload, size_t length) {
  String message = "";
  
  switch(type) {
    case WStype_DISCONNECTED:
      Serial.println("[WS] Desconectado!");
      isMeasuring = false;
      break;
      
    case WStype_CONNECTED: {
      Serial.println("[WS] Conectado físicamente! Enviando handshake de SignalR...");
      // Enviar Handshake Inicial
      String handshake = "{\"protocol\":\"json\",\"version\":1}" + String(recordSeparator);
      webSocket.sendTXT(handshake);
      
      // Registrar Dispositivo
      String regMsg = "{\"type\":1,\"target\":\"RegisterDevice\",\"arguments\":[\"" + String(deviceId) + "\"]}" + String(recordSeparator);
      webSocket.sendTXT(regMsg);
      break;
    }
    case WStype_TEXT: {
      message = String((char*)payload);
      // Eliminar el record separator del final para poder deserializar el JSON
      if (message.endsWith(String(recordSeparator))) {
        message = message.substring(0, message.length() - 1);
      }
      
      JsonDocument doc;
      DeserializationError error = deserializeJson(doc, message);
      if (!error) {
        int msgType = doc["type"];
        // type 1 representa una invocación de método desde el servidor
        if (msgType == 1) {
          String target = doc["target"].as<String>();
          if (target == "StartTelemetry") {
            Serial.println("[WS] Servidor solicitó iniciar medición!");
            isMeasuring = true;
          } else if (target == "StopTelemetry") {
            Serial.println("[WS] Servidor solicitó detener medición!");
            isMeasuring = false;
          } else if (target == "ReceiveAura") {
            String aura = doc["arguments"][0]["aura"].as<String>();
            Serial.println("[WS] Aura calculada recibida: " + aura);
            // TODO: Cambiar color de LED RGB físico según el aura
          }
        }
      }
      break;
    }
  }
}

void setup() {
  Serial.begin(115200);
  WiFi.begin(ssid, password);
  
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi Conectado!");

  // 1. Negociación de SignalR
  String connectionToken = negotiateSignalR();
  if (connectionToken == "") {
    Serial.println("Error en la negociación de SignalR.");
    return;
  }
  
  // 2. Configurar WebSocket
  String wsUrl = "/telemetryHub?id=" + connectionToken;
  webSocket.begin(serverHost, serverPort, wsUrl);
  webSocket.onEvent(webSocketEvent);
  webSocket.setReconnectInterval(5000);
}

void loop() {
  webSocket.loop();

  // Bucle de lectura y transmisión del sensor
  if (isMeasuring && millis() - lastSendTime >= sendInterval) {
    lastSendTime = millis();
    
    // Simulación de lectura física de sensores (MAX30102 y GSR)
    float bpm = 72.5 + (random(-10, 11) / 10.0); // Simular 72.5 + ruido
    long irValue = 87000 + random(-1000, 1000); 
    int gsrRaw = 1300 + random(-50, 50);
    float gsrVoltaje = (gsrRaw * 3.3) / 4095.0; // Conversión ADC a voltaje (ej. ESP32 12-bit)
    
    // Crear payload JSON para SignalR (aura vacía, el backend la calcula)
    JsonDocument doc;
    doc["type"] = 1;
    doc["target"] = "SendTelemetry";
    
    JsonArray arguments = doc.createNestedArray("arguments");
    JsonObject lectura = arguments.createNestedObject();
    lectura["dispositivoId"] = deviceId;
    lectura["bpm"] = bpm;
    lectura["ir"] = irValue;
    lectura["bpmPromedio"] = 0;
    lectura["gsrRaw"] = gsrRaw;
    lectura["gsrVoltaje"] = gsrVoltaje;
    lectura["aura"] = "";
    
    String output;
    serializeJson(doc, output);
    output += recordSeparator; // Agregar delimitador SignalR
    
    webSocket.sendTXT(output);
    Serial.println("[WS] Telemetría enviada: BPM=" + String(bpm) + ", GSR=" + String(gsrRaw) + " (" + String(gsrVoltaje) + "V)");
  }
}
```

> [!WARNING]
> **Seguridad TLS (WSS / Puerto 443)**: En entornos productivos debes utilizar HTTPS y WSS. En el ESP32 necesitarás configurar un cliente seguro (`WiFiClientSecure` o similar) y cargar el certificado raíz (CA Root) del servidor para validar el handshake de cifrado SSL/TLS.
