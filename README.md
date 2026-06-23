# Sistema de Lectura Biométrica y Visualización del Aura "Espejo Espiritual"

## 1. Resumen Ejecutivo y Propósito del Sistema

El **Sistema de Lectura Biométrica y Visualización del Aura "Espejo Espiritual"** es una solución tecnológica integral y disruptiva que fusiona la analítica de telemetría IoT en tiempo real con el bienestar psicofísico, el misticismo moderno y un modelo comercial de vanguardia. 

El propósito principal del sistema es capturar señales fotopletismográficas (PPG) de los usuarios a través de hardware especializado, procesar y limpiar estos flujos de datos en la nube y traducirlos dinámicamente en una representación visual tridimensional de su "aura" mediante algoritmos generativos de partículas y shaders interactivos en Unity. 

Desde la perspectiva empresarial, el ecosistema cuenta con una plataforma web administrativa y comercial desarrollada en ASP.NET Core que habilita la cotización automatizada de espejos inteligentes personalizados, la venta de consumibles, el seguimiento de clientes y la gestión de la cadena de suministro de materias primas.

---

## 2. Arquitectura del Ecosistema y Flujo de Datos

El sistema implementa una arquitectura desacoplada y altamente escalable estructurada en torno a un monolito modular con procesamiento asíncrono y streaming bidireccional. 

A continuación, se detalla el ciclo de vida del dato desde el sensor físico hasta su visualización y persistencia:

```mermaid
graph TD
    ESP32[Dispositivo IoT: ESP32 + MAX30102] -->|JSON via MQTT / TLS 8883| HiveMQ[Broker: HiveMQ Cloud]
    HiveMQ -->|Suscripción de Tópico| BackgroundWorker[ASP.NET Core Background Worker]
    
    subgraph Servidor Backend (ASP.NET Core Monolith)
        BackgroundWorker -->|1. Selección e Ingesta| InputPipeline[Filtro de Inconsistencias]
        InputPipeline -->|2. Limpieza y Suavizado| DataTransform[Transformador de Datos]
        DataTransform -->|3. Throttling en Memoria| BufferCache[(Buffer Cache / Memory Queue)]
        
        BufferCache -->|4. Push Inmediato| SignalR[SignalR Hub]
        BufferCache -->|5. Flush Periódico| EFCore[Entity Framework Core]
    end

    EFCore -->|Particionamiento Lógico| DB[(Base de Datos)]
    DB -->|AdminDbContext| DBAdmin[(EspejoEspiritual_Admin)]
    DB -->|TelemetryDbContext| DBTelemetry[(EspejoEspiritual_Telemetry)]

    SignalR -->|WebSockets Streaming| MobileApp[Android Native Client]
    MobileApp -->|Interop JNI / C# Bridge| UnityEngine[Unity 3D Engine: Shaders & Partículas]
```

### Descripción del Flujo:
1. **Captura y Transmisión:** El microcontrolador **ESP32** lee los valores de absorción infrarroja (IR) y calcula el pulso cardíaco mediante el sensor **MAX30102**, emitiendo un payload JSON al broker **HiveMQ Cloud** bajo seguridad TLS (puerto 8883).
2. **Ingesta y Limpieza:** El **Background Worker** en ASP.NET Core se suscribe al broker, interceptando el flujo de datos raw para descartar lecturas erráticas (ruido por movimiento).
3. **Throttling y Caché:** Para evitar la degradación del DBMS por exceso de inserciones, los datos se agrupan temporalmente en un búfer en memoria antes de ser guardados de forma agregada.
4. **Streaming en Tiempo Real:** En paralelo a la persistencia, los valores procesados se despachan inmediatamente vía **SignalR** a la aplicación móvil.
5. **Renderizado del Aura:** La aplicación en Android recibe el flujo y alimenta el motor embebido de **Unity 3D**, modulando en tiempo real las propiedades visuales del aura (densidad de partículas, gradientes y velocidad del shader).

---

## 3. Estrategia de Ingesta, Limpieza y Almacenamiento

### Selección, Limpieza y Transformación de Datos
El sensor de pulso cardíaco MAX30102 es susceptible a artefactos de movimiento y pérdidas momentáneas de contacto con la piel. Por ello, el pipeline de ingesta del Background Worker implementa las siguientes políticas de calidad de datos:
* **Filtro de Rangos Físicos (Outliers):** Se descartan lecturas de pulso instantáneo (`bpm`) inferiores a 30 BPM o superiores a 220 BPM.
* **Validación de Señal Infrarroja:** Si la señal infrarroja (`ir`) es inferior a un umbral base (ej. 50,000 unidades), el sistema asume que el usuario ha retirado el dedo del sensor y envía una trama de desconexión en lugar de datos erróneos.
* **Filtro de Media Móvil:** Se aplica un filtro paso bajo en memoria para suavizar las fluctuaciones rápidas no fisiológicas antes del análisis del aura.

