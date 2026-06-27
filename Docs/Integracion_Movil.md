# Guía de Integración para Aplicación Móvil (SignalR WebSockets)

Esta guía explica los pasos necesarios para conectar la aplicación móvil al backend de CORSYNC y recibir la telemetría cardíaca en tiempo real mediante **SignalR**.

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
      String timestamp = lectura["fechaHora"];

      print("BPM Recibido: $bpm | Promedio: $bpmPromedio | IR: $irValue");
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
    console.log(`BPM: ${lectura.bpm}, Promedio: ${lectura.bpmPromedio}, IR: ${lectura.ir}`);
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
  "dispositivoId": "ESP32_MAX30102_01",
  "bpm": 74.2,
  "bpmPromedio": 73,
  "ir": 102432,
  "fechaHora": "2026-06-27T15:52:12.345Z"
}
```

> [!TIP]
> **Optimización visual**: Utiliza el valor de `ir` (Infrarrojo) para dibujar la onda del fotopletismograma (la curva del pulso cardíaco) y el valor de `bpmPromedio` como el número estable a mostrar en pantalla.
