# Guía de Integración para Aplicación Móvil (SignalR WebSockets)

Esta guía explica los pasos necesarios para conectar la aplicación móvil al backend de CORSYNC y recibir la telemetría cardíaca en tiempo real mediante **SignalR**.

---

## 0. Endpoints REST API de la Aplicación Móvil

Para soportar las características de la aplicación móvil (autenticación, perfil de usuario, lecturas de aura histórica y gamificación), el backend provee los siguientes módulos de APIs REST. Todas las peticiones deben utilizar cabeceras JSON (`Content-Type: application/json`).

---

### 1. Módulo: Autenticación (`api/Auth`)

Gestiona el inicio de sesión, registro de usuarios, renovación de tokens mediante Refresh Tokens y cierre de sesión.

| Método | Endpoint | Autenticación | Descripción |
| :--- | :--- | :---: | :--- |
| **`POST`** | `/api/Auth/register` | Público | Registra un usuario y devuelve tokens JWT + Refresh. |
| **`POST`** | `/api/Auth/login` | Público | Valida credenciales y devuelve tokens JWT + Refresh. |
| **`POST`** | `/api/Auth/logout` | **JWT** | Cierra la sesión revocando el Refresh Token. |
| **`POST`** | `/api/Auth/refresh-token` | Público | Renueva un JWT expirado usando un Refresh Token activo. |

#### Contratos JSON de Autenticación:

* **Registro (`POST /api/Auth/register`):**
  - **Request:**
    ```json
    {
      "username": "espiritu_libre",
      "email": "zen@corsync.com",
      "password": "PasswordZen123!",
      "nombreCompleto": "Carlos Zen"
    }
    ```
  - **Response (201 Created):**
    ```json
    {
      "token": "eyJhbGciOiJIUzI1NiIsInR5c...",
      "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2gg...",
      "expiration": "2026-07-02T05:00:00Z",
      "user": {
        "id": 3,
        "username": "espiritu_libre",
        "email": "zen@corsync.com",
        "nombreCompleto": "Carlos Zen",
        "nombreEspiritual": "",
        "signoZodiacal": "",
        "fotoUrl": null,
        "role": "Cliente",
        "fechaRegistro": "2026-07-01T21:00:00Z"
      }
    }
    ```

* **Inicio de Sesión (`POST /api/Auth/login`):**
  - **Request:**
    ```json
    {
      "username": "espiritu_libre",
      "password": "PasswordZen123!"
    }
    ```
  - **Response (200 OK):** Mismo objeto JSON `AuthResponse` devuelto por el registro.

* **Cerrar Sesión (`POST /api/Auth/logout`):**
  - **Cabecera:** `Authorization: Bearer <JWT_TOKEN>`
  - **Request:**
    ```json
    {
      "token": "eyJhbGciOiJIUzI1NiIsInR5c...",
      "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2gg..."
    }
    ```
  - **Response (200 OK):**
    ```json
    {
      "message": "Sesión cerrada exitosamente."
    }
    ```

* **Renovar Token (`POST /api/Auth/refresh-token`):**
  - **Request:**
    ```json
    {
      "token": "eyJhbGciOiJIUzI1NiIsInR5c...", // Token JWT expirado
      "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2gg..." // Refresh token activo
    }
    ```
  - **Response (200 OK):** Devuelve un nuevo `AuthResponse` con tokens rotados.

---

### 2. Módulo: Usuario (`api/User`)

Gestiona el perfil espiritual y las estadísticas acumuladas de meditación y telemetría del usuario.

| Método | Endpoint | Autenticación | Descripción |
| :--- | :--- | :---: | :--- |
| **`GET`** | `/api/User/profile` | **JWT** | Obtiene el perfil completo del usuario. |
| **`PUT`** | `/api/User/profile` | **JWT** | Actualiza nombre espiritual, signo del zodiaco y foto. |
| **`GET`** | `/api/User/stats` | **JWT** | Retorna el BPM promedio, estrés, sesiones y rachas. |

#### Contratos JSON de Usuario:

* **Actualizar Perfil (`PUT /api/User/profile`):**
  - **Request:**
    ```json
    {
      "nombreCompleto": "Carlos Zen",
      "nombreEspiritual": "Luz de Luna",
      "signoZodiacal": "Piscis", // Aries, Tauro, Géminis, Cáncer, Leo, Virgo, Libra, Escorpio, Sagitario, Capricornio, Acuario, Piscis
      "fotoUrl": "https://images.corsync.com/avatars/user3.png"
    }
    ```
  - **Response (200 OK):** Devuelve el objeto `UserInfo` actualizado.