### Técnica de Throttling en Memoria
Para garantizar la durabilidad y escalabilidad del DBMS, se utiliza un patrón de acumulación en memoria utilizando colas concurrentes de alto rendimiento (`ConcurrentQueue<T>`).
* El sensor transmite a una frecuencia de ~20-50 Hz (20 a 50 tramas por segundo).
* En lugar de ejecutar 50 inserts/seg por usuario en la base de datos, el Background Worker almacena en caché las lecturas y calcula el promedio ponderado de BPM e IR cada **5 segundos** (parámetro configurable).
* Al cumplirse la ventana temporal, se genera un único registro consolidado en la tabla histórica de telemetría, reduciendo la carga de transacciones sobre la base de datos relacional en más de un 95%.

### Flujo y Especificaciones de Integración

#### Payload Crudo del Sensor (JSON enviado por ESP32 al Broker):
```json
{
  "ir": 102531,
  "bpm": 5.6,
  "bpmAvg": 68
}
```

#### Modelo de Persistencia en Base de Datos (Entity Framework Core):
La persistencia de la telemetría se modela bajo la siguiente clase C#, mapeada a un contexto específico (`TelemetryDbContext`) configurado para soportar esquemas de particionamiento lógico o de bases de datos distribuidas.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EspejoEspiritual.Core.Domain
{
    public class LecturaCorazon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string DispositivoId { get; set; } = "ESP32_MAX30102";

        public long IR { get; set; }

        [Column(TypeName = "decimal(5,1)")]
        public decimal BPM { get; set; }

        public int BPMPromedio { get; set; }

        public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    }
}
```

---

## 4. Módulos de la Aplicación Móvil (Android & Unity)

La aplicación para smartphones es el canal directo de interacción del usuario con el "Espejo Espiritual". Se compone de los siguientes módulos nativos e híbridos:

1. **Login / Register:**
   * Gestión de credenciales bajo protocolo OAuth 2.0 y JWT.
   * Registro de perfil espiritual inicial (cuestionario psicofísico para calibración base del aura).
2. **Escáner Home (Unity Embedded):**
   * Panel principal que incrusta el entorno de ejecución de Unity 3D dentro del layout nativo de Android.
   * Enlace en tiempo real que convierte las métricas de `BPM` y `IR` en variables del sistema de partículas de Unity (ej. el pulso cardíaco altera la pulsación cromática, el estrés capturado por la variabilidad cardíaca modifica la turbulencia del aura).
3. **Gráficas y Oráculo:**
   * Visualización histórica interactiva del comportamiento biométrico (gráficos lineales de pulso y dispersión).
   * Generador de interpretaciones místicas (el "Oráculo") que procesa la telemetría del día mediante reglas heurísticas para ofrecer reflexiones de bienestar y sugerencias de meditación.
4. **Diario e Historial:**
   * Bitácora digital donde el usuario puede registrar su estado emocional percibido y compararlo con el estado analizado por el sensor.
5. **Perfil y Configuración:**
   * Vinculación de sensores IoT mediante Bluetooth Low Energy (BLE) o aprovisionamiento Wi-Fi para MQTT.
   * Configuración de preferencias estéticas de renderizado (esquema de colores base para el aura).
6. **Gamificación:**
   * Sistema de recompensas y logros basados en la consistencia de lecturas diarias, estados de relajación alcanzados (BPM bajos estables) e hitos de meditación guiada.

---

## 5. Módulos del Sistema Web Comercial (Backoffice y Portal)

El portal web cumple una función comercial integral, administrando la cadena de valor y el ciclo de vida del cliente. Está segmentado en tres secciones con control de acceso basado en roles (RBAC):

### A. Sección Pública
* **Landing Page:** Presentación premium e interactiva del ecosistema "Espejo Espiritual", ilustrando la ciencia detrás del sensor y la belleza del renderizado en tiempo real.
* **Empresa / Producto:** Detalle de la misión corporativa, el equipo y la justificación técnica de la fusión biométrica-artística.
* **Comentarios de Clientes:** Muro social que despliega opiniones aprobadas de usuarios actuales.
* **Cotizador Dinámico de Espejos:** 
  * Algoritmo interactivo de cálculo de costos para la fabricación de espejos inteligentes a medida.
  * Fórmulas internas que analizan la materia prima (dimensiones del vidrio, tipo de marco, densidad de tiras LED, inclusión de sensores adicionales) más el coste de ensamblaje para proporcionar una cotización inmediata y formal al cliente en formato PDF.

### B. Sección Administrador
* **Gestión de Usuarios:** Creación, edición y suspensión de cuentas de clientes, administradores y analistas de soporte.
* **Seguimiento de Comentarios:** Moderación activa de los testimonios de la sección pública con capacidad de aprobación, rechazo y ban de spam.
* **Gestión de Proveedores:** Base de datos de proveedores de componentes electrónicos (ESP32, MAX30102, cableado) e insumos físicos (marcos de madera, resinas, cristales de doble vía).
* **Compras y Abastecimiento:** Órdenes de compra automatizadas cuando los componentes descienden del stock mínimo de seguridad.
* **Inventario de Materia Prima:** Control de existencias físicas en almacén centralizado de hardware y cristalería.
* **Explosión de Materiales (BOM - Bill of Materials):**
  * Definición de recetas técnicas para la manufactura de los distintos modelos de espejos (estándar, premium, corporativo).
  * Desglose jerárquico de cada producto final en sus componentes básicos para facilitar el cálculo de costos y la planificación de producción.

### C. Sección Clientes
* **Documentación del Producto:** Acceso de descarga a manuales de usuario oficiales, guías de montaje físico de los espejos y librerías de conexión para el sensor.
* **Historial de Compras:** Consulta detallada de pedidos anteriores, estados de envío (tracking), facturas y detalles de cotizaciones guardadas.
* **Panel de Opiniones:** Formulario exclusivo para que clientes verificados califiquen su experiencia física y digital con el sistema.

---

## 6. Cronograma de Planeación de Backend (Fases de Implementación)

La implementación del backend en ASP.NET Core está estructurada en tres fases iterativas:

| Fase | Título | Descripción de Actividades | Entregables Principales | Estado |
| :--- | :--- | :--- | :--- | :--- |
| **Fase 1** | Infraestructura y Modelos | Configuración del monolito inicial, diseño de las bases de datos relacionales particionadas, y modelado con Entity Framework Core. | `AdminDbContext`, `TelemetryDbContext`, Migraciones iniciales y esquemas de tablas. | Pendiente |
| **Fase 2** | Ingesta MQTT Bridge | Implementación del `IHostedService` (Background Worker) para la suscripción al broker MQTT HiveMQ Cloud bajo TLS. Módulos de sanitización y throttling en memoria. | Pipeline de limpieza de datos, almacenamiento intermedio asíncrono y lógica de guardado en lote. | Pendiente |
| **Fase 3** | Streaming SignalR y API REST | Desarrollo de los Hubs de SignalR para envío de datos en tiempo real. Construcción de los endpoints REST de administración, cotización dinámica y autenticación JWT. | API Controller de cotizador, endpoints de la sección pública/admin y Hubs de transmisión WebSocket. | Pendiente |

---

## 7. Instrucciones de Despliegue y Configuración Básica

Para ejecutar y compilar el backend de ASP.NET Core y el Background Worker del sistema, asegúrese de cumplir los siguientes requisitos y configurar el archivo `appsettings.json` o definir las variables de entorno correspondientes.

### Requisitos Previos
* **Runtime:** .NET 8.0 SDK o superior.
* **Servidor SQL:** Microsoft SQL Server / MySQL Server.
* **Broker MQTT:** Cuenta en HiveMQ Cloud configurada con TLS.

### Configuración del Entorno (`appsettings.json`)

Edite el archivo de configuración del proyecto con los parámetros del servidor de base de datos y la autenticación del broker MQTT:

```json
{
  "ConnectionStrings": {
    "AdminConnection": "Server=localhost;Database=EspejoEspiritual_Admin;User Id=sa;Password=YourSecurePassword123;TrustServerCertificate=True;",
    "TelemetryConnection": "Server=localhost;Database=EspejoEspiritual_Telemetry;User Id=sa;Password=YourSecurePassword123;TrustServerCertificate=True;"
  },
  "HiveMQ": {
    "Host": "your-unique-broker-id.s1.eu.hivemq.cloud",
    "Port": 8883,
    "Username": "App_Gateway_Client",
    "Password": "SuperSecureBrokerPassword2026",
    "UseTls": true
  },
  "TokenConfiguration": {
    "SecretKey": "LLAVE_SECRETA_SUPER_LARGA_Y_COMPLEJA_PARA_JWT_2026",
    "Issuer": "EspejoEspiritualServer",
    "Audience": "EspejoEspiritualClients"
  }
}
```

### Ejecución en Consola
1. **Restaurar Dependencias:**
   ```bash
   dotnet restore
   ```
2. **Aplicar Migraciones de Base de Datos:**
   ```bash
   dotnet ef database update --context AdminDbContext
   dotnet ef database update --context TelemetryDbContext
   ```
3. **Compilar y Ejecutar el Servidor:**
   ```bash
   dotnet run --project Src/EspejoEspiritual.Api/EspejoEspiritual.Api.csproj
   ```
