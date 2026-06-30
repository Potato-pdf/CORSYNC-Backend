using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using CORSYNC.Core.Domain;
using CORSYNC.Core.Interfaces;

namespace CORSYNC.Api.Hubs
{
    public class TelemetryHub : Hub
    {
        private readonly ILogger<TelemetryHub> _logger;
        private readonly ITelemetryProcessor _processor;

        public TelemetryHub(ILogger<TelemetryHub> logger, ITelemetryProcessor processor)
        {
            _logger = logger;
            _processor = processor;
        }

        // Registrar el prototipo IoT en su grupo correspondiente
        public async Task RegisterDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            string groupName = $"device_{deviceId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Dispositivo IoT '{DeviceId}' registrado en el grupo '{GroupName}'. ConnectionId: {ConnectionId}", 
                deviceId, groupName, Context.ConnectionId);
        }

        // Registrar la aplicación móvil en su grupo correspondiente
        public async Task RegisterMobile(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            string groupName = $"mobile_{deviceId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Cliente Móvil registrado para escuchar al dispositivo '{DeviceId}' en el grupo '{GroupName}'. ConnectionId: {ConnectionId}", 
                deviceId, groupName, Context.ConnectionId);
        }

        // Móvil solicita iniciar la medición en el dispositivo IoT
        public async Task StartMeasurement(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            string deviceGroup = $"device_{deviceId}";
            _logger.LogInformation("Cliente móvil solicitó iniciar medición para dispositivo '{DeviceId}'. Enviando comando a {Group}.", deviceId, deviceGroup);
            
            // Enviar comando "StartTelemetry" al grupo del dispositivo IoT
            await Clients.Group(deviceGroup).SendAsync("StartTelemetry");
        }

        // Móvil solicita detener la medición en el dispositivo IoT
        public async Task StopMeasurement(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            string deviceGroup = $"device_{deviceId}";
            _logger.LogInformation("Cliente móvil solicitó detener medición para dispositivo '{DeviceId}'. Enviar comando a {Group}.", deviceId, deviceGroup);

            // Enviar comando "StopTelemetry" al grupo del dispositivo IoT
            await Clients.Group(deviceGroup).SendAsync("StopTelemetry");
        }

        // Dispositivo IoT envía lectura en tiempo real
        public async Task SendTelemetry(LecturaCorazon lectura)
        {
            if (lectura == null || string.IsNullOrEmpty(lectura.DispositivoId)) return;

            // Validar la lectura (filtra valores anómalos o cuando no hay contacto)
            if (_processor.Validate(lectura))
            {
                // Suavizar la lectura (calcula el promedio móvil actual)
                var smoothed = _processor.Smooth(lectura);

                // Agregar al buffer para almacenamiento masivo en segundo plano (DbFlush)
                _processor.AddToBuffer(smoothed);

                // Retransmitir la lectura procesada en tiempo real solo al grupo móvil asociado
                string mobileGroup = $"mobile_{lectura.DispositivoId}";
                await Clients.Group(mobileGroup).SendAsync("ReceiveTelemetry", smoothed);
            }
            else
            {
                _logger.LogTrace("Lectura descartada por inconsistencia del sensor para dispositivo: {DispId}", lectura.DispositivoId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Cliente desconectado de TelemetryHub: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}

