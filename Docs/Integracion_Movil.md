# Guía de Integración para Aplicación Móvil (SignalR WebSockets)

Esta guía explica los pasos necesarios para conectar la aplicación móvil al backend de CORSYNC y recibir la telemetría cardíaca en tiempo real mediante **SignalR**.

---

## 0. Autenticación y Perfil de Usuario (REST API)

Antes de iniciar la conexión de telemetría, la aplicación móvil debe autenticar al usuario o registrar una cuenta nueva. El backend expone un conjunto de endpoints REST para gestionar el ciclo de vida del usuario.

### Endpoints de Autenticación

| Método | Endpoint | Autenticación | Descripción |
| :--- | :--- | :---: | :--- |
| **`POST`** | `/api/Auth/register` | Ninguna (Público) | Registra una nueva cuenta de usuario (forzando rol `Cliente`). |
| **`POST`** | `/api/Auth/login` | Ninguna (Público) | Valida credenciales y retorna un token JWT válido por 2 horas. |
| **`GET`** | `/api/Auth/profile` | **JWT Requerido** | Retorna la información de perfil del usuario autenticado. |

---

### Contratos de Datos (JSON)

#### 1. Registro de Usuario (`POST /api/Auth/register`)
* **Cuerpo de la Petición (Request):**
```json
{
  "username": "nombre_usuario",
  "email": "usuario@ejemplo.com",
  "password": "PasswordSeguro123!",
  "nombreCompleto": "Nombre Completo del Usuario"
}
```
* **Respuesta Exitosa (`201 Created`):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiration": "2026-06-30T07:30:00Z",
  "user": {
    "id": 3,
    "username": "nombre_usuario",
    "email": "usuario@ejemplo.com",
    "nombreCompleto": "Nombre Completo del Usuario",
    "role": "Cliente",
    "fechaRegistro": "2026-06-30T05:30:00Z"
  }
}
```
* **Respuestas de Error:**
  - **`400 Bad Request`**: Datos inválidos o faltantes (ej: contraseña menor a 8 caracteres).
  - **`409 Conflict`**: El nombre de usuario o correo electrónico ya están en uso.

#### 2. Inicio de Sesión (`POST /api/Auth/login`)
* **Cuerpo de la Petición (Request):**
```json
{
  "username": "nombre_usuario",
  "password": "PasswordSeguro123!"
}
```
* **Respuesta Exitosa (`200 OK`):**
Retorna la misma estructura `AuthResponse` que el registro (token JWT + información del usuario).
* **Respuestas de Error:**
  - **`401 Unauthorized`**: Usuario o contraseña incorrectos, o usuario inactivo.

#### 3. Obtener Perfil (`GET /api/Auth/profile`)
* **Cabecera requerida:** `Authorization: Bearer <TU_JWT_TOKEN>`
* **Respuesta Exitosa (`200 OK`):**
```json
{
  "id": 3,
  "username": "nombre_usuario",
  "email": "usuario@ejemplo.com",
  "nombreCompleto": "Nombre Completo del Usuario",
  "role": "Cliente",
  "fechaRegistro": "2026-06-30T05:30:00Z"
}
```

---

### Almacenamiento Seguro del Token en la App Móvil

Se recomienda almacenar el token de forma persistente y segura en el dispositivo móvil:
- **Flutter:** Usar el paquete `flutter_secure_storage`.
- **React Native / Expo:** Usar `expo-secure-store` o `react-native-keychain`.

Una vez obtenido y almacenado el token, este debe ser adjuntado a la URL del WebSocket en el parámetro `access_token` para poder conectar exitosamente a SignalR (ver sección siguiente).

---

## 1. Configuración de Conexión

La aplicación móvil se conecta al servidor a través de WebSockets utilizando la biblioteca de cliente oficial de SignalR. 

* **Ruta de conexión (WS/WSS)**: `wss://<tu-servidor-url>/telemetryHub`
* **Autenticación (JWT)**: Debido a limitaciones de los WebSockets en dispositivos móviles y navegadores, el token JWT se debe pasar a través del query string con el nombre `access_token`.

### Ejemplo de URL de Conexión:
`wss://api.corsync.com/telemetryHub?access_token=eyJhbGciOiJIUzI1Ni...`

---

## 2. Flujo de Comunicación y Métodos

El ciclo de vida de una sesión de medición en la app móvil debe seguir este orden:

```
[ Conectar ] ──> [ RegisterMobile ] ──> [ StartMeasurement ] ──> ( Escuchar ReceiveTelemetry ) ──> [ StopMeasurement ] ──> [ Desconectar ]
```

### Métodos a Invocar (Client-to-Hub)
* **`RegisterMobile(string deviceId)`**: Vincula tu sesión móvil al grupo exclusivo del dispositivo que se va a medir. Debes llamarlo inmediatamente después de establecer la conexión.
* **`StartMeasurement(string deviceId)`**: Solicita al backend que ordene al dispositivo IoT físico iniciar las lecturas y el envío de datos.
* **`StopMeasurement(string deviceId)`**: Solicita al backend que ordene al dispositivo IoT apagar el sensor y detener las transmisiones.

### Eventos a Escuchar (Hub-to-Client)
* **`ReceiveTelemetry`**: Evento disparado cada vez que llega una lectura procesada del sensor. Entrega un objeto JSON con la estructura de la telemetría.

---

## 3. Ejemplos de Implementación

### Opción A: Flutter (Dart)
Asegúrate de agregar el paquete `signalr_netcore` a tu `pubspec.yaml`.

