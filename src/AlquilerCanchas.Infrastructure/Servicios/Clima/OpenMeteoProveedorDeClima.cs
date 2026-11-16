using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace AlquilerCanchas.Infrastructure.Servicios.Clima;

public class ClimaOpciones
{
    public const string Seccion = "Clima";

    public string UrlBase { get; set; } = "https://api.open-meteo.com/";

    /// <summary>Ubicación del complejo. Por defecto Buenos Aires; se cambia sin tocar código.</summary>
    public double Latitud { get; set; } = -34.6037;
    public double Longitud { get; set; } = -58.3816;
}

/// <summary>
/// Cliente tipado de Open-Meteo (API pública, sin clave). HttpClient lo provee IHttpClientFactory:
/// reutiliza conexiones y respeta los cambios de DNS, a diferencia de crear un HttpClient por llamada.
/// </summary>
public class OpenMeteoProveedorDeClima : IProveedorDeClima
{
    private readonly HttpClient _http;
    private readonly ClimaOpciones _opciones;

    public OpenMeteoProveedorDeClima(HttpClient http, IOptions<ClimaOpciones> opciones)
    {
        _http = http;
        _opciones = opciones.Value;
    }

    public async Task<IReadOnlyList<PronosticoHorario>> ObtenerPronosticoDelDiaAsync(
        DateOnly fecha, CancellationToken ct = default)
    {
        var dia = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lat = _opciones.Latitud.ToString(CultureInfo.InvariantCulture);
        var lon = _opciones.Longitud.ToString(CultureInfo.InvariantCulture);
        var url = $"v1/forecast?latitude={lat}&longitude={lon}" +
                  "&hourly=temperature_2m,precipitation_probability" +
                  $"&start_date={dia}&end_date={dia}&timezone=America%2FArgentina%2FBuenos_Aires";

        try
        {
            using var respuesta = await _http.GetAsync(url, ct);

            // Open-Meteo contesta 400 cuando la fecha cae fuera de su rango de pronóstico: no es una
            // falla del servicio sino "no hay datos", y el caso de uso lo informa con un mensaje claro.
            if (respuesta.StatusCode == HttpStatusCode.BadRequest)
                return [];

            respuesta.EnsureSuccessStatusCode();
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaOpenMeteo>(ct);
            return Mapear(cuerpo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                   && !ct.IsCancellationRequested)
        {
            throw new ServicioExternoNoDisponibleException("Open-Meteo", ex);
        }
    }

    private static IReadOnlyList<PronosticoHorario> Mapear(RespuestaOpenMeteo? r)
    {
        var h = r?.Hourly;
        if (h?.Time is null || h.Temperatura is null || h.ProbLluvia is null) return [];

        var lista = new List<PronosticoHorario>(h.Time.Count);
        for (var i = 0; i < h.Time.Count; i++)
        {
            // Open-Meteo manda null en horas sin dato; esas horas se descartan en vez de contarse como 0 °C.
            if (h.Temperatura[i] is null) continue;
            lista.Add(new PronosticoHorario(
                DateTime.Parse(h.Time[i], CultureInfo.InvariantCulture),
                (decimal)h.Temperatura[i]!.Value,
                h.ProbLluvia[i] ?? 0));
        }
        return lista;
    }

    private sealed record RespuestaOpenMeteo([property: JsonPropertyName("hourly")] Horario? Hourly);

    private sealed record Horario(
        [property: JsonPropertyName("time")] List<string>? Time,
        [property: JsonPropertyName("temperature_2m")] List<double?>? Temperatura,
        [property: JsonPropertyName("precipitation_probability")] List<int?>? ProbLluvia);
}
