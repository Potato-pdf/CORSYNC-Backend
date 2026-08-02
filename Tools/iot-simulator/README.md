# Simulador del dispositivo IoT

Sustituye al prototipo físico (ESP32 + MAX30102 + GSR) para poder probar la app
móvil sin encender el hardware.

Habla el **mismo protocolo que el firmware**: negociación HTTP, WebSocket,
handshake de SignalR terminado en `0x1E` e invocaciones JSON. No usa el cliente
oficial de SignalR ni ninguna dependencia de npm — solo el `WebSocket` nativo de
Node (requiere **Node 22+**; aquí corre 24).

## Levantarlo

```bat
cd Tools\iot-simulator
node simulator.js
```

Se conecta por defecto al **backend desplegado** (`http://corsync.runasp.net/telemetryHub`),
que es el mismo que trae la app móvil en su `.env`. No hay que desplegar nada:
esta carpeta es una herramienta local y no forma parte del publish de la API.

Panel de control: **http://localhost:5300**

### Variables de entorno

| Variable | Default | Para qué |
|---|---|---|
| `HUB_URL` | `http://corsync.runasp.net/telemetryHub` | Contra un backend local: `http://localhost:5213/telemetryHub` |
| `DEVICE_ID` | `ESP32_MAX30102` | Debe coincidir con `AppConfig.DEVICE_ID` de la móvil |
| `UI_PORT` | `5300` | Puerto del panel |
| `RATE_HZ` | `5` | Frecuencia de envío |

## Cómo encaja en el flujo

```
Móvil: "Escanear"  ──StartMeasurement──▶  Hub  ──StartTelemetry──▶  simulador
                                                                        │
simulador  ──SendTelemetry(bpm, gsr, ir)──▶  Hub                        │
                                              │ Validate + Smooth       │
                                              │ CalculateAura           │
                              ┌───────────────┴───────────────┐
                     ReceiveTelemetry                  ReceiveAura
                       (a la móvil)                   (al simulador)
```

El simulador **obedece** `StartTelemetry` / `StopTelemetry`: si no pulsas
"Escanear" en la móvil, no envía nada. El botón *Forzar medición* del panel sirve
para probar sin teléfono.

## Escenarios

El backend deriva el aura de `bpm` + `gsrVoltaje`. Cada escenario fija esos dos
valores en el centro de su franja, con ruido lo bastante chico como para **nunca
cruzar un umbral**: el aura que pides es la que devuelve el backend.

| Escenario | bpm | GSR (V) | Aura resultante |
|---|---|---|---|
| Rojo | ~110 | ~2.40 | Rojo |
| Naranja | ~92 | ~1.70 | Naranja |
| Amarillo | ~80 | ~1.20 | Amarillo |
| Verde | ~70 | ~0.70 | Verde |
| Azul | ~60 | ~0.35 | Azul |
| Morado | ~48 | ~0.10 | Morado |
| **Ciclo** | — | — | Rota por las seis, 6 s cada una |
| **Deriva** | — | — | Paseo lento que atraviesa varias auras |

Todas las lecturas llevan `ir ≈ 87000`, por encima del mínimo de 50000 que exige
`TelemetryProcessor.Validate` — por debajo el backend las descarta en silencio.

**Ciclo** y **Deriva** son los útiles para probar el *aura dominante*: la móvil
promedia la sesión y se queda con la moda, así que con un escenario fijo siempre
saldría el mismo color.

## API del panel

| Método | Ruta | Cuerpo |
|---|---|---|
| GET | `/api/state` | — |
| POST | `/api/scenario` | `{"scenario":"Rojo"}` |
| POST | `/api/measure` | `{"on":true}` |
| POST | `/api/reconnect` | `{}` |
