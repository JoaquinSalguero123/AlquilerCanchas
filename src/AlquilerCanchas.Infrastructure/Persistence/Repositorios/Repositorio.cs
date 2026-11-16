using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AlquilerCanchas.Infrastructure.Persistence.Repositorios;

/// <summary>Implementación única del CRUD común para todas las entidades.</summary>
public class Repositorio<T> : IRepositorio<T> where T : EntidadBase
{
    protected readonly AppDbContext Contexto;
    protected readonly DbSet<T> Conjunto;

    public Repositorio(AppDbContext contexto)
    {
        Contexto = contexto;
        Conjunto = contexto.Set<T>();
    }

    public async Task<IReadOnlyList<T>> ObtenerTodosAsync(CancellationToken ct = default) =>
        await Conjunto.AsNoTracking().OrderBy(e => e.Id).ToListAsync(ct);

    public async Task<T?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        await Conjunto.FindAsync([id], ct);

    public async Task AgregarAsync(T entidad, CancellationToken ct = default) =>
        await Conjunto.AddAsync(entidad, ct);

    public void Actualizar(T entidad)
    {
        // Si la entidad ya viene rastreada, EF detecta los cambios solo; llamar a Update() marcaría
        // como modificado todo el grafo (Cliente, Cancha, Pagos) y generaría UPDATEs de más.
        if (Contexto.Entry(entidad).State == EntityState.Detached)
            Conjunto.Update(entidad);
    }

    public void Eliminar(T entidad) => Conjunto.Remove(entidad);

    public Task<int> GuardarCambiosAsync(CancellationToken ct = default) => Contexto.SaveChangesAsync(ct);
}