```dart
import 'package:signalr_netcore/signalr_netcore.dart';

class TelemetryService {
  HubConnection? _hubConnection;
  final String serverUrl = "https://api.corsync.com/telemetryHub"; // Usar HTTPS, la librería cambia a WSS automáticamente
  final String deviceId = "ESP32_MAX30102_01";
  final String jwtToken = "TU_JWT_TOKEN";

  Future<void> initConnection() async {
    // Configurar conexión con el Token JWT por query parameter
    _hubConnection = HubConnectionBuilder()
        .withUrl("$serverUrl?access_token=$jwtToken")
        .withAutomaticReconnect()
        .build();

    // Registrar escuchas de eventos antes de iniciar la conexión
    _hubConnection!.on("ReceiveTelemetry", _handleIncomingTelemetry);

    // Iniciar conexión
    await _hubConnection!.start();
    print("Conexión establecida con el Hub de Telemetría.");

    // Paso 1: Registrar el móvil para escuchar al dispositivo
    await _hubConnection!.invoke("RegisterMobile", args: [deviceId]);
  }

  // Enviar comando para que el sensor empiece a medir
  Future<void> comenzarMedicion() async {
    if (_hubConnection?.state == HubConnectionState.Connected) {
      await _hubConnection!.invoke("StartMeasurement", args: [deviceId]);
      print("Solicitud de inicio de medición enviada.");
    }
  }

  // Enviar comando para detener el sensor
  Future<void> detenerMedicion() async {
    if (_hubConnection?.state == HubConnectionState.Connected) {
      await _hubConnection!.invoke("StopMeasurement", args: [deviceId]);
      print("Solicitud de detención de medición enviada.");
    }
  }

  // Manejar datos recibidos
  void _handleIncomingTelemetry(List<Object?>? arguments) {
    if (arguments != null && arguments.isNotEmpty) {
      final lectura = arguments.first as Map<String, dynamic>;
      
      // Mapear datos recibidos
      double bpm = lectura["bpm"];
      int bpmPromedio = lectura["bpmPromedio"];
      int irValue = lectura["ir"];
      int gsrRaw = lectura["gsrRaw"] ?? 0;
      double gsrVoltaje = lectura["gsrVoltaje"] ?? 0.0;
      String aura = lectura["aura"] ?? "";
      String timestamp = lectura["fechaHora"];

      print("BPM Recibido: $bpm | Promedio: $bpmPromedio | IR: $irValue | GSR Raw: $gsrRaw | Voltaje: $gsrVoltaje | Aura: $aura");
      // TODO: Actualizar gráfica en tiempo real en la UI
    }
  }

  Future<void> disconnect() async {
    await _hubConnection?.stop();
  }
}
```

### Opción B: React Native / Expo (JavaScript/TypeScript)
Instala el cliente oficial: `npm install @microsoft/signalr`

```typescript
import * as signalR from "@microsoft/signalr";

const serverUrl = "https://api.corsync.com/telemetryHub";
const deviceId = "ESP32_MAX30102_01";
const jwtToken = "TU_JWT_TOKEN";

// Inicializar conexión
const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${serverUrl}?access_token=${jwtToken}`)
    .withAutomaticReconnect()
    .build();

// Registrar el manejador del evento de recepción de datos
connection.on("ReceiveTelemetry", (lectura) => {
    console.log(`BPM: ${lectura.bpm}, Promedio: ${lectura.bpmPromedio}, IR: ${lectura.ir}, GSR Raw: ${lectura.gsrRaw}, Voltaje: ${lectura.gsrVoltaje}V, Aura: ${lectura.aura}`);
    // TODO: Actualizar estado de React / Gráfica
});

async function startSession() {
    try {
        await connection.start();
        console.log("Conectado a SignalR.");

        // Registrar la aplicación en el canal del dispositivo
        await connection.invoke("RegisterMobile", deviceId);

        // Solicitar al hardware que comience a emitir datos
        await connection.invoke("StartMeasurement", deviceId);
    } catch (err) {
        console.error("Error al iniciar sesión de telemetría:", err);
    }
}

async function stopSession() {
    try {
        // Enviar orden de parada al dispositivo
        await connection.invoke("StopMeasurement", deviceId);
        await connection.stop();
        console.log("Conexión cerrada.");
    } catch (err) {
        console.error(err);
    }
}
```

---

## 4. Estructura del Payload Recibido (`ReceiveTelemetry`)

El JSON que recibirá la aplicación móvil representa el objeto de telemetría suavizado y filtrado por el backend:

```json
{
  "id": 0,
  "dispositivoId": "ESP32_MAX30102",
  "ir": 87432,
  "bpm": 72.5,
  "bpmPromedio": 71,
  "gsrRaw": 1340,
  "gsrVoltaje": 1.079,
  "aura": "Roja",
  "fechaHora": "2026-06-29T22:51:35Z"
}
```

### Descripción de Campos Adicionales (GSR & Aura):
* **`gsrRaw`** (int): Valor analógico crudo de conductividad de la piel (Galvanic Skin Response).
* **`gsrVoltaje`** (decimal): Voltaje equivalente del sensor (de 0.0V a 3.3V) según la resistencia cutánea.
* **`aura`** (string): Clasificación calculada para representar el nivel de estrés o estado emocional (ej: `"Roja"`, `"Azul"`, `"Verde"`).

> [!TIP]
> **Optimización visual**: Utiliza el valor de `ir` (Infrarrojo) para dibujar la onda del fotopletismograma (la curva del pulso cardíaco) y el valor de `bpmPromedio` como el número estable a mostrar en pantalla. El valor de `gsrVoltaje` y `aura` te permitirán pintar indicadores de estrés y cambiar dinámicamente colores de interfaz inspirados en el color del aura.