* **Obtener Estadísticas (`GET /api/User/stats`):**
  - **Response (200 OK):**
    ```json
    {
      "bpmPromedio": 74.2,
      "nivelEstresPromedio": 32.5,
      "sesionesTotales": 12,
      "auraDominante": "Verde",
      "rachaActualDias": 5,
      "ultimaSesion": "2026-07-01T20:30:00Z"
    }
    ```

---

### 3. Módulo: Historial de Lecturas (`api/Readings`)

Permite guardar y consultar las sesiones de escaneo biométrico y clasificación de aura completadas.

| Método | Endpoint | Autenticación | Descripción |
| :--- | :--- | :---: | :--- |
| **`GET`** | `/api/Readings` | **JWT** | Lista lecturas históricas (soporta `page` y `pageSize`). |
| **`GET`** | `/api/Readings/{id}` | **JWT** | Detalle completo de una lectura específica. |
| **`POST`** | `/api/Readings` | **JWT** | Guarda una nueva sesión tras realizar un escaneo en la app. |
| **`GET`** | `/api/Readings/summary` | **JWT** | Resumen global de auras y su distribución. |

#### Contratos JSON de Lecturas:

* **Guardar Lectura (`POST /api/Readings`):**
  - **Request:**
    ```json
    {
      "dispositivoId": "ESP32_MAX30102_01",
      "bpmPromedio": 68.4,
      "bpmMaximo": 85.0,
      "bpmMinimo": 60.0,
      "gsrRawPromedio": 1240,
      "gsrVoltajePromedio": 0.998,
      "nivelEstres": 22.50,
      "auraDominante": "Verde",
      "notas": "Excelente sesión de respiración profunda",
      "duracionSegundos": 300,
      "fechaInicio": "2026-07-01T20:25:00Z",
      "fechaFin": "2026-07-01T20:30:00Z"
    }
    ```
  - **Response (201 Created):** Retorna el objeto guardado con su `id` asignado.

* **Resumen Global (`GET /api/Readings/summary`):**
  - **Response (200 OK):**
    ```json
    {
      "bpmPromedioGlobal": 72.8,
      "nivelEstresPromedio": 28.4,
      "totalSesiones": 12,
      "auraMasFrecuente": "Verde",
      "distribucionAuras": {
        "Verde": 7,
        "Azul": 3,
        "Amarilla": 2
      }
    }
    ```

---

### 4. Módulo: Desafíos y Gamificación (`api/Challenges` y `api/Medals`)

Listado y control de desafíos espirituales y medallas desbloqueadas por el usuario.

| Método | Endpoint | Autenticación | Descripción |
| :--- | :--- | :---: | :--- |
| **`GET`** | `/api/Challenges` | **JWT** | Lista desafíos activos con el progreso del usuario. |
| **`PUT`** | `/api/Challenges/{id}/progress` | **JWT** | Actualiza manualmente el progreso de un desafío. |
| **`GET`** | `/api/Medals` | **JWT** | Obtiene las medallas que el usuario ha desbloqueado. |

#### Contratos JSON de Gamificación:

* **Lista de Desafíos (`GET /api/Challenges`):**
  - **Response (200 OK):**
    ```json
    [
      {
        "id": 1,
        "titulo": "Primera Lectura",
        "descripcion": "Realiza tu primer escaneo de aura",
        "icono": "🌟",
        "tipo": "Sesiones",
        "metaObjetivo": 1,
        "unidadMedida": "sesiones",
        "puntos": 10,
        "progresoActual": 1,
        "completado": true,
        "porcentajeProgreso": 100.0,
        "fechaCompletado": "2026-06-30T10:15:00Z"
      },
      {
        "id": 4,
        "titulo": "Semana Zen",
        "descripcion": "Completa sesiones de escaneo durante 7 días seguidos",
        "icono": "🧘",
        "tipo": "Racha",
        "metaObjetivo": 7,
        "unidadMedida": "días",
        "puntos": 100,
        "progresoActual": 5,
        "completado": false,
        "porcentajeProgreso": 71.4,
        "fechaCompletado": null
      }
    ]
    ```

* **Actualizar Progreso (`PUT /api/Challenges/{id}/progress`):**
  - **Request:**
    ```json
    {
      "progresoActual": 6
    }
    ```
  - **Response (200 OK):** Retorna el objeto del desafío con el progreso actualizado.

* **Listar Medallas Obtenidas (`GET /api/Medals`):**
  - **Response (200 OK):**
    ```json
    [
      {
        "id": 1,
        "nombre": "Primer Escaneo",
        "descripcion": "Completaste tu primera lectura de aura",
        "icono": "🏅",
        "fechaObtenida": "2026-06-30T10:15:00Z"
      }
    ]
    ```

---

### Almacenamiento Seguro del Token en la App Móvil

Se recomienda almacenar el token JWT de forma persistente y segura en el dispositivo móvil:
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
