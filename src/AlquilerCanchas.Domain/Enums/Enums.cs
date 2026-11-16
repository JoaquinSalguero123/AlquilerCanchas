namespace AlquilerCanchas.Domain.Enums;

/// <summary>El diagrama lo define como string; se modela como enum y se persiste como texto.</summary>
public enum EstadoReserva
{
    Pendiente,
    Confirmada,
    Cancelada
}

public enum MetodoPago
{
    Efectivo,
    Transferencia,
    TarjetaDebito,
    TarjetaCredito,
    MercadoPago
}

public enum RolUsuario
{
    Empleado,
    Administrador
}
