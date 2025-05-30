using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static readonly string connectionString = "Server=LAPTOP-5NF567DI\\SQLEXPRESS;Database=AquaAI;Trusted_Connection=True;";
    static readonly string openAiApiKey = "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"; // Reemplaza con tu clave OpenAI válida
    static readonly string openAiEndpoint = "https://api.openai.com/v1/chat/completions";

    static void Main(string[] args)
    {
        Console.WriteLine("Ingrese el nombre de la comunidad:");
        string comunidad = Console.ReadLine();

        Console.WriteLine("Ingrese el nivel de agua (Alta, Media, Baja):");
        string nivelAgua = Console.ReadLine();

        Console.WriteLine("Ingrese el tipo de problema (Contaminación, Escasez, etc):");
        string tipoProblema = Console.ReadLine();

        Console.WriteLine("Describa el problema:");
        string descripcion = Console.ReadLine();

        try
        {
            string respuestaAI = ConsultarOpenAI(descripcion).GetAwaiter().GetResult();
            GuardarEnBaseDeDatos(comunidad, nivelAgua, tipoProblema, descripcion, respuestaAI);

            Console.WriteLine("\nRespuesta generada por IA:");
            Console.WriteLine(respuestaAI);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ocurrió un error: " + ex.Message);
        }
    }

    static async Task<string> ConsultarOpenAI(string descripcion)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "Eres un experto en gestión hídrica rural en Guatemala." },
                    new { role = "user", content = $"Analiza el siguiente reporte y sugiere soluciones adaptadas a Jutiapa: {descripcion}" }
                },
                temperature = 0.7
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(openAiEndpoint, content);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Error al consultar la API de OpenAI: " + response.StatusCode + " - " + jsonResponse);
            }

            JObject json = JObject.Parse(jsonResponse);
            var completion = json["choices"]?[0]?["message"]?["content"];
            if (completion != null)
            {
                return completion.ToString().Trim();
            }
            else
            {
                return jsonResponse; // Por si cambia el formato
            }
        }
    }

    static void GuardarEnBaseDeDatos(string comunidad, string nivelAgua, string tipoProblema, string descripcion, string respuesta)
    {
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            string insertReporte = "INSERT INTO Reporte (Comunidad, NivelAgua, TipoProblema, Descripcion, Fecha) OUTPUT INSERTED.ID VALUES (@comunidad, @nivel, @tipo, @desc, @fecha)";
            int idReporte;

            using (var cmd = new SqlCommand(insertReporte, connection))
            {
                cmd.Parameters.AddWithValue("@comunidad", comunidad);
                cmd.Parameters.AddWithValue("@nivel", nivelAgua);
                cmd.Parameters.AddWithValue("@tipo", tipoProblema);
                cmd.Parameters.AddWithValue("@desc", descripcion);
                cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                idReporte = (int)cmd.ExecuteScalar();
            }

            string insertRespuesta = "INSERT INTO RespuestaAI (IDReporte, Respuesta, FechaProcesamiento) VALUES (@id, @resp, @fecha)";
            using (var cmd = new SqlCommand(insertRespuesta, connection))
            {
                cmd.Parameters.AddWithValue("@id", idReporte);
                cmd.Parameters.AddWithValue("@resp", respuesta);
                cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
