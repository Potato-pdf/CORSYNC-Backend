using CORSYNC.Core.Domain;

namespace CORSYNC.Core.Interfaces
{
    public interface ITelemetryProcessor
    {
        bool Validate(LecturaCorazon lectura);
        LecturaCorazon Smooth(LecturaCorazon lectura);
        void AddToBuffer(LecturaCorazon lectura);
        LecturaCorazon? FlushBuffer();
    }
}
