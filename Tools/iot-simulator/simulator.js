#!/usr/bin/env node
/**
 * Simulador del prototipo IoT (ESP32 + MAX30102 + GSR) para CORSYNC.
 *
 * Habla el protocolo SignalR "a mano" sobre el WebSocket nativo de Node (>=22),
 * igual que lo hace el firmware del ESP32: negotiate por HTTP, handshake con el
 * separador 0x1E y luego invocaciones JSON. Cero dependencias de npm — lo que
 * corre aquí es lo mismo que corre en el microcontrolador.
 *
 * Uso:  node simulator.js
 * Config por variables de entorno: HUB_URL, DEVICE_ID, UI_PORT, RATE_HZ.
 */

const http = require('node:http');
const https = require('node:https');
const fs = require('node:fs');
const path = require('node:path');

// Apunta al backend YA DESPLEGADO: es el mismo host que trae la app móvil en su
// .env, así que simulador y móvil caen en el mismo grupo del hub sin tocar nada
// del servidor. Para probar contra un backend local: HUB_URL=http://localhost:5213/telemetryHub
const HUB_URL   = process.env.HUB_URL   || 'http://corsync.runasp.net/telemetryHub';
const DEVICE_ID = process.env.DEVICE_ID || 'ESP32_MAX30102';
const UI_PORT   = Number(process.env.UI_PORT || 5300);
const RATE_HZ   = Number(process.env.RATE_HZ || 5);

const RS = '\x1e'; // Record Separator: delimitador de mensajes de SignalR

// ─────────────────────────────────────────────────────────────────────────────
//  Escenarios
//
//  Los rangos están elegidos para caer siempre del lado correcto de los cortes
//  de TelemetryProcessor.CalculateAura y para pasar Validate() (IR >= 50000,
//  30 <= BPM <= 220). El ruido nunca es suficiente para cruzar un umbral, así
//  que el aura que pides es exactamente el aura que te devuelve el backend.
// ─────────────────────────────────────────────────────────────────────────────
const SCENARIOS = {
  Rojo:     { bpm: 110, bpmJitter: 3, gsr: 2.40, gsrJitter: 0.10, desc: 'Alta activación (estrés/enfado)' },
  Naranja:  { bpm:  92, bpmJitter: 2, gsr: 1.70, gsrJitter: 0.08, desc: 'Activación moderada-alta (ansiedad)' },
  Amarillo: { bpm:  80, bpmJitter: 2, gsr: 1.20, gsrJitter: 0.07, desc: 'Enfoque / concentración' },
  Verde:    { bpm:  70, bpmJitter: 2, gsr: 0.70, gsrJitter: 0.07, desc: 'Estado neutro (calma)' },
  Azul:     { bpm:  60, bpmJitter: 2, gsr: 0.35, gsrJitter: 0.05, desc: 'Relajación' },
  Morado:   { bpm:  48, bpmJitter: 2, gsr: 0.10, gsrJitter: 0.03, desc: 'Relajación profunda / meditación' },
};

const AURA_ORDER = Object.keys(SCENARIOS);

const state = {
  hubUrl: HUB_URL,
  deviceId: DEVICE_ID,
  connection: 'desconectado',   // desconectado | negociando | conectando | conectado
  measuring: false,
  measuringSource: null,        // 'movil' | 'manual'
  scenario: 'Verde',            // clave de SCENARIOS | 'ciclo' | 'deriva'
  sent: 0,
  lastReading: null,
  lastAura: null,               // lo que el backend devuelve por ReceiveAura
  connectedSince: null,
  log: [],
};

function log(msg, level = 'info') {
  const entry = { t: new Date().toISOString(), level, msg };
  state.log.unshift(entry);
  if (state.log.length > 200) state.log.pop();
  const tag = level === 'error' ? '[!]' : level === 'ok' ? '[+]' : '[·]';
  console.log(`${tag} ${msg}`);
}

// ─────────────────────────────────────────────────────────────────────────────
//  Generación de lecturas
// ─────────────────────────────────────────────────────────────────────────────
const noise = (amp) => (Math.random() * 2 - 1) * amp;

let cycleIndex = 0;
let cycleTick = 0;
const CYCLE_SECONDS = 6;

// Estado interno del modo "deriva": un paseo aleatorio lento que atraviesa
// varias auras, para probar el cálculo del aura dominante de la sesión.
let drift = { bpm: 75, gsr: 1.0, dBpm: 0.4, dGsr: 0.01 };

