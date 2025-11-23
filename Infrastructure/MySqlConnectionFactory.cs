using MySqlConnector;
using Microsoft.Extensions.Configuration;

namespace InnovaTube.Api.Infrastructure;

public class MySqlConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("Connection string 'MySql' no encontrada.");
    }

    public MySqlConnection CreateConnection()
    {
        // SIEMPRE una conexión nueva
        return new MySqlConnection(_connectionString);
    }
}
