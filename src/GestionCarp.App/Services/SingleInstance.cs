using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GestionCarp.App.Services;

/// <summary>
/// Garantiza una sola ventana de GestionCarp. Si se abre otra vez (por ejemplo desde el menú contextual
/// del Explorador con varios archivos seleccionados), la nueva copia pasa sus argumentos a la primera y se cierra.
/// </summary>
public static class SingleInstance
{
    private static Mutex? _mutex;

    private static string Id
    {
        get
        {
            string user = new string(Environment.UserName.Where(char.IsLetterOrDigit).ToArray());
            return "GestionCarp-" + (user.Length == 0 ? "usuario" : user);
        }
    }

    public static bool TryBecomePrimary()
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + Id, out bool created);
        return created;
    }

    /// <summary>Envía los argumentos a la ventana ya abierta. Devuelve false si no responde.</summary>
    public static bool SendToPrimary(string[] args)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", Id, PipeDirection.Out);
                client.Connect(2000);
                var bytes = Encoding.UTF8.GetBytes(string.Join("\n", args));
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return true;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
            {
                Thread.Sleep(300);
            }
        }
        return false;
    }

    /// <summary>Escucha a otras copias que se abran y entrega sus argumentos.</summary>
    public static void StartListening(Action<string[]> onArguments)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(Id, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string text = await reader.ReadToEndAsync();
                    onArguments(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
                {
                    await Task.Delay(500);
                }
            }
        });
    }
}