function currentTarget() {
  if (state.scenario === 'ciclo') {
    return SCENARIOS[AURA_ORDER[cycleIndex % AURA_ORDER.length]];
  }
  if (state.scenario === 'deriva') {
    drift.bpm += drift.dBpm + noise(0.3);
    drift.gsr += drift.dGsr + noise(0.01);
    if (drift.bpm > 115 || drift.bpm < 45) drift.dBpm *= -1;
    if (drift.gsr > 2.6 || drift.gsr < 0.05) drift.dGsr *= -1;
    drift.bpm = Math.min(118, Math.max(45, drift.bpm));
    drift.gsr = Math.min(2.7, Math.max(0.05, drift.gsr));
    return { bpm: drift.bpm, bpmJitter: 1, gsr: drift.gsr, gsrJitter: 0.02 };
  }
  return SCENARIOS[state.scenario] || SCENARIOS.Verde;
}

function buildReading() {
  const t = currentTarget();
  const bpm = Math.round((t.bpm + noise(t.bpmJitter)) * 10) / 10;
  const gsrVoltaje = Math.max(0, Math.round((t.gsr + noise(t.gsrJitter)) * 1000) / 1000);
  // ADC de 12 bits del ESP32 sobre 3.3 V, el mismo cálculo del firmware.
  const gsrRaw = Math.round((gsrVoltaje * 4095) / 3.3);
  const ir = Math.round(87000 + noise(1500)); // >= 50000 o el backend descarta la lectura

  return {
    dispositivoId: state.deviceId,
    ir,
    bpm,
    bpmPromedio: 0,       // lo calcula el backend
    gsrRaw,
    gsrVoltaje,
    aura: '',             // lo calcula el backend
  };
}

// ─────────────────────────────────────────────────────────────────────────────
//  Cliente SignalR manual
// ─────────────────────────────────────────────────────────────────────────────
let ws = null;
let sendTimer = null;
let reconnectTimer = null;
let handshakeDone = false;

function post(url) {
  const client = url.startsWith('https:') ? https : http;
  return new Promise((resolve, reject) => {
    const req = client.request(url, { method: 'POST', headers: { 'Content-Length': 0 } }, (res) => {
      let body = '';
      res.on('data', (c) => (body += c));
      res.on('end', () => {
        if (res.statusCode !== 200) return reject(new Error(`HTTP ${res.statusCode}: ${body.slice(0, 200)}`));
        resolve(body);
      });
    });
    req.on('error', reject);
    req.end();
  });
}

function send(obj) {
  if (!ws || ws.readyState !== 1) return false;
  ws.send(JSON.stringify(obj) + RS);
  return true;
}

function invoke(target, ...args) {
  return send({ type: 1, target, arguments: args });
}

async function connect() {
  clearTimeout(reconnectTimer);
  handshakeDone = false;
  state.connection = 'negociando';

  let token;
  try {
    const raw = await post(`${state.hubUrl}/negotiate?negotiateVersion=1`);
    token = JSON.parse(raw).connectionToken;
    if (!token) throw new Error('la negociación no devolvió connectionToken');
  } catch (e) {
    state.connection = 'desconectado';
    log(`Negociación fallida contra ${state.hubUrl} → ${e.message}`, 'error');
    return scheduleReconnect();
  }

  const wsUrl = state.hubUrl.replace(/^http/, 'ws') + `?id=${encodeURIComponent(token)}`;
  state.connection = 'conectando';
  log(`Negociación OK. Abriendo WebSocket…`);

  ws = new WebSocket(wsUrl);

  ws.addEventListener('open', () => {
    // Handshake obligatorio de SignalR, terminado en 0x1E.
    ws.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS);
  });

  ws.addEventListener('message', (ev) => {
    const raw = typeof ev.data === 'string' ? ev.data : String(ev.data);
    for (const chunk of raw.split(RS)) {
      if (!chunk) continue;
      let msg;
      try { msg = JSON.parse(chunk); } catch { continue; }

      if (!handshakeDone) {
        // La respuesta al handshake es {} o {"error": "..."}.
        handshakeDone = true;
        if (msg.error) {
          log(`Handshake rechazado: ${msg.error}`, 'error');
          ws.close();
          return;
        }
        state.connection = 'conectado';
        state.connectedSince = new Date().toISOString();
        invoke('RegisterDevice', state.deviceId);
        log(`Conectado al hub y registrado como '${state.deviceId}'`, 'ok');
        continue;
      }

      if (msg.type === 6) { send({ type: 6 }); continue; }   // ping / keep-alive
      if (msg.type === 7) { log('El servidor cerró la conexión', 'error'); continue; }
      if (msg.type !== 1) continue;

      switch (msg.target) {
        case 'StartTelemetry':
          log('La móvil pidió INICIAR medición', 'ok');
          startMeasuring('movil');
          break;
        case 'StopTelemetry':
          log('La móvil pidió DETENER medición', 'ok');
          stopMeasuring();
          break;
        case 'ReceiveAura':
          state.lastAura = msg.arguments?.[0]?.aura ?? null;
          break;
        default:
          break;
      }
    }
  });

  ws.addEventListener('close', () => {
    state.connection = 'desconectado';
    state.connectedSince = null;
    stopMeasuring(true);
    log('WebSocket cerrado', 'error');
    scheduleReconnect();
  });

  ws.addEventListener('error', () => {
    // El evento 'close' llega justo después; ahí se reprograma la reconexión.
  });
}

function scheduleReconnect() {
  clearTimeout(reconnectTimer);
  reconnectTimer = setTimeout(connect, 3000);
}

function startMeasuring(source) {
  state.measuring = true;
  state.measuringSource = source;
  state.sent = 0;
  if (sendTimer) return;
  sendTimer = setInterval(() => {
    if (!state.measuring) return;
    const reading = buildReading();
    if (invoke('SendTelemetry', reading)) {
      state.lastReading = reading;
      state.sent++;
      if (state.scenario === 'ciclo' && ++cycleTick >= CYCLE_SECONDS * RATE_HZ) {
        cycleTick = 0;
        cycleIndex++;
        log(`Ciclo → ${AURA_ORDER[cycleIndex % AURA_ORDER.length]}`);
      }
    }
  }, Math.round(1000 / RATE_HZ));
}

function stopMeasuring(silent = false) {
  state.measuring = false;
  state.measuringSource = null;
  clearInterval(sendTimer);
  sendTimer = null;
  if (!silent) log('Medición detenida');
}

// ─────────────────────────────────────────────────────────────────────────────
//  Panel de control web (solo localhost)
// ─────────────────────────────────────────────────────────────────────────────
function readBody(req) {
  return new Promise((resolve) => {
    let b = '';
    req.on('data', (c) => (b += c));
    req.on('end', () => {
      try { resolve(JSON.parse(b || '{}')); } catch { resolve({}); }
    });
  });
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${UI_PORT}`);
  const json = (obj, code = 200) => {
    res.writeHead(code, { 'Content-Type': 'application/json; charset=utf-8' });
    res.end(JSON.stringify(obj));
  };

  if (url.pathname === '/' ) {
    const html = fs.readFileSync(path.join(__dirname, 'public', 'index.html'));
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    return res.end(html);
  }

  if (url.pathname === '/api/state') {
    return json({ ...state, scenarios: SCENARIOS, rateHz: RATE_HZ });
  }

  if (url.pathname === '/api/scenario' && req.method === 'POST') {
    const { scenario } = await readBody(req);
    if (scenario && (SCENARIOS[scenario] || scenario === 'ciclo' || scenario === 'deriva')) {
      state.scenario = scenario;
      cycleTick = 0;
      log(`Escenario → ${scenario}`);
      return json({ ok: true, scenario });
    }
    return json({ ok: false, error: 'escenario inválido' }, 400);
  }

  if (url.pathname === '/api/measure' && req.method === 'POST') {
    const { on } = await readBody(req);
    if (on) { log('Medición forzada desde el panel'); startMeasuring('manual'); }
    else stopMeasuring();
    return json({ ok: true, measuring: state.measuring });
  }

  if (url.pathname === '/api/reconnect' && req.method === 'POST') {
    try { ws?.close(); } catch {}
    connect();
    return json({ ok: true });
  }

  res.writeHead(404); res.end('not found');
});

server.listen(UI_PORT, () => {
  console.log('');
  console.log('  CORSYNC · simulador del dispositivo IoT');
  console.log(`  hub      : ${HUB_URL}`);
  console.log(`  device   : ${DEVICE_ID}`);
  console.log(`  frecuencia: ${RATE_HZ} Hz`);
  console.log(`  panel    : http://localhost:${UI_PORT}`);
  console.log('');
  connect();
});

process.on('SIGINT', () => {
  stopMeasuring(true);
  try { ws?.close(); } catch {}
  process.exit(0);
});

// Sin esto, cualquier fallo asíncrono tumba el proceso sin dejar una sola línea
// en el log y parece que el simulador "se cayó solo".
process.on('unhandledRejection', (err) => {
  log(`Promesa sin capturar: ${err?.stack || err}`, 'error');
});
process.on('uncaughtException', (err) => {
  log(`Excepción sin capturar: ${err?.stack || err}`, 'error');
  scheduleReconnect();
});
